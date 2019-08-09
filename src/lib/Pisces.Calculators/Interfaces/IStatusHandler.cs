using Alignment.Domain.Sequencing;

namespace Gemini.Interfaces
{
    public interface IStatusHandler
    {
        void AddStatusCount(string status);
        void UpdateStatusStringTag(string tagName, string tagValue, BamAlignment alignment);
        void AppendStatusStringTag(string tagName, string tagValue, BamAlignment alignment);
        void AddCombinedStatusStringTags(string tagName, BamAlignment alignment1, BamAlignment alignment2, BamAlignment outAlignment);
    }
}