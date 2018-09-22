using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Common.IO.Utility;
using Alignment.Domain;
using Alignment.IO;
using Alignment.Domain.Sequencing;
using Alignment.IO.Sequencing;

using System.Threading;


namespace Alignment.Logic
{
    public interface IAlignmentPairFilter
    {
        bool ReachedFlushingCheckpoint(BamAlignment bamAlignment);
        IEnumerable<BamAlignment> GetFlushableUnpairedReads();
        ReadPair TryPair(BamAlignment bamAlignment);
        IEnumerable<BamAlignment> GetUnpairedAlignments(bool b);
        bool ReadIsBlacklisted(BamAlignment bamAlignment);
    }

    public abstract class WaitForFinishTask : Task
    {
        private static int _numTasks = 0;
        private static AutoResetEvent _event = new AutoResetEvent(false);

        public WaitForFinishTask()
        {
            Interlocked.Increment(ref _numTasks);
        }

        public override void Execute(int threadNum)
        {
            try
            {
                ExecuteImpl(threadNum);

                Interlocked.Decrement(ref _numTasks);
                _event.Set();
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to execute task: {e.Message}", e);
            }
        }

        public static void WaitUntilZeroTasks()
        {
            while (_numTasks > 0)
            {
                _event.WaitOne();
            }
        }

        public abstract void ExecuteImpl(int threadNum);
    }



    public class ExtractReadsTask : WaitForFinishTask
    {
        private List<IReadPairHandler> _pairHandlers;
        private readonly IAlignmentPairFilter _filter;
        private readonly List<ReadPair> _readPairs;
        private List<IBamWriterHandle> _bamWriterHandles;

        public ExtractReadsTask(List<IReadPairHandler> pairHandlers, List<ReadPair> readPairs,
            List<IBamWriterHandle> bamWriterHandles, IAlignmentPairFilter filter = null) : base()
        {
            _pairHandlers = pairHandlers;
            _filter = filter;
            _readPairs = readPairs;
            _bamWriterHandles = bamWriterHandles;
        }

        public override void ExecuteImpl(int threadNum)
        {
            var pairHandler = _pairHandlers[threadNum];

            foreach (ReadPair readPair in _readPairs)
            {
                var bamAlignmentList = pairHandler.ExtractReads(readPair);
                foreach (var bamAlignment in bamAlignmentList)
                {
                    if (_filter == null || !_filter.ReadIsBlacklisted(bamAlignment))
                    {
                        _bamWriterHandles[threadNum].WriteAlignment(bamAlignment);
                    }
                }
            }
        }
    }

    class FlushableUnpairedReadsTask : WaitForFinishTask
    {
        private readonly IEnumerable<BamAlignment> _bamAlignments;
        private List<IBamWriterHandle> _bamWriterHandles;

        public FlushableUnpairedReadsTask(IEnumerable<BamAlignment> bamAlignments, List<IBamWriterHandle> bamWriterHandles) : base()
        {
            _bamAlignments = bamAlignments;
            _bamWriterHandles = bamWriterHandles;
        }

        public override void ExecuteImpl(int threadNum)
        {
            foreach (var bamAlignment in _bamAlignments)
            {
                _bamWriterHandles[threadNum].WriteAlignment(bamAlignment);
            }
        }
    }

    public class BamRewriter
    {
        private readonly IBamReader _bamReader;
        private readonly IBamWriterMultithreaded _bamWriter;
        private readonly IAlignmentPairFilter _filter;
        private readonly List<IReadPairHandler> _pairHandlers;
        private readonly bool _getUnpaired;
        private readonly string _chrFilter;
        private BlockingCollection<Task> _taskQueue;

        public BamRewriter(IBamReader bamReader, IBamWriterMultithreaded bamWriter, IAlignmentPairFilter filter,
          List<IReadPairHandler> pairHandlers, BlockingCollection<Task> taskQueue, bool getUnpaired = false, string chrFilter = null)
        {
            _bamReader = bamReader;
            _bamWriter = bamWriter;
            _filter = filter;
            _pairHandlers = pairHandlers;
            _getUnpaired = getUnpaired;
            _chrFilter = chrFilter;
            _taskQueue = taskQueue;
        }

        public void Execute()
        {
            var bamAlignment = new BamAlignment();
            var bamWriterHandles = _bamWriter.GenerateHandles();

            int? chrFilterRefIndex = null;

            if (_chrFilter != null)
            {
                chrFilterRefIndex = _bamReader.GetReferenceIndex(_chrFilter);
            }

            const int READ_BUFFER_SIZE = 64;
            List<ReadPair> readPairBuffer = new List<ReadPair>(READ_BUFFER_SIZE);

            while (true)
            {
                var hasMoreReads = _bamReader.GetNextAlignment(ref bamAlignment, false);
                if (!hasMoreReads) break;

                if (chrFilterRefIndex != null)
                {
                    if (bamAlignment.RefID < chrFilterRefIndex.Value)
                    {
                        continue;
                    }
                    if (bamAlignment.RefID > chrFilterRefIndex.Value)
                    {
                        Logger.WriteToLog("Ending BAM reading for " + _chrFilter + ".");
                        break;
                    }
                }

                if (_getUnpaired && _filter.ReachedFlushingCheckpoint(bamAlignment))
                {
                    ExecuteTask(new FlushableUnpairedReadsTask(_filter.GetFlushableUnpairedReads(), bamWriterHandles));
                }

                var filteredReadPair = _filter.TryPair(bamAlignment);
                if (filteredReadPair != null)
                {
                    readPairBuffer.Add(filteredReadPair);
                    if (readPairBuffer.Count >= READ_BUFFER_SIZE - 1)
                    {
                        ExecuteTask(new ExtractReadsTask(_pairHandlers, readPairBuffer, bamWriterHandles, _filter));

                        readPairBuffer = new List<ReadPair>(READ_BUFFER_SIZE);
                    }
                }
            }

            if (readPairBuffer.Count > 0)
            {
                ExecuteTask(new ExtractReadsTask(_pairHandlers, readPairBuffer, bamWriterHandles, _filter));
            }

            if (_getUnpaired)
            {
                ExecuteTask(new FlushableUnpairedReadsTask(_filter.GetFlushableUnpairedReads(), bamWriterHandles));
            }

            WaitForFinishTask.WaitUntilZeroTasks();
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
    }
}