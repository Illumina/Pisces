using System;
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
        private static int _numReadCollapsedTypes = 4;

        public static readonly AlleleType[] CoverageContributingAlleles = 
        {
            AlleleType.A, AlleleType.C, AlleleType.G, AlleleType.T, AlleleType.Deletion 
        };

        public static int NumCovContributingAlleleTypes { get { return CoverageContributingAlleles.Length; } }  
    }
}
