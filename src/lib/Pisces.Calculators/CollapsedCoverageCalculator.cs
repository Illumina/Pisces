using System;
using System.Collections.Generic;
using System.Text;
using Pisces.Domain;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Calculators
{
    public class CollapsedCoverageCalculator : CoverageCalculator
    {
        protected override void CalculateSinglePoint(CalledAllele allele, IAlleleSource alleleCountSource)
        {
            base.CalculateSinglePoint(allele, alleleCountSource);
            for (var type = 0; type < Constants.NumReadCollapsedTypes; type++)
            {
                allele.ReadCollapsedCountTotal[type] += alleleCountSource.GetCollapsedReadCount(allele.ReferencePosition, (ReadCollapsedType)type);
            }
        }

        protected override void CalculateSpanning(CalledAllele variant, IAlleleSource alleleCountSource, int startPointPosition, int endPointPosition, bool anchored = true)
        {
            base.CalculateSpanning(variant, alleleCountSource, startPointPosition, endPointPosition, anchored);
            for (var type = 0; type < Constants.NumReadCollapsedTypes; type++)
            {
                // always use start position for read collapsed read count 
                variant.ReadCollapsedCountTotal[type] += alleleCountSource.GetCollapsedReadCount(startPointPosition, (ReadCollapsedType)type);
            }
        }
    }
}
