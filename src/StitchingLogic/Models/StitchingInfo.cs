using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;

namespace StitchingLogic.Models
{
    public class StitchingInfo
    {
        public CigarAlignment StitchedCigar;
        public CigarDirection StitchedDirections;
        public int InsertionAdjustment = 0;
        public int IgnoredProbePrefixBases = 0;
        public int IgnoredProbeSuffixBases = 0;

        public StitchingInfo()
        {
            StitchedCigar = new CigarAlignment();
            StitchedDirections = new CigarDirection();
        }
    }
}