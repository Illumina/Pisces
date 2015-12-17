using CallSomaticVariants.Models;

namespace CallSomaticVariants.Interfaces
{
    public interface IAlignmentMateFinder
    {
        Read GetMate(Read bamAlignment);
        int? LastClearedPosition { get; }
    }
}
