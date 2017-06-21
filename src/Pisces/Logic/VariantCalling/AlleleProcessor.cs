using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pisces.Calculators;
using Pisces.Domain.Models;
﻿using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Logic.VariantCalling
{
    public static class AlleleProcessor
    {
        private const int FlankingBaseCount = 50;

        public static void Process(CalledAllele allele, 
            float minFrequency, int? lowDepthFilter, int? filterVariantQscore, bool filterSingleStrandVariants, float? variantFreqFilter, float? lowGqFilter, int? indelRepeatFilter, RMxNFilterSettings rMxNFilterSettings, ChrReference chrReference, bool isStitchedSource = false)
        {
            allele.SetFractionNoCalls();
            ApplyFilters(allele, lowDepthFilter, filterVariantQscore, filterSingleStrandVariants, variantFreqFilter, lowGqFilter, indelRepeatFilter, rMxNFilterSettings, isStitchedSource, chrReference);
        }

       
        private static void ApplyFilters(CalledAllele allele, int? minCoverageFilter, int? variantQscoreThreshold, bool filterSingleStrandVariants, float? variantFreqFilter, float? lowGenotypeqFilter, int? indelRepeatFilter,
            RMxNFilterSettings rMxNFilterSettings, bool hasStitchedSource, ChrReference chrReference)
        {
            //Reset filters
            allele.Filters.Clear();

            if (minCoverageFilter.HasValue && allele.TotalCoverage < minCoverageFilter)
                allele.AddFilter(FilterType.LowDepth);

            if (variantQscoreThreshold.HasValue && allele.VariantQscore < variantQscoreThreshold  && (allele.TotalCoverage != 0))
            {
                //note we wont flag it for Qscore, if its got zero depth, because in that case, the Q score calc was not made anyway.
                allele.AddFilter(FilterType.LowVariantQscore);
            }
            if (allele.Type != AlleleCategory.Reference)
            {
                if (!allele.StrandBiasResults.BiasAcceptable ||
                (filterSingleStrandVariants && !allele.StrandBiasResults.VarPresentOnBothStrands))
                    allele.AddFilter(FilterType.StrandBias);

                if (indelRepeatFilter.HasValue && indelRepeatFilter > 0)
                {
                    var indelRepeatLength = ComputeIndelRepeatLength(allele, chrReference.Sequence);
                    if (indelRepeatFilter <= indelRepeatLength)
                        allele.AddFilter(FilterType.IndelRepeatLength);
                }

                if (RMxNCalculator.ShouldFilter(allele, rMxNFilterSettings, chrReference.Sequence))
                    allele.AddFilter(FilterType.RMxN);

                if (variantFreqFilter.HasValue && allele.Frequency < variantFreqFilter)
                    allele.AddFilter(FilterType.LowVariantFrequency);

                if (hasStitchedSource) //can only happen for insertions and MNVs
                {
                    if (allele.AlternateAllele.Contains("N"))
                        allele.AddFilter(FilterType.StrandBias);
                }
            }
        }

     
        /// <summary>
        ///     Calculates repeats for insertions and deletions by scanning up to 50 base pairs of the chromosome reference on either side of the allele coordinate. 
        ///     Duplicates of the allele alternate found in the reference are summed to compute the overall repeat length..  Useful for filtering some
        ///     indels - e.g. we mistrust a call of AAAAAAAA versus reference AAAAAAAAA, since it may be
        ///     polymerase slippage during PCR in sample prep, rather than an actual mutation.
        /// </summary>
        private static int ComputeIndelRepeatLength(CalledAllele allele, string referenceBases)
        {
            if (String.IsNullOrEmpty(referenceBases)) return 0;
            if (allele.Type != AlleleCategory.Insertion && allele.Type != AlleleCategory.Deletion && allele.Type != AlleleCategory.Snv) return 0;

            // Logic from GetFlankingBases:
            var stringPos = allele.ReferencePosition - 1;
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

        private static int CheckVariantRepeatCount(CalledAllele allele, string upstreamFlankingBases, string downstreamFlankingBases)
        {
            var currentPosition = 0;
            if(!String.IsNullOrEmpty(upstreamFlankingBases))
                currentPosition = upstreamFlankingBases.Length;
            
            var variantBases = String.Empty;

            if (allele.Type == AlleleCategory.Insertion) 
            {
                variantBases = allele.AlternateAllele.Substring(1);
                currentPosition++;
            }

            if (allele.Type == AlleleCategory.Deletion)
            {
                variantBases = allele.ReferenceAllele.Substring(1);
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
