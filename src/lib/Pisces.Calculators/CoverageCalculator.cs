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
        private readonly bool _considerAnchorInformation;

        public CoverageCalculator(bool considerAnchorInformation = false)
        {
            _considerAnchorInformation = considerAnchorInformation;
        }

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

        protected virtual void CalculateSinglePoint(CalledAllele allele, IAlleleSource alleleCountSource)
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

                // For single point variants, for now, we're calling everything confident coverage
                allele.ConfidentCoverageStart += allele.EstimatedCoverageByDirection[direction];
                allele.ConfidentCoverageEnd += allele.EstimatedCoverageByDirection[direction];

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

        // TODO revisit using anchor info to make deletion coverage more accurate, when we have the time...
        //private void CalculateDeletionCoverage(CalledAllele variant, IAlleleSource alleleCountSource,
        //    int startPointPosition, int endPointPosition, bool presumeAnchoredForExactCov = true)
        //{
        //    var startPointCoverage = new[] { 0, 0, 0 };
        //    var endPointCoverage = new[] { 0, 0, 0 };
        //    var exactTotalCoverage = 0f;

        //    var variantLength = variant.Length;

        //    for (var directionIndex = 0; directionIndex < Constants.NumDirectionTypes; directionIndex++)
        //    {
        //        var minAnchor = variantLength + 1;
        //        var startPointCoverageForDirectionTotal = 0;
        //        var endPointCoverageForDirectionTotal = 0;
        //        foreach (var alleleType in Constants.CoverageContributingAlleles)
        //        {
        //            var startPointCoverageForDirection = alleleCountSource.GetAlleleCount(startPointPosition, alleleType, (DirectionType)directionIndex, minAnchor, symmetric: true);
        //            var endPointCoverageForDirection = alleleCountSource.GetAlleleCount(endPointPosition, alleleType, (DirectionType)directionIndex, minAnchor, fromEnd: true, symmetric: true);

        //            startPointCoverageForDirectionTotal += startPointCoverageForDirection;
        //            endPointCoverageForDirectionTotal += endPointCoverageForDirection;

        //            startPointCoverage[directionIndex] += startPointCoverageForDirection;
        //            endPointCoverage[directionIndex] += endPointCoverageForDirection;

        //            variant.SumOfBaseQuality += alleleCountSource.GetSumOfAlleleBaseQualities(startPointPosition, alleleType, (DirectionType)directionIndex, minAnchor);
        //            variant.SumOfBaseQuality += alleleCountSource.GetSumOfAlleleBaseQualities(endPointPosition, alleleType, (DirectionType)directionIndex, minAnchor, fromEnd: true);
        //        }
        //    }

        //    // coverage by strand direction is used for strand bias.  need to redistribute stitched contribution to forward and reverse directions for book-ends before reconciling them.
        //    RedistributeStitchedCoverage(startPointCoverage);
        //    RedistributeStitchedCoverage(endPointCoverage);

        //    // intentionally leave stitched coverage empty when calculating for a spanned variant (it's already been redistributed)
        //    for (var directionIndex = 0; directionIndex < 2; directionIndex++)
        //    {
        //        var exactCoverageForDir = presumeAnchoredForExactCov ? ((startPointCoverage[directionIndex] + endPointCoverage[directionIndex])) / 2f : //will always round to lower.
        //            Math.Min(startPointCoverage[directionIndex], endPointCoverage[directionIndex]);
        //        variant.EstimatedCoverageByDirection[directionIndex] = (int)exactCoverageForDir;

        //        exactTotalCoverage += exactCoverageForDir;
        //    }

        //    //for extended variants, coverage is not an exact value. 
        //    //Its an estimate based on the depth over the length of the variant.
        //    //In particular, the depth by direction does not always allocate neatly to an integer value.

        //    //ie, variant.TotalCoverage != variant.EstimatedCoverageByDirection[directionIndex].Sum

        //    variant.TotalCoverage = (int)exactTotalCoverage;
        //    variant.ReferenceSupport = Math.Max(0, variant.TotalCoverage - variant.AlleleSupport);

        //}

        /// <summary>
        /// Calculation for spanning variants requires looking at two datapoints and reconciling the coverage between the two.
        /// For insertions, take min of preceeding and trailing datapoints.
        /// For deletions and mnvs, take average of first and last datapoint for variant.
        /// jg todo - figure out this old comment - (Or if we're at the edge of the world, give up and just take the coverage of the left base)
        /// </summary>
        protected virtual void CalculateSpanning(CalledAllele variant, IAlleleSource alleleCountSource, int startPointPosition, int endPointPosition, bool presumeAnchoredForExactCov = true)
        {
            // TODO come back to this - now that we are tracking coverage more tightly we may be able to improve deletion spanning read count estimates as well
            //if (variant.Type == AlleleCategory.Deletion)
            //{
            //    CalculateDeletionCoverage(variant, alleleCountSource, startPointPosition, endPointPosition,
            //        presumeAnchoredForExactCov);
            //    return;
            //}

            //empty arrays to do our coverage calculations.  the three spaces are for each read direction.
            var startPointCoverage = new[] { 0, 0, 0 };
            var endPointCoverage = new[] { 0, 0, 0 };
            var exactTotalCoverage = 0f;

            var confidentCoverageLeft = 0;
            var confidentCoverageRight = 0;
            var suspiciousCoverageLeft = 0;
            var suspiciousCoverageRight = 0;

            var firstBase = AlleleType.N;
            var lastBase = AlleleType.N;
            var bePickyAboutAnchors = _considerAnchorInformation && variant.Type == AlleleCategory.Insertion;
            if (bePickyAboutAnchors)
            {
                var firstBaseChar = variant.AlternateAllele[1];
                firstBase = AlleleHelper.GetAlleleType(firstBaseChar);

                var lastBaseChar = variant.AlternateAllele[variant.AlternateAllele.Length - 1];
                lastBase = AlleleHelper.GetAlleleType(lastBaseChar);
            }

            var startPointCoverageUnanchored = new[] { 0, 0, 0 };
            var endPointCoverageUnanchored = new[] { 0, 0, 0 };
            var unanchoredCoverageStartQuality = 0D;
            var unanchoredCoverageEndQuality = 0D;

            var unanchoredSupport = variant.AlleleSupport - variant.WellAnchoredSupport;

            // Track the relative coverages of each and then go back and use this to determine the weighting factor

            for (var directionIndex = 0; directionIndex < Constants.NumDirectionTypes; directionIndex++)
            {
                foreach (var alleleType in Constants.CoverageContributingAlleles)
                {
                    var anchoredCoverageOnlyEnd = bePickyAboutAnchors && alleleType == firstBase;
                    var anchoredCoverageOnlyStart = bePickyAboutAnchors && alleleType == lastBase;

                    var minAnchorEnd = anchoredCoverageOnlyEnd ? variant.Length : 0;
                    var minAnchorStart = anchoredCoverageOnlyStart ? variant.Length : 0;

                    var startPointCoverageForDirection = alleleCountSource.GetAlleleCount(startPointPosition, alleleType, (DirectionType)directionIndex, minAnchorStart);
                    startPointCoverage[directionIndex] += startPointCoverageForDirection;
                    var endPointCoverageForDirection = alleleCountSource.GetAlleleCount(endPointPosition, alleleType, (DirectionType)directionIndex, minAnchorEnd, fromEnd: true);;
                    endPointCoverage[directionIndex] += endPointCoverageForDirection;

                    confidentCoverageLeft += startPointCoverageForDirection;
                    confidentCoverageRight += endPointCoverageForDirection;

                    variant.SumOfBaseQuality += alleleCountSource.GetSumOfAlleleBaseQualities(startPointPosition, alleleType, (DirectionType)directionIndex, minAnchorStart);
                    variant.SumOfBaseQuality += alleleCountSource.GetSumOfAlleleBaseQualities(endPointPosition, alleleType, (DirectionType)directionIndex, minAnchorEnd, fromEnd: true);

                    if (bePickyAboutAnchors && unanchoredSupport > 0) // Shortcut - if the unanchored support is 0 anyway, we're going to use 0 as our weight here and there's no point collecting this info
                    {
                        if (minAnchorStart > 0)
                        {
                            var unanchoredCoverageStartCount = alleleCountSource.GetAlleleCount(startPointPosition,
                                alleleType, (DirectionType) directionIndex, 0, maxAnchor: minAnchorStart - 1);
                            startPointCoverageUnanchored[directionIndex] += unanchoredCoverageStartCount;

                            suspiciousCoverageLeft += unanchoredCoverageStartCount;

                            // Need to adjust the windowed base qualities as well
                            unanchoredCoverageStartQuality += alleleCountSource.GetSumOfAlleleBaseQualities(startPointPosition, alleleType, (DirectionType)directionIndex, 0, maxAnchor: minAnchorStart - 1);
                        }

                        if (minAnchorEnd > 0)
                        {
                            var unanchoredCoverageEndCount = alleleCountSource.GetAlleleCount(endPointPosition,
                                alleleType, (DirectionType) directionIndex, 0, fromEnd: true,
                                maxAnchor: minAnchorEnd - 1);
                            endPointCoverageUnanchored[directionIndex] += unanchoredCoverageEndCount;

                            suspiciousCoverageRight += unanchoredCoverageEndCount;

                            // Need to adjust the windowed base qualities as well
                            unanchoredCoverageEndQuality += alleleCountSource.GetSumOfAlleleBaseQualities(startPointPosition, alleleType, (DirectionType)directionIndex, 0, fromEnd: true,
                                maxAnchor: minAnchorEnd - 1);
                        }
                    }

				}
            }

            if (bePickyAboutAnchors)
            {
                var trulyAnchoredCoverage = (((confidentCoverageLeft - suspiciousCoverageRight) +
                                              (confidentCoverageRight - suspiciousCoverageLeft)) / 2f);

                var anchoredVariantFreq =
                    trulyAnchoredCoverage <= 0 ? 0 : variant.WellAnchoredSupport / trulyAnchoredCoverage;

                var totalSuspiciousCoverage =
                    suspiciousCoverageLeft +
                    suspiciousCoverageRight; // Suspicious coverages are not likely to be from the same sources, so add rather than average
                var unanchoredVariantFreq = totalSuspiciousCoverage == 0
                    ? 0
                    : unanchoredSupport / ((float) totalSuspiciousCoverage);
                var variantSpecificUnanchoredWeight = Math.Max(0, anchoredVariantFreq == 0
                    ? 1
                    : Math.Min(1, unanchoredVariantFreq / anchoredVariantFreq));
                variant.UnanchoredCoverageWeight = variantSpecificUnanchoredWeight;

                for (var directionIndex = 0; directionIndex < Constants.NumDirectionTypes; directionIndex++)
                {
                    startPointCoverage[directionIndex] +=
                        (int) (startPointCoverageUnanchored[directionIndex] * variantSpecificUnanchoredWeight);
                    endPointCoverage[directionIndex] +=
                        (int) (endPointCoverageUnanchored[directionIndex] * variantSpecificUnanchoredWeight);

                    // GB: this will keep us consisent with how we were doing it before, but I find it rather odd that we're ADDING base quality from both sides and ultimately in ProcessVariant dividing that sum by the total coverage which is an average, not a sum, of each side's coverage.
                    // Since we are dividing the total q score by the tot cov i didn't want it to get inflated by reducing the tot cov, so adjusted by the same facto
                    // TJD response: Base quality is a log of a p value, so averaging them is not the same as summing them then dividing. If you have a bunch of Qscores, say 10 and 10 and 100, you DO NOT do (10+ 10+100)/3. You have to do Q10-> p 0.1 and 100 -> p 0.01 so avg(0.1,0.1,0.01) is ~ .2/3 = 0.0666 -> a Q of (what ever that ends up being)... just a computational trick, to do it in log space instead of normal space
                    variant.SumOfBaseQuality += unanchoredCoverageStartQuality * variantSpecificUnanchoredWeight;
                    variant.SumOfBaseQuality += unanchoredCoverageEndQuality * variantSpecificUnanchoredWeight;
                }
            }

            // coverage by strand direction is used for strand bias.  need to redistribute stitched contribution to forward and reverse directions for book-ends before reconciling them.
            RedistributeStitchedCoverage(startPointCoverage);
            RedistributeStitchedCoverage(endPointCoverage);

            // intentionally leave stitched coverage empty when calculating for a spanned variant (it's already been redistributed)
            for (var directionIndex = 0; directionIndex < 2; directionIndex++)
            {
                var exactCoverageForDir = presumeAnchoredForExactCov ? (  (startPointCoverage[directionIndex] + endPointCoverage[directionIndex])) / 2f : //will always round to lower.
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
            variant.SuspiciousCoverageStart = suspiciousCoverageLeft;
            variant.ConfidentCoverageStart = confidentCoverageLeft;
            variant.SuspiciousCoverageEnd = suspiciousCoverageRight;
            variant.ConfidentCoverageEnd = confidentCoverageRight;
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
