using System.Collections.Generic;

namespace Pisces.Domain.Models.Alleles
{
    /// <summary>
    /// Variants are considered equal if at the same chr/loci AND have same ref & alt allele.
    /// This forces a deterministic ordering on co-loacted variants.
    /// </summary>
    public class AlleleCompareByLociAndAllele : IComparer<CalledAllele>
    {
        public int Compare(CalledAllele x, CalledAllele y)
        {
            var lociComparer = new AlleleCompareByLoci();

            int byLoci = lociComparer.Compare(x, y);
            if (byLoci != 0)
                return byLoci;

            int refOrder = string.Compare(x.ReferenceAllele, y.ReferenceAllele);
            if (refOrder != 0)
                return refOrder;

            int altOrder = string.Compare(x.AlternateAllele, y.AlternateAllele);
            if (altOrder != 0)
                return altOrder;

            return 0;
        }
    }

}
