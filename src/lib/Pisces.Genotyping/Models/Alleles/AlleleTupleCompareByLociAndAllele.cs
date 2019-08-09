using System;
using System.Collections.Generic;
using System.Text;

namespace Pisces.Domain.Models.Alleles
{
    /// <summary>
    /// Variants are considered equal if at the same chr/loci AND have same ref & alt allele.
    /// This forces a deterministic ordering on co-located variants.
    /// </summary>
    public class AlleleTupleCompareByLociAndAllele : IComparer<Tuple<CalledAllele, string>>
    {
        public int Compare(Tuple<CalledAllele, string> x, Tuple<CalledAllele, string> y)
        {
            var lociAndAlleleComparer = new AlleleCompareByLociAndAllele();
            
            int byLociAndAllele = lociAndAlleleComparer.Compare(x.Item1, y.Item1);
            if (byLociAndAllele != 0)
                return byLociAndAllele;

            return 0;
        }
    }
}
