using Pisces.Domain.Models;
using System.Collections.Generic;

namespace StitchingLogic
{
    public interface IAlignmentStitcher
    {
        bool TryStitch(AlignmentSet pairedAlignment);

        ReadStatusCounter GetStatusCounter();

        void SetStatusCounter(ReadStatusCounter counter);
    }
}