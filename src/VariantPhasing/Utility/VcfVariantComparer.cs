using System.Collections.Generic;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models.Alleles;
using Pisces.IO;

namespace VariantPhasing.Utility
{
    public class VcfVariantComparer : IComparer<VcfVariant>
    {
        private readonly bool _chrMFirst;

        public VcfVariantComparer(bool chrMFirst = true)
        {
            _chrMFirst = chrMFirst;
        }

        public int Compare(VcfVariant x, VcfVariant y)
        {
            return Extensions.OrderVariants(x, y, _chrMFirst);
        }

        public static int OrderVariants(CalledAllele a, CalledAllele b, bool mFirst)
        {
            var vcfVariantA = new VcfVariant { ReferencePosition = a.Coordinate, ReferenceName = a.Chromosome };
            var vcfVariantB = new VcfVariant { ReferencePosition = b.Coordinate, ReferenceName = b.Chromosome };
            return Extensions.OrderVariants(vcfVariantA, vcfVariantB, mFirst);
        }

        public static int OrderVariants(CalledAllele a, VcfVariant b, bool mFirst)
        {
            var vcfVariantA = new VcfVariant { ReferencePosition = a.Coordinate, ReferenceName = a.Chromosome };
            return Extensions.OrderVariants(vcfVariantA, b, mFirst);
        }
    }

}
