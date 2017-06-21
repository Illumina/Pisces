
using Pisces.Domain.Types;

namespace Pisces.Domain.Interfaces
{
    public interface IAllele
    {
        string Chromosome { get; }
        int ReferencePosition { get; } 
        string ReferenceAllele { get; }
        string AlternateAllele { get; }

        AlleleCategory Type { get; }
    }
}