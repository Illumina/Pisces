using Pisces.Domain.Models;

namespace Pisces.Domain.Interfaces
{
    public interface IAlignmentMateFinder
    {
        Read GetMate(Read bamAlignment);
        int? LastClearedPosition { get; }
        int ReadsSkipped { get; }
    }
}
