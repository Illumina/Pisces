using Pisces.Domain.Types;

namespace Pisces.Domain
{
    /// <summary>
    /// Enum counts are explicitly set, because mono behaves poorly with enum.getvalues.  Make sure these are updated if adding new enums!!
    /// </summary>
    public static class Constants
    {
        public static int NumAlleleTypes
        {
            get
            {
                return _numAlleleTypes;
            }
        }

        private static int _numAlleleTypes = 6;

        public static int NumDirectionTypes
        {
            get
            {
                return _numDirectionTypes;
            }
        }
        private static int _numDirectionTypes = 3;

        public static int NumReadCollapsedTypes
        {
            get
            {
                return _numReadCollapsedTypes;
            }
        }
        private static int _numReadCollapsedTypes = 8;

        //Note, we have AlleleType (pisces.domain), SoamitcVariantType (phaser), and AlleleCatgory (VennVCf)
        //These should probably be consolidated.
        public static readonly AlleleType[] CoverageContributingAlleles =
        {
            AlleleType.A, AlleleType.C, AlleleType.G, AlleleType.T, AlleleType.Deletion
        };

        public static int NumCovContributingAlleleTypes { get { return CoverageContributingAlleles.Length; } }


        /// <summary>
        /// The max number of overlapping amplicons that might happen.
        /// We could figure this out on the fly, but its probably better (perfomance wise)
        /// just to allocate a head of time.
        /// We can throw if the user has a sample that exceeds this, and just have them turn off the amplicon bias detection feature.
        /// </summary>
        public static int MaxNumOverlappingAmplicons
        {
            get
            {
                return _maxNumOverlappingAmplicons;
            }
        }

        private static int _maxNumOverlappingAmplicons = 6;
        
    }
}
