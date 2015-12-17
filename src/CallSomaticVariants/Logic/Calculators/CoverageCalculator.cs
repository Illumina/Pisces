using System;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Logic.Calculators
{
    public static class CoverageCalculator
    {
        public static void Compute(BaseCalledAllele allele, IStateManager alleleCountSource)
        {
            if (allele is CalledReference)
                CalculateSinglePoint(allele, alleleCountSource);
            else
            {
                var variant = (CalledVariant) allele;
                switch (variant.Type)
                {
                    case AlleleCategory.Deletion:
                        CalculateSpanning(variant, alleleCountSource, variant.Coordinate + 1,
                            variant.Coordinate + variant.Length, true);
                        break;
                    case AlleleCategory.Mnv:
                        CalculateSpanning(variant, alleleCountSource, variant.Coordinate,
                            variant.Coordinate + variant.Length - 1, true);
                        break;
                    case AlleleCategory.Insertion:
                        CalculateSpanning(variant, alleleCountSource, variant.Coordinate, variant.Coordinate + 1, false);
                        break;
                    default:
                        CalculateSinglePoint(variant, alleleCountSource);
                        break;
                }
            }
        }

        private static void CalculateSinglePoint(BaseCalledAllele allele, IStateManager alleleCountSource)
        {
            
            var variant = allele as CalledVariant;

            for (var direction = 0; direction < Constants.NumDirectionTypes; direction++)
            {
                foreach(var alleleType in Constants.CoverageContributingAlleles)
                {
                    allele.TotalCoverageByDirection[direction] += alleleCountSource.GetAlleleCount(allele.Coordinate, alleleType, (DirectionType)direction);
                    
                    if (alleleType != AlleleHelper.GetAlleleType(allele.Reference)) continue;
                    if (variant != null)
                    {
                        variant.ReferenceSupport += alleleCountSource.GetAlleleCount(variant.Coordinate, alleleType,
                            (DirectionType) direction);
                    }
                }

                allele.TotalCoverage += allele.TotalCoverageByDirection[direction];

                allele.NumNoCalls += alleleCountSource.GetAlleleCount(allele.Coordinate, AlleleType.N, (DirectionType)direction);
            }

            // adjust for reference counts already taken up by gapped mnvs
            var gappedRefCounts = alleleCountSource.GetGappedMnvRefCount(allele.Coordinate);

            if (allele.Type == AlleleCategory.Snv && variant != null)
            {
                variant.ReferenceSupport -= gappedRefCounts;
            }
            else if (allele.Type == AlleleCategory.Reference)
            {
                allele.AlleleSupport -= gappedRefCounts;
            }
        }

        /// <summary>
        /// Calculation for spanning variants requires looking at two datapoints and reconciling the coverage between the two.
        /// For insertions, take min of preceeding and trailing datapoints.
        /// For deletions and mnvs, take average of first and last datapoint for variant.
        /// </summary>
        private static void CalculateSpanning(CalledVariant variant, IStateManager alleleCountSource, int startPointPosition, int endPointPosition, bool anchored = true)
        {
            //empty arrays to do our coverage calculations.  the three spaces are for each read direction.
            var startPointCoverage = new[] { 0, 0, 0 };
            var endPointCoverage = new[] { 0, 0, 0 };

            // sum coverage by direction across all allele types for each data point
            for (var directionIndex = 0; directionIndex < Constants.NumDirectionTypes; directionIndex++)
            {
                foreach (var alleleType in Constants.CoverageContributingAlleles)
                {
                    startPointCoverage[directionIndex] += alleleCountSource.GetAlleleCount(startPointPosition, alleleType, (DirectionType)directionIndex);
                    endPointCoverage[directionIndex] += alleleCountSource.GetAlleleCount(endPointPosition, alleleType, (DirectionType)directionIndex);
                }
            }

            // coverage by strand direction is used for strand bias.  need to redistribute stitched contribution to forward and reverse directions for book-ends before reconciling them.
            RedistributeStitchedCoverage(startPointCoverage);
            RedistributeStitchedCoverage(endPointCoverage);

            // intentionally leave stitched coverage empty when calculating for a spanned variant (it's already been redistributed)
            for (var directionIndex = 0; directionIndex < 2; directionIndex++)
            {
                variant.TotalCoverageByDirection[directionIndex] = anchored ? (startPointCoverage[directionIndex] + endPointCoverage[directionIndex]) /2 :
                    Math.Min(startPointCoverage[directionIndex], endPointCoverage[directionIndex]);
            }

            // coverage should be total across the directions.  
            variant.TotalCoverage = variant.TotalCoverageByDirection.Sum();
            variant.ReferenceSupport = Math.Max(0, variant.TotalCoverage - variant.AlleleSupport);
        }

        // given a coverage by direction array - redistributes stitched coverage half to forward and half to reverse
        private static void RedistributeStitchedCoverage(int[] dataPoint)
        {
            var stitchedCoverage = dataPoint[(int)DirectionType.Stitched];

            dataPoint[(int) DirectionType.Forward] += (int)Math.Ceiling((float)stitchedCoverage/ 2);
            dataPoint[(int) DirectionType.Reverse] += (int)Math.Floor((float)stitchedCoverage / 2);
            dataPoint[(int) DirectionType.Stitched] = 0;
        }
    }
}
