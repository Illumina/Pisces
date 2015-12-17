using System.Collections.Generic;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Interfaces
{
    public interface IStateManager 
    {
        void AddCandidates(IEnumerable<CandidateAllele> candidateVariants);

        void AddAlleleCounts(AlignmentSet alignmentSet);

        ICandidateBatch GetCandidatesToProcess(int? upToPosition, ChrReference chrReference = null);

        void DoneProcessing(ICandidateBatch batch);

        int GetAlleleCount(int position, AlleleType alleleType, DirectionType directionType);

        void AddGappedMnvRefCount(Dictionary<int, int> countsByPosition);

        int GetGappedMnvRefCount(int position);
    }
}

