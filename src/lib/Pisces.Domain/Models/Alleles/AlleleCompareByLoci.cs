using System;
using System.Collections.Generic;

namespace Pisces.Domain.Models.Alleles
{
    /// <summary>
    /// Variants are considered equal if at the same chr/loci.
    /// </summary>
    public class AlleleCompareByLoci : IComparer<CalledAllele>
    {
        private readonly ChrCompare _chrCompare = new ChrCompare();
        
        public AlleleCompareByLoci(List<string> inputChrOrder = null)
        {          
            if ((inputChrOrder != null) && (inputChrOrder.Count != 0))
                _chrCompare = new ChrCompare(inputChrOrder);
        }

        public int Compare(CalledAllele x, CalledAllele y)
        {
            return OrderVariants(x, y);
        }

        public int OrderVariants(CalledAllele a, CalledAllele b)
        {
            return OrderAlleles(a, b);
        }



        public int OrderAlleles(CalledAllele a, CalledAllele b)
        {
            //return -1 if A comes first

            if ((a == null) && (b == null))
            {
                throw new ArgumentException("Allele 'a' and 'b' are null and cannot be ordered. Check the backlog is not empty.");
            }

            if (a == null)
                return 1;

            if (b == null)
                return -1;
          
            if (a.Chromosome != b.Chromosome)
            {
                return _chrCompare.Compare(a.Chromosome, b.Chromosome);
            }
                
            if (a.ReferencePosition < b.ReferencePosition) return -1;
            return a.ReferencePosition > b.ReferencePosition ? 1 : 0;
        }
    }
}
