using System;
using System.Linq;
using Pisces.Domain;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Calculators
{
    public class CoverageCalculator : ICoverageCalculator
    {
        public virtual void Compute(CalledAllele allele, IAlleleSource alleleCountSource)
        {
            if (allele.Type == AlleleCategory.Reference)
                CalculateSinglePoint(allele, alleleCountSource);
            else
            {
                var variant = allele;
                switch (variant.Type)
                {
                    case AlleleCategory.Deletion:
                        CalculateSpanning(variant, alleleCountSource, variant.ReferencePosition + 1,
                            variant.ReferencePosition + variant.Length, true);
                        break;
                    case AlleleCategory.Mnv:
                        CalculateSpanning(variant, alleleCountSource, variant.ReferencePosition,
                            variant.ReferencePosition + variant.Length - 1, true);
                        break;
                    case AlleleCategory.Insertion:
                        CalculateSpanning(variant, alleleCountSource, variant.ReferencePosition, variant.ReferencePosition + 1, alleleCountSource.ExpectStitchedReads);
                        break;
                    default:
                        CalculateSinglePoint(variant, alleleCountSource);
                        break;
                }
            }
        }

        protected void CalculateSinglePoint(CalledAllele allele, IAlleleSource alleleCountSource)
        {
            //TODO: Is there a reason why we don't reallocate the stitched coverage here for point mutations? (as we do with spanning ones)
            // sum up all observations at that point

            var variant = allele as CalledAllele;

            for (var direction = 0; direction < Constants.NumDirectionTypes; direction++)
            {
                foreach(var alleleType in Constants.CoverageContributingAlleles)
                {
                    allele.EstimatedCoverageByDirection[direction] += alleleCountSource.GetAlleleCount(allele.ReferencePosition, alleleType, (DirectionType)direction);
					allele.SumOfBaseQuality += alleleCountSource.GetSumOfAlleleBaseQualities(allele.ReferencePosition, alleleType, (DirectionType)direction);

					if (alleleType != AlleleHelper.GetAlleleType(allele.ReferenceAllele)) continue;
                    if (variant != null)
                    {
                        variant.ReferenceSupport += alleleCountSource.GetAlleleCount(variant.ReferencePosition, alleleType,
                            (DirectionType) direction);
                    }
                }

                allele.TotalCoverage += allele.EstimatedCoverageByDirection[direction];

                allele.NumNoCalls += alleleCountSource.GetAlleleCount(allele.ReferencePosition, AlleleType.N, (DirectionType)direction);
            }

            // adjust for reference counts already taken up by gapped mnvs

            // note: it's possible that the ref count taken up by a gapped mnv is greater than depth at that ref position.
            // this is possible when collapsing is true, and some gapped ref positions have low quality (or are N).
            // in these cases, they get collapsed to the mnv and count towards support, but those specific alleles were never added to region's allele counts because they are low quality.
            // collapsing is the correct thing to do, so this is ok.  we should just make sure to cap at 0.
            var gappedRefCounts = alleleCountSource.GetGappedMnvRefCount(allele.ReferencePosition);

            if (allele.Type == AlleleCategory.Snv && variant != null)
            {
                variant.ReferenceSupport = Math.Max(0, variant.ReferenceSupport - gappedRefCounts);
            }
            else if (allele.Type == AlleleCategory.Reference)
            {
                allele.AlleleSupport = Math.Max(0, allele.AlleleSupport - gappedRefCounts);
            }
        }

        /// <summary>
        /// Calculation for spanning variants requires looking at two datapoints and reconciling the coverage between the two.
        /// For insertions, take min of preceeding and trailing datapoints.
        /// For deletions and mnvs, take average of first and last datapoint for variant.
        /// jg todo - figure out this old comment - (Or if we're at the edge of the world, give up and just take the coverage of the left base)
        /// </summary>
        private void CalculateSpanning(CalledAllele variant, IAlleleSource alleleCountSource, int startPointPosition, int endPointPosition, bool anchored = true)
        {
            //empty arrays to do our coverage calculations.  the three spaces are for each read direction.
            var startPointCoverage = new[] { 0, 0, 0 };
            var endPointCoverage = new[] { 0, 0, 0 };
            float exactTotalCoverage = 0f;

            // sum coverage by direction across all allele types for each data point
            for (var directionIndex = 0; directionIndex < Constants.NumDirectionTypes; directionIndex++)
            {
                foreach (var alleleType in Constants.CoverageContributingAlleles)
                {
                    startPointCoverage[directionIndex] += alleleCountSource.GetAlleleCount(startPointPosition, alleleType, (DirectionType)directionIndex);
                    endPointCoverage[directionIndex] += alleleCountSource.GetAlleleCount(endPointPosition, alleleType, (DirectionType)directionIndex);
					variant.SumOfBaseQuality += alleleCountSource.GetSumOfAlleleBaseQualities(startPointPosition, alleleType, (DirectionType)directionIndex);
					variant.SumOfBaseQuality += alleleCountSource.GetSumOfAlleleBaseQualities(endPointPosition, alleleType, (DirectionType)directionIndex);
				}
            }

            // coverage by strand direction is used for strand bias.  need to redistribute stitched contribution to forward and reverse directions for book-ends before reconciling them.
            RedistributeStitchedCoverage(startPointCoverage);
            RedistributeStitchedCoverage(endPointCoverage);

            // intentionally leave stitched coverage empty when calculating for a spanned variant (it's already been redistributed)
            for (var directionIndex = 0; directionIndex < 2; directionIndex++)
            {
                float exactCoverageForDir = anchored ? (  (startPointCoverage[directionIndex] + endPointCoverage[directionIndex])) / 2f : //will always round to lower.
                    Math.Min(startPointCoverage[directionIndex], endPointCoverage[directionIndex]);
                variant.EstimatedCoverageByDirection[directionIndex] = (int) exactCoverageForDir;

                exactTotalCoverage += exactCoverageForDir;
            }

            //for extended variants, coverage is not an exact value. 
            //Its an estimate based on the depth over the length of the variant.
            //In particular, the depth by direction does not always allocate neatly to an integer value.

            //ie, variant.TotalCoverage != variant.EstimatedCoverageByDirection[directionIndex].Sum

            variant.TotalCoverage = (int) exactTotalCoverage;
            variant.ReferenceSupport = Math.Max(0, variant.TotalCoverage - variant.AlleleSupport);
        }

        // given a coverage by direction array - redistributes stitched coverage half to forward and half to reverse
        private void RedistributeStitchedCoverage(int[] dataPoint)
        {
            var stitchedCoverage = dataPoint[(int)DirectionType.Stitched];

            dataPoint[(int) DirectionType.Forward] += (int)Math.Ceiling((float)stitchedCoverage/ 2);
            dataPoint[(int) DirectionType.Reverse] += (int)Math.Floor((float)stitchedCoverage / 2);
            dataPoint[(int) DirectionType.Stitched] = 0;
        }
    }
}
