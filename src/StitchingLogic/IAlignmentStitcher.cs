using Pisces.Domain.Models;

namespace StitchingLogic
{
    public interface IAlignmentStitcher
    {
        bool TryStitch(AlignmentSet pairedAlignment);

        ReadStatusCounter GetStatusCounter();

        void SetStatusCounter(ReadStatusCounter counter);
    }
}