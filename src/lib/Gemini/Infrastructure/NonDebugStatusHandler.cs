using Alignment.Domain.Sequencing;
using Gemini.Interfaces;

namespace Gemini.Infrastructure
{
    public class NonDebugStatusHandler : IStatusHandler
    {
        public void AddStatusCount(string status)
        {
        }

        public void UpdateStatusStringTag(string tagName, string tagValue, BamAlignment alignment)
        {
        }

        public void AppendStatusStringTag(string tagName, string tagValue, BamAlignment alignment)
        {
        }

        public void AddCombinedStatusStringTags(string tagName, BamAlignment alignment1, BamAlignment alignment2, BamAlignment outAlignment)
        {
        }
    }
}