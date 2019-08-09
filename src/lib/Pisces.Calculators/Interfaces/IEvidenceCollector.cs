using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Gemini.IndelCollection;

namespace Gemini.Interfaces
{
    public interface IEvidenceCollector
    {
        void CollectEvidence(BamAlignment alignment, bool isReputable, bool isStitched, string chromosome);
        Dictionary<string, IndelEvidence> GetEvidence();
    }

    public class NonSnowballEvidenceCollector : IEvidenceCollector
    {
        public void CollectEvidence(BamAlignment alignment, bool isReputable, bool isStitched, string chromosome)
        {
            // Not doing anything here for now
        }

        public Dictionary<string, IndelEvidence> GetEvidence()
        {
            return new Dictionary<string, IndelEvidence>();
        }
    }

    public class SnowballEvidenceCollector : IEvidenceCollector
    {
        private readonly IndelTargetFinder _targetFinder;
        private readonly Dictionary<string, IndelEvidence> _lookup = new Dictionary<string, IndelEvidence>();

        public SnowballEvidenceCollector(IndelTargetFinder targetFinder)
        {
            _targetFinder = targetFinder;
        }

        public void CollectEvidence(BamAlignment alignment, bool isReputable, bool isStitched, string chromosome)
        {
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(alignment, _targetFinder, _lookup, isReputable, chromosome, 30, isStitched);
        }

        public Dictionary<string, IndelEvidence> GetEvidence()
        {
            return _lookup;
        }
    }

}