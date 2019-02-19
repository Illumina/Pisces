using Alignment.Domain.Sequencing;
using Gemini.Interfaces;
using StitchingLogic;

namespace Gemini.Infrastructure
{
    public class DebugStatusHandler : IStatusHandler
    {
        private readonly ReadStatusCounter _statusCounter;

        public DebugStatusHandler(ReadStatusCounter statusCounter)
        {
            _statusCounter = statusCounter;
        }

        public void AddStatusCount(string status)
        {
            _statusCounter.AddStatusCount(status);
        }

        public void AppendStatusStringTag(string tagName, string tagValue, BamAlignment alignment)
        {
            // TODO if there is not already a tag, don't add the comma, it's confusing
            TagUtils.ReplaceOrAddStringTag(ref alignment.TagData, tagName, alignment.GetStringTag(tagName) + "," + tagValue);
        }

        public void UpdateStatusStringTag(string tagName, string tagValue, BamAlignment alignment)
        {
            TagUtils.ReplaceOrAddStringTag(ref alignment.TagData, tagName, tagValue);
        }

        public void AddCombinedStatusStringTags(string tagName, BamAlignment alignment1, BamAlignment alignment2, BamAlignment outAlignment)
        {
            TagUtils.ReplaceOrAddStringTag(ref outAlignment.TagData, tagName,
                alignment1.GetStringTag(tagName) + "," + alignment2.GetStringTag(tagName));
        }
    }
}