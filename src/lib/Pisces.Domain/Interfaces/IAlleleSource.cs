using System.Collections.Generic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Domain.Interfaces
{
    public interface IAlleleSource
    {
        void AddCandidates(IEnumerable<CandidateAllele> candidateVariants);

        int GetAlleleCount(int position, AlleleType alleleType, DirectionType directionType, int minAnchor = 0, int? maxAnchor = null, bool fromEnd = false, bool symmetric = false);

        int GetCollapsedReadCount(int position, ReadCollapsedType type);

		double GetSumOfAlleleBaseQualities(int position, AlleleType alleleType, DirectionType directionType, int minAnchor = 0, int? maxAnchor = null, bool fromEnd = false, bool symmetric = false);
		void AddGappedMnvRefCount(Dictionary<int, int> countsByPosition);

        int GetGappedMnvRefCount(int position);

        List<ReadCoverageSummary> GetSpanningReadSummaries(int startPosition, int endPosition);

        bool ExpectStitchedReads { get; }
    }
}
