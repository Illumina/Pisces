using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using System.Collections.Generic;

namespace StitchingLogic.Models
{
    public class StitchingInfo
    {
        public CigarAlignment StitchedCigar;
        public CigarDirection StitchedDirections;
        public List<char> StitchedBases;
        public string StitchedBasesString = null;
        public List<byte> StitchedQualities;
        public int InsertionAdjustment = 0;
        public int IgnoredProbePrefixBases = 0;
        public int IgnoredProbeSuffixBases = 0;
        public bool IsSimple;
        public int NumDisagreeingBases = 0;
        public string OverlapBases;
        public int NumNDisagreements;
        public int NumAgreements;

        public string FinalStitchedBasesString
        {
            get
            {
                return StitchedBasesString ?? new string(StitchedBases.ToArray());
            }
        }

        public StitchingInfo(bool finalized = false)
        {
            if (!finalized)
            {
                StitchedCigar = new CigarAlignment();
                StitchedDirections = new CigarDirection();
                StitchedBases = new List<char>();
                StitchedQualities = new List<byte>();
            }
        }
    }
}