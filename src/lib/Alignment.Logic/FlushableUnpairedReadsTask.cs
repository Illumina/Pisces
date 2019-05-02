using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Alignment.IO;

namespace Alignment.Logic
{
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
}