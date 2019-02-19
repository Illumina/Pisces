using System.Collections.Concurrent;
using System.Collections.Generic;
using Alignment.Domain;
using Alignment.IO;
using Alignment.Logic;
using BamStitchingLogic;
using Common.IO.Utility;
using Gemini.Interfaces;
using Gemini.Logic;
using Gemini.Utility;
using StitchingLogic;

namespace Gemini.Realignment
{
    public class SimpleRealigner : IRealigner
    {
        private readonly StitcherOptions _stitcherOptions;
        private readonly IChromosomeIndelSource _indelSource;
        BlockingCollection<Task> _taskQueue = null;
        ThreadPool _threadPool = null;
        private readonly ReadStatusCounter _statusCounter;
        private readonly string _chromosome;
        private readonly IGeminiDataSourceFactory _dataSourceFactory;
        private readonly IGeminiDataOutputFactory _dataOutputFactory;
        private readonly IBamRealignmentFactory _realignmentFactory;
        private readonly bool _isSnowball;
        private readonly Dictionary<string, int[]> _masterLookup = new Dictionary<string, int[]>();
        private readonly List<IReadStatusCounter> _statusCounters = new List<IReadStatusCounter>();

        public SimpleRealigner(StitcherOptions stitcherOptions, IChromosomeIndelSource indelSource,
            string chromosome, IGeminiDataSourceFactory dataSourceFactory, IGeminiDataOutputFactory dataOutputFactory, 
            IBamRealignmentFactory realignmentFactory, bool isSnowball = false)
        {
            _stitcherOptions = stitcherOptions;
            _indelSource = indelSource;
            _chromosome = chromosome;
            _dataSourceFactory = dataSourceFactory;
            _dataOutputFactory = dataOutputFactory;
            _realignmentFactory = realignmentFactory;
            _isSnowball = isSnowball;

            // TODO combine these
            _statusCounter = new ReadStatusCounter();
        }

        void ExecuteTask(Task task)
        {
            if (_taskQueue != null)
            {
                _taskQueue.Add(task);
            }
            else
            {
                task.Execute(0);
            }
        }

        private List<IReadPairHandler> CreatePairHandlers(int numThreads, string inBam, bool tryRestitch, bool alreadyStitched, bool pairAwareRealign)
        {
            var handlers = new List<IReadPairHandler>(numThreads);


            for (int i = 0; i < numThreads; ++i)
            {
                var statusCounter = new ReadStatusCounter();
                _statusCounters.Add(statusCounter);

                var handler = _realignmentFactory.GetRealignPairHandler(tryRestitch, alreadyStitched, pairAwareRealign, _dataSourceFactory.GetRefIdMapping(inBam), statusCounter, _isSnowball, _indelSource, _chromosome, _masterLookup);

                handlers.Add(handler);
            }

            return handlers;
        }


        public void Execute(string inBam, string outBam, bool tryRestitch, bool alreadyStitched, bool pairAwareRealign)
        {
            var numThreads = _stitcherOptions.NumThreads;

            Logger.WriteToLog($"Executing realignment on {inBam} with output at {outBam}. Settings: tryRestitch={tryRestitch}, alreadyStitched={alreadyStitched}, pairAwareRealign={pairAwareRealign}");

            _taskQueue = new BlockingCollection<Task>(4 * numThreads);
            _threadPool = new ThreadPool(_taskQueue, numThreads);

            var pairHandlers = CreatePairHandlers(numThreads, inBam, tryRestitch, alreadyStitched, pairAwareRealign);

            var writerFactory = _dataOutputFactory.GetBamWriterFactory(inBam);
            //var writerFactory = new BamWriterFactory(_stitcherOptions, inBam); // TODO pass in cache size here, or stop making it configurable

            var count = 0;
            var increment = 10000;

            IBamWriterMultithreaded bamWriter = writerFactory.CreateBamWriter(outBam);
            using (bamWriter)
            {
                var bamWriterHandles = bamWriter.GenerateHandles();
                using (var bamReader = _dataSourceFactory.CreateBamReader(inBam))
                {
                    using (var readPairSource = _dataSourceFactory.CreateReadPairSource(bamReader, _statusCounter))
                    {
                        const int READ_BUFFER_SIZE = 64;
                        List<ReadPair> readPairBuffer = new List<ReadPair>(READ_BUFFER_SIZE);

                        ReadPair readPairEntry;
                        while ((readPairEntry = readPairSource.GetNextEntryUntilNull()) != null)
                        {
                            _statusCounter.AddStatusCount("Total read pairs");
                            count++;    
                            if (count % increment == 0)
                            {
                                Logger.WriteToLog("Processed " + count);
                            }

                            if (readPairEntry != null)
                            {
                                readPairBuffer.Add(readPairEntry);
                                if (readPairBuffer.Count >= READ_BUFFER_SIZE - 1)
                                {
                                    ExecuteTask(new SimpleExtractReadsAndRealignTask(pairHandlers, readPairBuffer, bamWriterHandles));

                                    readPairBuffer = new List<ReadPair>(READ_BUFFER_SIZE);
                                }
                            }
                        }

                        if (readPairBuffer.Count > 0)
                        {
                            ExecuteTask(new SimpleExtractReadsAndRealignTask(pairHandlers, readPairBuffer, bamWriterHandles));
                        }
                    }

                    WaitForFinishTask.WaitUntilZeroTasks();
                }

                _threadPool?.RunToCompletion();

                foreach (var pairHandler in pairHandlers)
                {
                    pairHandler.Finish();
                }

                bamWriter.Flush(); // Important!
            }

            foreach (var counter in _statusCounters)
            {
                _statusCounter.Merge((ReadStatusCounter)counter); // TODO
            }
            StatusLogger.LogStatuses(inBam, _statusCounter);

            if (_isSnowball)
            {
                Logger.WriteToLog($"Added support for {_masterLookup.Keys.Count} indels from snowball (obs,left,right,mess,quals,fwd,reverse,stitched,reput)");
                foreach (var kvp in _masterLookup)
                {
                    Logger.WriteToLog($"{kvp.Key} : {string.Join(",",kvp.Value)}");
                }
            }
        }

        public Dictionary<string, int[]> GetIndels()
        {
            return _masterLookup;
        }
    }
}