using CallSomaticVariants.Models;

namespace CallSomaticVariants.Interfaces
{
    public interface IAlignmentSource
    {
        AlignmentSet GetNextAlignmentSet();
        int? LastClearedPosition { get; }
        string ChromosomeFilter { get; }
    }
}