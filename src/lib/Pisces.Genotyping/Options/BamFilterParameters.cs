using Pisces.Domain.Utility;

namespace Pisces.Domain.Options
{
    public class BamFilterParameters
    {
        public int MinimumMapQuality = 1;
        public int MinimumBaseCallQuality = 20;
        public int MinNumberVariantsInRead = 1; //Scylla only (should we move this into Scylla if no ther app plans to use it?)
        public bool RemoveDuplicates = true; 
        public bool OnlyUseProperPairs = false;
        public void Validate()
        {
            ValidationHelper.VerifyRange(MinimumBaseCallQuality, 0, int.MaxValue, "MinimumBaseCallQuality");
            ValidationHelper.VerifyRange(MinimumMapQuality, 0, int.MaxValue, "MinimumMapQuality");
        }

    }

}
