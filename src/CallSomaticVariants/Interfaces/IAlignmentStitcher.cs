using CallSomaticVariants.Models;

namespace CallSomaticVariants.Interfaces
{
    public interface IAlignmentStitcher
    {
        void TryStitch(AlignmentSet pairedAlignment);
    }
}