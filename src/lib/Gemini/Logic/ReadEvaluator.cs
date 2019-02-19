using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alignment.Domain;
using Alignment.IO;
using Alignment.Logic;
using BamStitchingLogic;
using Common.IO.Utility;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.Types;
using Gemini.Utility;
using StitchingLogic;

namespace Gemini.Logic
{
    public class ReadEvaluator : ICategorizedBamAndIndelEvidenceSource
    {
        private readonly PairClassification[] _pairClassificationsToCount = new[]{
            PairClassification.PerfectStitched, PairClassification.ImperfectStitched,
            PairClassification.FailStitch, PairClassification.UnstitchIndel, PairClassification.Split,
            PairClassification.Unstitchable, PairClassification.Disagree, PairClassification.Unknown,
            PairClassification.Unusable, PairClassification.MessyStitched, PairClassification.MessySplit, PairClassification.UnusableSplit,
            PairClassification.UnstitchImperfect
        };

        private readonly PairClassification[] _pairClassificationsToWriteBamsFor = new[]
        {
            PairClassification.PerfectStitched, PairClassification.ImperfectStitched,
            PairClassification.FailStitch, PairClassification.UnstitchIndel, PairClassification.Split,
            PairClassification.Unstitchable,
            PairClassification.Disagree, PairClassification.MessyStitched, PairClassification.MessySplit,
            PairClassification.UnusableSplit,
            PairClassification.UnstitchImperfect
        };

        private readonly StitcherOptions _options;
        private readonly string _inBam;
        private readonly string _outBam;
        private readonly bool _trustSoftclips;
        private readonly bool _skipStitching;
        private const int NumEvidenceDataPoints = 9; // TODO share throughout

        BlockingCollection<Task> _taskQueue = null;
        ThreadPool _threadPool = null;
        readonly ReadStatusCounter _statusCounter = new ReadStatusCounter();
        private readonly Dictionary<string, Dictionary<PairClassification, List<string>>> _perChromBamFiles = new Dictionary<string, Dictionary<PairClassification, List<string>>>();
        private readonly bool _deferStitchIndelReads;
        private Dictionary<string, int[]> _masterLookup;
        private readonly int? _refId;
        private readonly bool _alwaysTryStitching;

        public ReadEvaluator(StitcherOptions options, string inBam, string outBam, GeminiOptions geminiOptions, int? refId = null)
        {
            _options = options;
            _inBam = inBam;
            _outBam = outBam;
            _deferStitchIndelReads = geminiOptions.DeferStitchIndelReads;
            _refId = refId;
            _alwaysTryStitching = geminiOptions.StitchOnly;
            _trustSoftclips = geminiOptions.TrustSoftclips;
            _skipStitching = geminiOptions.SkipStitching;
        }

        private List<IReadPairHandler> CreatePairHandlers(int numThreads, string inBam, IGeminiDataSourceFactory dataSourceFactory)
        {
            var handlers = new List<IReadPairHandler>(numThreads);
            var refIdMapping = dataSourceFactory.GetRefIdMapping(inBam);

            for (int i = 0; i < numThreads; ++i)
            {
                // TODO - cleanup - maybe move this out to factory
                var stitcher = new BasicStitcher(_options.MinBaseCallQuality, useSoftclippedBases: _options.UseSoftClippedBases,
                    nifyDisagreements: _options.NifyDisagreements, debug: _options.Debug, nifyUnstitchablePairs: _options.NifyUnstitchablePairs, ignoreProbeSoftclips: !_options.StitchProbeSoftclips, maxReadLength: _options.MaxReadLength, ignoreReadsAboveMaxLength: _options.IgnoreReadsAboveMaxLength, thresholdNumDisagreeingBases: _options.MaxNumDisagreeingBases);

                handlers.Add(new PairHandler(refIdMapping, stitcher, _statusCounter, tryStitch: !_skipStitching));
            }

            return handlers;
        }

        private List<IReadPairClassifierAndExtractor> CreateClassifiers(int numThreads)
        {
            var classifiers = new List<IReadPairClassifierAndExtractor>(numThreads);

            for (int i = 0; i < numThreads; ++i)
            {
                classifiers.Add(new ReadPairClassifierAndExtractor(_trustSoftclips, _deferStitchIndelReads, (int)_options.FilterMinMapQuality, skipStitch: _skipStitching, alwaysTryStitch: _alwaysTryStitching));
            }

            return classifiers;
        }

