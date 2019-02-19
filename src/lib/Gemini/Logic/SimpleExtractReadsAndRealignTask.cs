using System.Collections.Generic;
using Alignment.Domain;
using Alignment.IO;
using Alignment.Logic;

namespace Gemini.Logic
{
    public class SimpleExtractReadsAndRealignTask : WaitForFinishTask
    {
        private readonly List<IReadPairHandler> _pairHandlers;
        private readonly List<ReadPair> _readPairs;
        private readonly List<IBamWriterHandle> _bamWriterHandles;

        public SimpleExtractReadsAndRealignTask(List<IReadPairHandler> pairHandlers, List<ReadPair> readPairs,
            List<IBamWriterHandle> bamWriterHandles) : base()
        {
            _pairHandlers = pairHandlers;
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
                    _bamWriterHandles[threadNum].WriteAlignment(bamAlignment);

                }

            }
        }

    }
}