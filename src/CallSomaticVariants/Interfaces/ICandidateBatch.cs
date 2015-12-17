using System.Collections.Generic;
using CallSomaticVariants.Logic.RegionState;
using CallSomaticVariants.Models.Alleles;

namespace CallSomaticVariants.Interfaces
{
    public interface ICandidateBatch
    {
        List<Region> ClearedRegions { get; }

        /// <summary>
        /// Maxmimum cleared position for the batch.  
        /// - If this is the final batch, value should be null.  
        /// - If there is nothing in the batch, value should be -1.
        /// </summary>
        int? MaxClearedPosition { get; }  

        bool HasCandidates { get; }

        void Add(CandidateAllele candidate);

        List<CandidateAllele> GetCandidates();
    }
}
