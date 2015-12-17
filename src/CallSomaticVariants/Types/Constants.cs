using System.Collections.Generic;

namespace CallSomaticVariants.Types
{
    public static class Constants
    {
        public static bool DebugMode { get; set; }

        public const int NumAlleleTypes = 6;
        public const int NumDirectionTypes = 3;
        public const int RegionSize = 1000;
        public const int MaxFragmentSize = 10000; // limits window to find mate

        public static readonly AlleleType[] CoverageContributingAlleles = new []
        {
            AlleleType.A, AlleleType.C, AlleleType.G, AlleleType.T, AlleleType.Deletion, 
        };

        public static int NumCovContributingAlleleTypes { get { return CoverageContributingAlleles.Length; } }  
    }
}
