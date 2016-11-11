using System;
using System.Linq;
using Pisces.Domain;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Calculators
{
    public class ExactCoverageCalculator : CoverageCalculator
    {
        private DirectionType[] _directionMap;
        private int[] _positionMap;

        public override void Compute(CalledAllele allele, IAlleleSource alleleCountSource)
        {
            if (allele.Type== AlleleCategory.Reference)
                CalculateSinglePoint(allele, alleleCountSource);
            else
            {
                switch (allele.Type)
                {
                    case AlleleCategory.Deletion:
                        CalculateSpanning(allele, alleleCountSource, allele.Coordinate,
                            allele.Coordinate + allele.Length + 1);
                        break;
                    case AlleleCategory.Mnv:
                        CalculateSpanning(allele, alleleCountSource, allele.Coordinate - 1,
                            allele.Coordinate + allele.Length);
                        break;
                    case AlleleCategory.Insertion:
                        CalculateSpanning(allele, alleleCountSource, allele.Coordinate, allele.Coordinate + 1, true);
                        break;
                    default:
                        CalculateSinglePoint(allele, alleleCountSource);
                        break;
                }
            }
        }

        private void CalculateSpanning(CalledAllele variant, IAlleleSource alleleCountSource, int precedingPosition,
            int trailingPosition, bool isInsertion = false)
        {
            //reset
            for (var i = 0; i < variant.EstimatedCoverageByDirection.Length; i ++)
                variant.EstimatedCoverageByDirection[i] = 0;
			for (var directionIndex = 0; directionIndex < Constants.NumDirectionTypes; directionIndex++)
			{
				foreach (var alleleType in Constants.CoverageContributingAlleles)
				{
					variant.SumOfBaseQuality += alleleCountSource.GetSumOfAlleleBaseQualities(precedingPosition, alleleType, (DirectionType)directionIndex);
					variant.SumOfBaseQuality += alleleCountSource.GetSumOfAlleleBaseQualities(trailingPosition, alleleType, (DirectionType)directionIndex);
				}
			}

			var spanningReads = alleleCountSource.GetSpanningReadSummaries(precedingPosition, trailingPosition);

            DirectionType[] directionMap = null;
            int[] positionMap = null;

            // figure out direction for each spanning read
            foreach (var spanningRead in spanningReads)
            {
                var cigarData = spanningRead.Cigar;

                // remove reads that do not span
                // leave reads that have spanning insertions at the end of the read (this is possible!)
                if ((spanningRead.ClipAdjustedEndPosition < precedingPosition || spanningRead.ClipAdjustedStartPosition > trailingPosition) ||
                    (spanningRead.ClipAdjustedEndPosition == precedingPosition && !cigarData.HasOperationAtOpIndex(0, 'I', true)) ||
                    (spanningRead.ClipAdjustedStartPosition == trailingPosition && !cigarData.HasOperationAtOpIndex(0, 'I')))
                    continue;

                var directionInfo = spanningRead.DirectionInfo;

                if (directionInfo.Directions.Count() == 1)
                {
                    variant.EstimatedCoverageByDirection[(int) directionInfo.Directions.First().Direction]++;
                }
                else
                {
                    // figure out what part of the read we are in
                    var readLength = cigarData.GetReadSpan();

                    if (_directionMap == null || _directionMap.Length != readLength)
                        _directionMap = new DirectionType[readLength];
                    if (_positionMap == null || _positionMap.Length != readLength)
                        _positionMap = new int[readLength];

                    Read.UpdateDirectionMap(directionInfo, _directionMap);
                    Read.UpdatePositionMap(spanningRead.ClipAdjustedStartPosition - (int)cigarData.GetPrefixClip(), cigarData, _positionMap, true);

                    var indexBoundaries = GetIndexBoundaries(precedingPosition, trailingPosition, _positionMap);

                    var direction = GetDirection(indexBoundaries.Item1, indexBoundaries.Item2, _directionMap);
                    variant.EstimatedCoverageByDirection[(int) direction]++;
                }
            }

            // coverage should be total across the directions.  
            variant.TotalCoverage = variant.EstimatedCoverageByDirection.Sum();
            variant.ReferenceSupport = Math.Max(0, variant.TotalCoverage - variant.AlleleSupport);
        }

        /// <summary>
        /// Looks in between preceding and trailing indices to determine direction.
        /// If any positions in between are stitched, return stitched.  If not return that direction.
        /// If there are no bases in between preceding and trailing, take stitched only if both preceding and trailing are stitched.
        /// If preceding or trailing is -1, start at end of read.
        /// </summary>
        /// <param name="precedingIndex"></param>
        /// <param name="trailingIndex"></param>
        /// <param name="directionMap"></param>
        /// <returns></returns>
        private DirectionType GetDirection(int precedingIndex, int trailingIndex, DirectionType[] directionMap)
        {
            DirectionType direction = DirectionType.Forward;

            if (precedingIndex == -1 && trailingIndex == -1)
                throw new Exception(string.Format("Invalid indices {0}-{1}", precedingIndex, trailingIndex));

            // check if indices are right next to each other
            if (trailingIndex == precedingIndex + 1)
            {
                // check if one side is -1, if so go with direction of other side
                // note other side is never at the edge of the read because that means they're not spanning and we've
                // already filtered those out
                if (precedingIndex == -1)
                    direction = directionMap[trailingIndex];
                else if (trailingIndex == -1)
                    direction = directionMap[precedingIndex];
                else
                {
                    direction = directionMap[precedingIndex];
                    if (direction == DirectionType.Stitched)
                        direction = directionMap[trailingIndex];
                }
            }
            else
            {
                if (trailingIndex == -1)
                    trailingIndex = directionMap.Length;  // go to end of read

                // if preceding index is -1, we already start at 0
                for (var i = precedingIndex + 1; i <= trailingIndex - 1; i ++)
                {
                    direction = directionMap[i];
                    if (direction == DirectionType.Stitched)
                        break;
                }
            }

            return direction;
        }

        /// <summary>
        /// Get first position less than or equal to start position, and first position greater than or equal to end position
        /// </summary>
        /// <param name="startPosition"></param>
        /// <param name="endPosition"></param>
        /// <param name="positionMap"></param>
        /// <returns></returns>
        private Tuple<int, int> GetIndexBoundaries(int startPosition, int endPosition, int[] positionMap)
        {
            int? startIndex = null;
            int? endIndex = null;

            for (var i = 0; i < positionMap.Length; i ++)
            {
                var positionAtIndex = positionMap[i];
                if (positionAtIndex >= 0 && positionAtIndex <= startPosition)
                    startIndex = i;

                if (!endIndex.HasValue && positionMap[i] >= endPosition)
                    endIndex = i;
            }

            // special case, see if we end in soft clip, e.g. 5M5D5S.  we would want end index to be first soft clipped base after start index
            if (startIndex.HasValue && !endIndex.HasValue && positionMap[positionMap.Length - 1] == -2)
            {
                for(var i = startIndex.Value + 1; i < positionMap.Length; i ++)
                    if (positionMap[i] == -2)
                    {
                        endIndex = i;
                        break;
                    }
            }

            if (endIndex.HasValue && !startIndex.HasValue && positionMap[0] == -2)
            {
                for (var i = endIndex.Value - 1; i >= 0; i--)
                    if (positionMap[i] == -2)
                    {
                        startIndex = i;
                        break;
                    }
            }

            return new Tuple<int, int>(startIndex ?? -1, endIndex ?? -1);
        }
    }
}
