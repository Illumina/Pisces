using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.IO;

namespace Pisces.Calculators
{
    public class RMxNFilterSettings
    {
        public int? RMxNFilterMaxLengthRepeat { get; set; }
        public int? RMxNFilterMinRepetitions { get; set; }
        public float? RMxNFilterFrequencyLimit { get; set; }
    }
    public static class RMxNCalculator
    {
       
        public static bool ShouldFilter(CalledAllele allele, RMxNFilterSettings rMxNFilterSettings, string referenceSequence)
        {
            if (rMxNFilterSettings == null)
                return false;

            //high frequency alleles can escape this filter.
            if (allele.Frequency >= rMxNFilterSettings.RMxNFilterFrequencyLimit)
                return false;

            if (rMxNFilterSettings.RMxNFilterMaxLengthRepeat.HasValue && rMxNFilterSettings.RMxNFilterMinRepetitions.HasValue)
            {
                var maxRepeatPerComponent = RMxNCalculator.ComputeComponentRMxNLengths(allele, referenceSequence, (int)rMxNFilterSettings.RMxNFilterMaxLengthRepeat);
                if (Math.Min(maxRepeatPerComponent.Item1, maxRepeatPerComponent.Item2) >= rMxNFilterSettings.RMxNFilterMinRepetitions)
                {
                    return true;
                }
            }

            return false;
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

                    if (!Extensions.CompareSubstring(bookend, referenceBases, newBackPeekPosition)) break;

                    backPeekPosition = newBackPeekPosition;
                }

                // Read forward from first instance of motif, counting consecutive repeats
                var repeatCount = 0;
                var currentPosition = backPeekPosition;
                while (true)
                {
                    if (currentPosition + bookend.Length > referenceBases.Length) break;

                    if (!Extensions.CompareSubstring(bookend, referenceBases, currentPosition)) break;

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
        public static Tuple<int, int> ComputeComponentRMxNLengths(CalledAllele allele, string referenceBases, int maxRepeatUnitLength)
        {
            var component1 = 0;
            var component2 = int.MaxValue;

            // TODO handle complex indels (ref length != alt length and neither = 1)
            var variantBases = (
                allele.Type == AlleleCategory.Mnv || allele.Type == AlleleCategory.Snv) ? allele.AlternateAllele
                : allele.Type == AlleleCategory.Insertion ? allele.AlternateAllele.Substring(1)
                : allele.ReferenceAllele.Substring(1);

            if (allele.Type == AlleleCategory.Insertion || allele.Type == AlleleCategory.Deletion)
            {
                // Only 1 indel component for insertions or deletions
                component1 = ComputeRMxNLengthForIndel(allele.ReferencePosition, variantBases, referenceBases, maxRepeatUnitLength);
            }
            else
            {
                // Treat MNVs and SNVs as potential combination insertion-deletion events.
                // Only ever one possible deletion component
                component1 = ComputeRMxNLengthForIndel(allele.ReferencePosition - 1, allele.ReferenceAllele, referenceBases, maxRepeatUnitLength);

                // Try insertion at front or tail end of variant
                var candidateComponentInsertion1 = ComputeRMxNLengthForIndel(allele.ReferencePosition + allele.ReferenceAllele.Length - 1, variantBases, referenceBases, maxRepeatUnitLength);
                var candidateComponentInsertion2 = ComputeRMxNLengthForIndel(allele.ReferencePosition - 1, variantBases, referenceBases, maxRepeatUnitLength);
                component2 = Math.Max(candidateComponentInsertion1, candidateComponentInsertion2);
            }

            return new Tuple<int, int>(component1, component2);
        }

    }
}
