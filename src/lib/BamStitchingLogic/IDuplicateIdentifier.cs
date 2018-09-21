using Alignment.Domain.Sequencing;

namespace BamStitchingLogic
{
    public interface IDuplicateIdentifier
    {
        bool IsDuplicate(BamAlignment alignment);
    }
}