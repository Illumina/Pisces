using Alignment.Domain.Sequencing;

namespace BamStitchingLogic
{
    public class AlignmentFlagDuplicateIdentifier : IDuplicateIdentifier
    {
        public bool IsDuplicate(BamAlignment alignment)
        {
            return alignment.IsDuplicate();
        }
    }
}