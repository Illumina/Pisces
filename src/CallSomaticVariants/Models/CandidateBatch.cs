using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic.RegionState;
using CallSomaticVariants.Models.Alleles;

namespace CallSomaticVariants.Models
{
    public class CandidateBatch : ICandidateBatch
    {
        private readonly List<CandidateAllele> _candidates = new List<CandidateAllele>();

        public int? MaxClearedPosition { get; set; }
        public List<Region> ClearedRegions { get; set; }  // specifies which blocks went into the batch
        public List<int> BlockKeys { get; set; } // keys for which blocks went into the batch

        public bool HasCandidates { get { return _candidates.Any(); } }

        public void Add(List<CandidateAllele> candidates)
        {
            foreach (var candidate in candidates)
            {
                Add(candidate);
            }
        }

        public void Add(CandidateAllele candidate)
        {
            _candidates.Add(candidate);
        }

        public List<CandidateAllele> GetCandidates()
        {
            return _candidates;
        }

        public CandidateBatch()
        {
            BlockKeys = new List<int>();
        }

        public CandidateBatch(List<CandidateAllele> candidates)
        {
            BlockKeys = new List<int>();
            Add(candidates);
        }
    }
}
