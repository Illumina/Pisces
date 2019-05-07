using System.Collections.Generic;
using System.Collections.Concurrent;
using Common.IO.Utility;
using Alignment.Domain;
using Alignment.IO;
using Alignment.Domain.Sequencing;
using Alignment.IO.Sequencing;


namespace Alignment.Logic
{
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