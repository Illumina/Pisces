using CallSomaticVariants.Models;

namespace CallSomaticVariants.Interfaces
{
    public interface IAlignmentExtractor 
    {
        bool GetNextAlignment(Read read);

        void JumpToChromosome(string chromosomeName);

        string ChromosomeFilter { get; }
    }
}