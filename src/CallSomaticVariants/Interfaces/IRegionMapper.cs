using CallSomaticVariants.Models;

namespace CallSomaticVariants.Interfaces
{
    public interface IRegionPadder
    {
        void Pad(ICandidateBatch batch, bool mapAll = false);

        ChrIntervalSet IntervalSet { get; }
    }
}
