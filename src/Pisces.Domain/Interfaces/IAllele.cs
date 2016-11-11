
using Pisces.Domain.Types;

namespace Pisces.Domain.Interfaces
{
    public interface IAllele
    {
        string Chromosome { get; }
        int Coordinate { get; } 
        string Reference { get; }
        string Alternate { get; }

        AlleleCategory Type { get; }
    }
}