using System.Resources;
using Pisces.Domain.Models;

namespace Pisces.Interfaces
{
    public interface IAlignmentSource
    {
        AlignmentSet GetNextAlignmentSet();
        int? LastClearedPosition { get; }
    }
}