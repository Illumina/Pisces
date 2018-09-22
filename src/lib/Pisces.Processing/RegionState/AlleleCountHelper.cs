using System;

namespace Pisces.Processing.RegionState
{
    public static class AlleleCountHelper
    {
        /// <summary>
        /// Get allele count for a given allele count array based on the provided anchoring settings, position, allele type, and direction type. If no maxAnchor is provided, it is assumed that anything from the minAnchor and beyond is to be counted toward coverage. If a maxAnchor is provided, it is assumed that the caller is looking for residual coverage and already has the well-anchored (and other-side) coverage. Therefore it internally caps the maxAnchor to not reach the well-anchored coverage.
        /// </summary>
        /// <param name="minAnchor"></param>
        /// <param name="fromEnd"></param>
        /// <param name="wellAnchoredIndex"></param>
        /// <param name="numAnchorIndexes"></param>
        /// <param name="alleleCounts"></param>
        /// <param name="positionIndex"></param>
        /// <param name="alleleTypeIndex"></param>
        /// <param name="directionTypeIndex"></param>
        /// <param name="numAnchorTypes"></param>
        /// <param name="maxAnchor"></param>
        /// <returns></returns>
        public static int GetAnchorAdjustedAlleleCount(int minAnchor, bool fromEnd, int wellAnchoredIndex, int numAnchorIndexes,
            int[,,,] alleleCounts, int positionIndex, int alleleTypeIndex, int directionTypeIndex, int numAnchorTypes,
            int? maxAnchor, bool symmetric = false)
        {
            var trueMinAnchor = Math.Min(wellAnchoredIndex, minAnchor);
            int initialMaxAnchor = wellAnchoredIndex;
            if (maxAnchor != null)
            {
                if (maxAnchor >= wellAnchoredIndex)
                {
                    initialMaxAnchor = wellAnchoredIndex - 1;
                }

                if (maxAnchor < wellAnchoredIndex)
                {
                    initialMaxAnchor = maxAnchor.Value;
                }
            }

            // Add coverage for all anchor types that are >= the min anchor
            var totCount = 0;
            if (fromEnd)
            {
                for (int i = trueMinAnchor; i <= initialMaxAnchor; i++)
                {
                    var anchorIndex = numAnchorIndexes - i - 1;
                    var countForAnchorType = alleleCounts[positionIndex, alleleTypeIndex,
                        directionTypeIndex, anchorIndex];
                    totCount += countForAnchorType;
                }

                if (maxAnchor == null)
                {
                    // Everything on the other side should be safe
                    for (int i = symmetric ? trueMinAnchor : 0; i < initialMaxAnchor; i++)
                    {
                        var countForAnchorType = alleleCounts[positionIndex, alleleTypeIndex,
                            directionTypeIndex, i];
                        totCount += countForAnchorType;
                    }
                }
            }
            else
            {
                for (int i = trueMinAnchor; i <= initialMaxAnchor; i++)
                {
                    var countForAnchorType = alleleCounts[positionIndex, alleleTypeIndex,
                        directionTypeIndex, i];
                    totCount += countForAnchorType;
                }

                if (maxAnchor == null)
                {
                    // Everything on the other side should be safe
                    for (int i = initialMaxAnchor + 1; i < (symmetric ? numAnchorIndexes - trueMinAnchor : numAnchorIndexes); i++)
                    {
                        var countForAnchorType = alleleCounts[positionIndex, alleleTypeIndex,
                            directionTypeIndex, i];
                        totCount += countForAnchorType;
                    }
                }
            }

            return totCount;
        }

        // TODO monumentally annoyed that I had to duplicate this code. But the underlying structures are of different types. Should really consolidate. As soon as timeline isn't so ungodly tight.
        /// <summary>
        /// Get quality for a given allele count array based on the provided anchoring settings, position, allele type, and direction type. If no maxAnchor is provided, it is assumed that anything from the minAnchor and beyond is to be counted toward coverage. If a maxAnchor is provided, it is assumed that the caller is looking for residual coverage and already has the well-anchored (and other-side) coverage. Therefore it internally caps the maxAnchor to not reach the well-anchored coverage.
        /// </summary>
        /// <param name="minAnchor"></param>
        /// <param name="fromEnd"></param>
        /// <param name="wellAnchoredIndex"></param>
        /// <param name="numAnchorIndexes"></param>
        /// <param name="alleleCounts"></param>
        /// <param name="positionIndex"></param>
        /// <param name="alleleTypeIndex"></param>
        /// <param name="directionTypeIndex"></param>
        /// <param name="numAnchorTypes"></param>
        /// <param name="maxAnchor"></param>
        /// <returns></returns>
        public static double GetAnchorAdjustedTotalQuality(int minAnchor, bool fromEnd, int wellAnchoredIndex, int numAnchorIndexes,
            double[,,,] alleleCounts, int positionIndex, int alleleTypeIndex, int directionTypeIndex, int numAnchorTypes,
            int? maxAnchor, bool symmetric = false)
        {
            var trueMinAnchor = Math.Min(wellAnchoredIndex, minAnchor);
            int initialMaxAnchor = wellAnchoredIndex;
            if (maxAnchor != null)
            {
                if (maxAnchor >= wellAnchoredIndex)
                {
                    initialMaxAnchor = wellAnchoredIndex - 1;
                }

                if (maxAnchor < wellAnchoredIndex)
                {
                    initialMaxAnchor = maxAnchor.Value;
                }
            }

            // Add coverage for all anchor types that are >= the min anchor
            var totCount = 0D;
            if (fromEnd)
            {
                for (int i = trueMinAnchor; i <= initialMaxAnchor; i++)
                {
                    var anchorIndex = numAnchorIndexes - i - 1;
                    var countForAnchorType = alleleCounts[positionIndex, alleleTypeIndex,
                        directionTypeIndex, anchorIndex];
                    totCount += countForAnchorType;
                }

                if (maxAnchor == null)
                {
                    // Everything on the other side should be safe
                    for (int i = symmetric ? trueMinAnchor : 0; i < initialMaxAnchor; i++)
                    {
                        var countForAnchorType = alleleCounts[positionIndex, alleleTypeIndex,
                            directionTypeIndex, i];
                        totCount += countForAnchorType;
                    }
                }
            }
            else
            {
                for (int i = trueMinAnchor; i <= initialMaxAnchor; i++)
                {
                    var countForAnchorType = alleleCounts[positionIndex, alleleTypeIndex,
                        directionTypeIndex, i];
                    totCount += countForAnchorType;
                }

                if (maxAnchor == null)
                {
                    // Everything on the other side should be safe
                    for (int i = initialMaxAnchor + 1; i < (symmetric ? numAnchorIndexes - trueMinAnchor : numAnchorIndexes); i++)
                    {
                        var countForAnchorType = alleleCounts[positionIndex, alleleTypeIndex,
                            directionTypeIndex, i];
                        totCount += countForAnchorType;
                    }
                }
            }

            return totCount;
        }
    }
}