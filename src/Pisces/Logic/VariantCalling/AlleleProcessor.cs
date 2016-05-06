using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pisces.Domain.Models;
﻿using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Logic.VariantCalling
{
    public static class AlleleProcessor
    {
        private const int FlankingBaseCount = 50;

        public static void Process(BaseCalledAllele allele, GenotypeModel model,
            float minFrequency, int? lowDepthFilter, int? filterVariantQscore, bool filterSingleStrandVariants, float? variantFreqFilter, float? lowGqFilter, int? indelRepeatFilter, int? rmxnFilterMaxRepeatLength, int? rmxnFilterMinRepetitions, ChrReference chrReference)
        {
            SetFractionNoCall(allele);
            ApplyFilters(allele, lowDepthFilter, filterVariantQscore, filterSingleStrandVariants, variantFreqFilter, lowGqFilter, indelRepeatFilter, rmxnFilterMaxRepeatLength, rmxnFilterMinRepetitions, chrReference);
        }

        // jg todo - set numnocalls - appears to only be applicable to SNVs
        private static void SetFractionNoCall(BaseCalledAllele allele)
        {
            var allReads = (float)(allele.TotalCoverage + allele.NumNoCalls);
            if (allReads == 0)
                allele.FractionNoCalls = 0;
            else
                allele.FractionNoCalls = allele.NumNoCalls / allReads;
        }

        private static void ApplyFilters(BaseCalledAllele allele, int? minCoverageFilter, int? variantQscoreThreshold, bool filterSingleStrandVariants, float? variantFreqFilter, float? lowGenotypeqFilter, int? indelRepeatFilter, int? rmxnFilterMaxRepeatLength, int? rmxnFilterMinRepetitions, ChrReference chrReference)
        {
            //Reset filters
            allele.Filters.Clear();

            if (indelRepeatFilter.HasValue && indelRepeatFilter > 0)
            {
                var indelRepeatLength = ComputeIndelRepeatLength(allele, chrReference.Sequence);
                if (indelRepeatFilter <= indelRepeatLength)
                    allele.AddFilter(FilterType.IndelRepeatLength);
            }

            if (rmxnFilterMaxRepeatLength.HasValue && rmxnFilterMinRepetitions.HasValue && allele is CalledVariant)
            {
                var maxRepeatPerComponent = ComputeComponentRMxNLengths((CalledVariant)allele, chrReference.Sequence, (int)rmxnFilterMaxRepeatLength);
                if (Math.Min(maxRepeatPerComponent.Item1, maxRepeatPerComponent.Item2) >= rmxnFilterMinRepetitions)
                {
                    allele.AddFilter(FilterType.RMxN);
                }
            }

            if (variantFreqFilter.HasValue && allele.Frequency < variantFreqFilter)
                allele.AddFilter(FilterType.LowVariantFrequency);

            if (minCoverageFilter.HasValue && allele.TotalCoverage < minCoverageFilter)
                allele.AddFilter(FilterType.LowDepth);

            if (variantQscoreThreshold.HasValue && allele.VariantQscore < variantQscoreThreshold)
                allele.AddFilter(FilterType.LowVariantQscore);

            if (allele is CalledVariant)
            {
                if (!allele.StrandBiasResults.BiasAcceptable ||
                (filterSingleStrandVariants && !allele.StrandBiasResults.VarPresentOnBothStrands))
                    allele.AddFilter(FilterType.StrandBias);
            }
        }

        /// <summary>
        ///     Calculates repeats of a part (front end, back end, or entirety) of an indel in the reference sequence. All possible prefix
        ///     and suffix subunits of the variant bases are calculated (up to a configured max unit length), and we then search for each unit
        ///     in the sequence directly flanking the variant position. Point of reference is the position of the base immediately preceding 
        ///     the added or deleted bases. From there, we first look backward to see if there are instances of the unit directly before the 
        ///     point of reference (inclusive). After ratcheting back to the first consecutive instance of the unit, we read through the sequence,
        ///     counting number of repeats of the unit. We return the maximum number of repeats found for any eligible unit.
        /// </summary>
        private static int ComputeRMxNLengthForIndel(int variantPosition, string variantBases, string referenceBases, int maxRepeatUnitLength)
        {
            var maxRepeatsFound = 0;
            var prefixes = new List<string>();
            var suffixes = new List<string>();
            var length = variantBases.Length;

            for (var i = length - Math.Min(maxRepeatUnitLength, length); i < length; i++)
            {
                prefixes.Add(variantBases.Substring(0, length - i));
                suffixes.Add(variantBases.Substring(i, length - i));
            }
            var bookends = prefixes.Concat(suffixes);

            foreach (var bookend in bookends)
            {
                var backPeekPosition = variantPosition;

                // Keep ratcheting backward as long as this motif is repeating
                while (true)
                {
                    var newBackPeekPosition = backPeekPosition - bookend.Length;
                    if (newBackPeekPosition < 0) break;

                    var snippetAtPeek = referenceBases.Substring(newBackPeekPosition, bookend.Length);
                    if (snippetAtPeek != bookend) break;

                    backPeekPosition = newBackPeekPosition;
                }

                // Read forward from first instance of motif, counting consecutive repeats
                var repeatCount = 0;
                var currentPosition = backPeekPosition;
                while (true)
                {
                    if (currentPosition + bookend.Length > referenceBases.Length) break;

                    var currentSnippet = referenceBases.Substring(currentPosition, bookend.Length);
                    if (currentSnippet != bookend) break;

                    repeatCount++;
                    currentPosition += bookend.Length;
                }

                if (repeatCount > maxRepeatsFound) maxRepeatsFound = repeatCount;
            }

            return maxRepeatsFound;
        }

        /// <summary>
        ///     Calculates repeats of the front, back, or entirety of a variant. For MNVs and SNVs, the variant is broken down into hypothesized
        ///     insertion/deletion events. A pair of integers is returned, and the minimum of the two is meant to be judged against the threshold
        ///     number of repetitions. For MNVs and SNVs, this represents the max repeats found in the hypothesized component deletion event and 
        ///     the larger of the max repeats found in the two hypothesized insertion events. For Insertions and Deletions, this is simply the 
        ///     max number of repeats found and an impossibly large number (as there is only one component indel event).
        /// </summary>
        private static Tuple<int,int> ComputeComponentRMxNLengths(BaseCalledAllele allele, string referenceBases, int maxRepeatUnitLength)
        {
            var component1 = 0;
            var component2 = int.MaxValue;

            // TODO handle complex indels (ref length != alt length and neither = 1)
            var variantBases = (
                allele.Type == AlleleCategory.Mnv || allele.Type == AlleleCategory.Snv) ? allele.Alternate 
                : allele.Type == AlleleCategory.Insertion ? allele.Alternate.Substring(1) 
                : allele.Reference.Substring(1);

            if (allele.Type == AlleleCategory.Insertion || allele.Type == AlleleCategory.Deletion)
            {
                // Only 1 indel component for insertions or deletions
                component1 = ComputeRMxNLengthForIndel(allele.Coordinate, variantBases, referenceBases, maxRepeatUnitLength);
            }
            else
            {
                // Treat MNVs and SNVs as potential combination insertion-deletion events.
                // Only ever one possible deletion component
                component1 = ComputeRMxNLengthForIndel(allele.Coordinate - 1, allele.Reference, referenceBases, maxRepeatUnitLength);

                // Try insertion at front or tail end of variant
                var candidateComponentInsertion1 = ComputeRMxNLengthForIndel(allele.Coordinate + allele.Reference.Length - 1, variantBases, referenceBases, maxRepeatUnitLength);
                var candidateComponentInsertion2 = ComputeRMxNLengthForIndel(allele.Coordinate - 1, variantBases, referenceBases, maxRepeatUnitLength);
                component2 = Math.Max(candidateComponentInsertion1, candidateComponentInsertion2);
            }

            return new Tuple<int, int>(component1, component2);
        }

        /// <summary>
        ///     Calculates repeats for insertions and deletions by scanning up to 50 base pairs of the chromosome reference on either side of the allele coordinate. 
        ///     Duplicates of the allele alternate found in the reference are summed to compute the overall repeat length..  Useful for filtering some
        ///     indels - e.g. we mistrust a call of AAAAAAAA versus reference AAAAAAAAA, since it may be
        ///     polymerase slippage during PCR in sample prep, rather than an actual mutation.
        /// </summary>
        private static int ComputeIndelRepeatLength(BaseCalledAllele allele, string referenceBases)
        {
            if (String.IsNullOrEmpty(referenceBases)) return 0;
            if (allele.Type != AlleleCategory.Insertion && allele.Type != AlleleCategory.Deletion && allele.Type != AlleleCategory.Snv) return 0;

            // Logic from GetFlankingBases:
            var stringPos = allele.Coordinate - 1;
            var upstreamBegin = stringPos - FlankingBaseCount;
            var upstreamEnd = stringPos - 1;
            var downstreamBegin = stringPos;
            var downstreamEnd = stringPos + FlankingBaseCount - 1;
            if (upstreamBegin < 0) upstreamBegin = 0;
            if (downstreamBegin < 0) downstreamBegin = 0;
            if (downstreamEnd > referenceBases.Length)
                downstreamEnd = referenceBases.Length - 1;
            if (upstreamEnd >= referenceBases.Length)
                upstreamEnd = referenceBases.Length - 1;
            var upstreamBases = String.Empty;
            if (upstreamEnd >= 0)
                upstreamBases =
                    referenceBases.Substring(upstreamBegin, upstreamEnd - upstreamBegin + 1).ToUpper();
            var downstreamBases =
                referenceBases.Substring(downstreamBegin, downstreamEnd - downstreamBegin + 1)
                                               .ToUpper();

            var longestRepeatLength = CheckVariantRepeatCount(allele, upstreamBases, downstreamBases);
            return longestRepeatLength;
        }

        private static int CheckVariantRepeatCount(BaseCalledAllele allele, string upstreamFlankingBases, string downstreamFlankingBases)
        {
            var currentPosition = 0;
            if(!String.IsNullOrEmpty(upstreamFlankingBases))
                currentPosition = upstreamFlankingBases.Length;
            
            var variantBases = String.Empty;

            if (allele.Type == AlleleCategory.Insertion) 
            {
                variantBases = allele.Alternate.Substring(1);
                currentPosition++;
            }

            if (allele.Type == AlleleCategory.Deletion)
            {
                variantBases = allele.Reference.Substring(1);
                currentPosition++;
            }
            
            // put together the upstream and downstream bases
            var bases = string.Format("{0}{1}", upstreamFlankingBases, downstreamFlankingBases);

            var repeatUnit = SimplifyRepeatUnit(variantBases);

            return GetRepeatLength(bases, currentPosition, repeatUnit);
        }

        /// <summary>
        ///     cleans out repeats from the repeat
        /// </summary>
        private static string SimplifyRepeatUnit(string repeatUnit)
        {
            if (repeatUnit.Length == 0) return "";
            var sb = new StringBuilder(repeatUnit.Substring(0, 1));

            for (var i = 1; i < repeatUnit.Length; i++)
            {
                var testForRepeats = repeatUnit.Split(new[] { sb.ToString() }, StringSplitOptions.None);
                var isValidRepeatUnit = (repeatUnit.Length == (testForRepeats.Length - 1) * sb.Length);
                
                if (isValidRepeatUnit) break;
                sb.Append(repeatUnit[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        ///     returns the repeat length given flanking bases and a repeat unit
        /// </summary>
        private static int GetRepeatLength(string bases, int currentPos, string repeatUnit)
        {
            var numRepeatBases = repeatUnit.Length;
            if (numRepeatBases == 0) return 0; // return 0 for nonsense input
            var lastPosition = bases.Length - numRepeatBases - 1;

            // this handles cases where a deletion is larger than the number of downstream flanking bases
            var requiredLength = currentPos + numRepeatBases + 1;
            if (requiredLength > bases.Length) return 1;

            // backtrack
            var previousPos = currentPos;
            while (currentPos > 0)
            {
                var match = true;
                for (int index = 0; index < numRepeatBases; index++)
                {
                    if (bases[currentPos + index] != repeatUnit[index])
                    {
                        match = false;
                        break;
                    }
                }

                if (!match) break;

                previousPos = currentPos;
                currentPos -= numRepeatBases;
            }

            currentPos = previousPos;

            // count the number of repeats
            int repeatLength = 0;
            while (currentPos <= lastPosition)
            {
                var match = true;
                for (int index = 0; index < numRepeatBases; index++)
                {
                    if (bases[currentPos + index] != repeatUnit[index])
                    {
                        match = false;
                        break;
                    }
                }

                if (!match) break;

                currentPos += numRepeatBases;
                repeatLength++;
            }

            return repeatLength;
        }
    }
}
