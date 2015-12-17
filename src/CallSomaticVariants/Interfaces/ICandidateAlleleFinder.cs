using System.Collections.Generic;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;

namespace CallSomaticVariants.Interfaces
{
    public interface ICandidateVariantFinder
    {
        IEnumerable<CandidateAllele> FindCandidates(AlignmentSet alignmentSet, string genome, string chromosomeName);
    }
}