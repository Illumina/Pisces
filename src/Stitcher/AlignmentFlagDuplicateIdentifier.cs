using Alignment.IO.Sequencing;
using Alignment.Domain.Sequencing;

namespace Stitcher
{
    public class AlignmentFlagDuplicateIdentifier : IDuplicateIdentifier
    {
        public bool IsDuplicate(BamAlignment alignment)
        {
            return alignment.IsDuplicate();
        }
    }
}