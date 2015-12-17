using CallSomaticVariants.Types;

namespace CallSomaticVariants.Interfaces
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