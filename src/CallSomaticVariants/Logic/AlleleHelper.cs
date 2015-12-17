using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Logic
{
    public static class AlleleHelper
    {
        public static AlleleType GetAlleleType(string alleleString)
        {
            return GetAlleleType(Convert.ToChar(alleleString));
        }
        public static AlleleType GetAlleleType(char alleleChar)
        {
            switch (alleleChar)
            {
                case 'A':
                    return AlleleType.A;
                case 'C':
                    return AlleleType.C;
                case 'G':
                    return AlleleType.G;
                case 'T':
                    return AlleleType.T;
                case 'N':
                    return AlleleType.N;
                default:  
                    //tjd+
                    //be kinder to unknown bases.
                    return AlleleType.N; 
                    //throw new ArgumentException(string.Format("Unrecognized allele '{0}'.", alleleChar));
            }
        }
    }
}
