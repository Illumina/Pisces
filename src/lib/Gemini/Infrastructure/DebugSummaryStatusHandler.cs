using Alignment.Domain.Sequencing;
using Gemini.Interfaces;
using StitchingLogic;

namespace Gemini.Infrastructure
{
    public class DebugSummaryStatusHandler : IStatusHandler
    {
        private readonly ReadStatusCounter _statusCounter;

        public DebugSummaryStatusHandler(ReadStatusCounter statusCounter)
        {
            _statusCounter = statusCounter;
        }

        public void AddStatusCount(string status)
        {
            _statusCounter.AddStatusCount(status);
        }

        public void AppendStatusStringTag(string tagName, string tagValue, BamAlignment alignment)
        {
        }

        public void UpdateStatusStringTag(string tagName, string tagValue, BamAlignment alignment)
        {
        }

        public void AddCombinedStatusStringTags(string tagName, BamAlignment alignment1, BamAlignment alignment2, BamAlignment outAlignment)
        {
        }
    }
}