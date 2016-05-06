using Pisces.Domain.Models;

namespace Pisces.Interfaces
{
    public interface IAlignmentStitcher
    {
        bool TryStitch(AlignmentSet pairedAlignment);
    }
}