using System.Collections.Generic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Processing.Interfaces
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

        void Add(List<CandidateAllele> candidates);

        List<CandidateAllele> GetCandidates();
    }
}
