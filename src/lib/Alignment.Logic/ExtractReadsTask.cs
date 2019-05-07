using System.Collections.Generic;
using Alignment.Domain;
using Alignment.IO;

namespace Alignment.Logic
{
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
}