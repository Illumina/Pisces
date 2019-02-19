using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Gemini.IndelCollection;

namespace Gemini.Interfaces
{
    public interface IEvidenceCollector
    {
        void CollectEvidence(BamAlignment alignment, bool isReputable, bool isStitched, string chromosome);
        Dictionary<string, int[]> GetEvidence();
    }

    public class NonSnowballEvidenceCollector : IEvidenceCollector
    {
        public void CollectEvidence(BamAlignment alignment, bool isReputable, bool isStitched, string chromosome)
        {
            // Not doing anything here for now
        }

        public Dictionary<string, int[]> GetEvidence()
        {
            return new Dictionary<string, int[]>();
        }
    }

    public class SnowballEvidenceCollector : IEvidenceCollector
    {
        private readonly IndelTargetFinder _targetFinder;
        private readonly Dictionary<string, int[]> _lookup = new Dictionary<string, int[]>();

        public SnowballEvidenceCollector(IndelTargetFinder targetFinder)
        {
            _targetFinder = targetFinder;
        }

        public void CollectEvidence(BamAlignment alignment, bool isReputable, bool isStitched, string chromosome)
        {
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(alignment, _targetFinder, _lookup, isReputable, chromosome, 30, isStitched);
        }

        public Dictionary<string, int[]> GetEvidence()
        {
            return _lookup;
        }
    }

}