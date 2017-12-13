using Alignment.Domain.Sequencing;

namespace Stitcher
{
    public interface IDuplicateIdentifier
    {
        bool IsDuplicate(BamAlignment alignment);
    }
}