using Pisces.Domain.Models;
using System.Collections.Generic;

namespace StitchingLogic
{
    public interface IAlignmentStitcher
    {
        StitchingResult TryStitch(AlignmentSet pairedAlignment);

        ReadStatusCounter GetStatusCounter();

        void SetStatusCounter(ReadStatusCounter counter);
    }
}