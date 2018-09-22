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
        public CollapsedCoverageCalculator(bool considerAnchorInformation = false)
            : base(considerAnchorInformation)
        {
        }

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
            // TODO - we're not doing anything fancy here with the anchoring in combination with collapsed info. Just calling the base.
            base.CalculateSpanning(variant, alleleCountSource, startPointPosition, endPointPosition, anchored);
            for (var type = 0; type < Constants.NumReadCollapsedTypes; type++)
            {
                // always use start position for read collapsed read count 
                variant.ReadCollapsedCountTotal[type] += alleleCountSource.GetCollapsedReadCount(startPointPosition, (ReadCollapsedType)type);
            }
        }
    }
}