        private List<IndelTargetFinder> CreateTargetFinders(int numThreads)
        {
            var targetFinders = new List<IndelTargetFinder>(numThreads);

            for (int i = 0; i < numThreads; ++i)
            {
                targetFinders.Add(new IndelTargetFinder());
            }

            return targetFinders;
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


        public Dictionary<string, Dictionary<PairClassification, List<string>>> GetCategorizedAlignments()
        {
            return _perChromBamFiles;
        }

        public Dictionary<string, int[]> GetIndelStringLookup()
        {
            return _masterLookup;
        }

        public void CollectAndCategorize(IGeminiDataSourceFactory dataSourceFactory, IGeminiDataOutputFactory dataOutputFactory)
        {
            // Iterate through pairs, determine which bucket to send to, and return the proper reads to output
            var taskThreadRatio = 4;
            _taskQueue = new BlockingCollection<Task>(taskThreadRatio * _options.NumThreads);
            _threadPool = new ThreadPool(_taskQueue, _options.NumThreads);

            // TODO less hacky
            var indelLookups = new List<Dictionary<string, int[]>>();
            for (int i = 0; i < _options.NumThreads; i++)
            {
                indelLookups.Add(new Dictionary<string, int[]>());
            }

            var pairHandlers = CreatePairHandlers(_options.NumThreads, _inBam, dataSourceFactory);
            var classifiers = CreateClassifiers(_options.NumThreads);
            var targetFinders = CreateTargetFinders(_options.NumThreads);

            var refIdMapping = dataSourceFactory.GetRefIdMapping(_inBam);

            var chromosomes = _refId != null? new List<string>(){refIdMapping[_refId.Value]} : refIdMapping.Values.ToList();
            var counters = GetCounters(_options.NumThreads);
            {
                var bamWriterHandles = GetHandles(_outBam, chromosomes, dataOutputFactory, _options.NumThreads);

                using (var bamReader = dataSourceFactory.CreateBamReader(_inBam))
                {
                    using (var readPairSource =
                        dataSourceFactory.CreateReadPairSource(bamReader, _statusCounter))
                    {
                        const int READ_BUFFER_SIZE = 64;
                        var readPairBuffer = new List<ReadPair>(READ_BUFFER_SIZE);

                        ReadPair readPairEntry;
                        while ((readPairEntry = readPairSource.GetNextEntryUntilNull()) != null)
                        {
                            if (readPairEntry != null)
                            {
                                readPairBuffer.Add(readPairEntry);
                                if (readPairBuffer.Count >= READ_BUFFER_SIZE - 1)
                                {
                                    ExecuteTask(new ClassificationAndCollectionExtractReadsTask(pairHandlers, classifiers, targetFinders, readPairBuffer, counters,
                                        bamWriterHandles, indelLookups, refIdMapping, trustSoftclips:_trustSoftclips));

                                    readPairBuffer = new List<ReadPair>(READ_BUFFER_SIZE);
                                }
                            }
                        }

                        if (readPairBuffer.Count > 0)
                        {
                            ExecuteTask(new ClassificationAndCollectionExtractReadsTask(pairHandlers, classifiers, targetFinders, readPairBuffer, counters, bamWriterHandles,
                                indelLookups, refIdMapping, trustSoftclips: _trustSoftclips));
                        }
                    }

                    WaitForFinishTask.WaitUntilZeroTasks();
                    Logger.WriteToLog("Finished processing from read pair source.");
                }

                _threadPool?.RunToCompletion();

                foreach (var pairHandler in pairHandlers)
                {
                    pairHandler.Finish();
                }

                foreach (var chrom in bamWriterHandles.Keys)
                {
                    foreach (var pairClassification in bamWriterHandles[chrom].Keys)
                    {
                        foreach (var writer in bamWriterHandles[chrom][pairClassification])
                        {
                            writer.WriteAlignment(null);
                        }
                    }
                }
            }


            var masterLookup = new Dictionary<string, int[]>();

            foreach (var dictionary in indelLookups)
            {
                foreach (var key in dictionary.Keys)
                {
                    if (!masterLookup.ContainsKey(key))
                    {
                        masterLookup.Add(key, new int[NumEvidenceDataPoints]);
                    }

                    for (int i = 0; i < NumEvidenceDataPoints; i++)
                    {
                        masterLookup[key][i] += dictionary[key][i];
                    }
                }
            }

            foreach (var classification in counters.Keys)
            {
                Logger.WriteToLog(Path.GetFileName(_inBam) + "\t | CATEGORYCOUNT " + classification+ " | " + counters[classification].Sum());
            }

            StatusLogger.LogStatuses(_inBam, _statusCounter);
            _masterLookup = masterLookup;
        }

        private Dictionary<PairClassification, int[]> GetCounters(int numThreads)
        {
            var lookup = new Dictionary<PairClassification, int[]>();

            foreach (var pairClassification in _pairClassificationsToCount)
            {
                lookup.Add(pairClassification, new int[numThreads]);
            }

            return lookup;
        }

        private Dictionary<string, Dictionary<PairClassification, List<IBamWriterHandle>>> GetHandles(string outStub, IEnumerable<string> chroms, IGeminiDataOutputFactory factory, int numThreads)
        {
            var writerFactory = factory.GetBamWriterFactory(_inBam);
            var masterLookup = new Dictionary<string, Dictionary<PairClassification, List<IBamWriterHandle>>>();
            foreach (var chrom in chroms)
            {
                _perChromBamFiles[chrom] = new Dictionary<PairClassification, List<string>>();
                var lookup = new Dictionary<PairClassification, List<IBamWriterHandle>>();

                foreach (var pairClassification in _pairClassificationsToWriteBamsFor)
                {
                    var classificationHandles = new List<IBamWriterHandle>();
                    var outPath = Path.Combine(outStub + "_" + pairClassification + "_" + chrom);

                    var threadOutPaths = new List<string>();
                    for (int i = 0; i < numThreads; i++)
                    {
                        var threadOutPath = outPath + "_" + i;
                        threadOutPaths.Add(threadOutPath);
                        classificationHandles.Add(writerFactory.CreateSingleBamWriter(threadOutPath));
                    }

                    lookup.Add(pairClassification, classificationHandles);
                    _perChromBamFiles[chrom].Add(pairClassification, threadOutPaths);
                }

                masterLookup.Add(chrom, lookup);
            }

            return masterLookup;
        }
 

    }

   
}