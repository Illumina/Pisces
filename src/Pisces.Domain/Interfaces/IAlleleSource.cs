using System.Collections.Generic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Domain.Interfaces
{
    public interface IAlleleSource
    {
        void AddCandidates(IEnumerable<CandidateAllele> candidateVariants);

        int GetAlleleCount(int position, AlleleType alleleType, DirectionType directionType);

        void AddGappedMnvRefCount(Dictionary<int, int> countsByPosition);

        int GetGappedMnvRefCount(int position);

        List<ReadCoverageSummary> GetSpanningReadSummaries(int startPosition, int endPosition);
    }
}
