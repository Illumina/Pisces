using System;
using System.Collections.Generic;
using System.Linq;
using Common.IO.Utility;
using Gemini.Models;
using Gemini.Types;
using Gemini.Utility;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using ReadRealignmentLogic.Models;

namespace Gemini.CandidateIndelSelection
{
    public class HashableIndelSource : IHashableIndelSource
    {
        private readonly bool _debug;

        public HashableIndelSource(bool debug = false)
        {
            _debug = debug;
        }

        /// <summary>
        /// Given a list of raw (non-genome-contextualized) indels to realign around, returns a list of hashable, contextualized indels.
        /// </summary>
        /// <param name="chrom"></param>
        /// <param name="indelsForChrom"></param>
        /// <param name="chrReference"></param>
        /// <returns></returns>
        public List<HashableIndel> GetFinalIndelsForChromosome(string chrom, List<PreIndel> indelsForChrom, ChrReference chrReference)
        {
            return GetFinalIndelsForChromosome(indelsForChrom, chrReference, _debug);
        }

        // TODO EXTRACT TO SHARED
        private static PreIndel GetIndelKey(string splittedIndel)
        {
            var splitString = splittedIndel.Split(' ').SelectMany(x => x.Split(':', '>')).ToList();
            var chrom = splitString[0];
            var pos = int.Parse(splitString[1]);
            var refAllele = splitString[2];
            var altAllele = splitString[3];

            var indel = new PreIndel(new CandidateAllele(chrom, pos, refAllele, altAllele,
                refAllele.Length > altAllele.Length ? AlleleCategory.Deletion : AlleleCategory.Insertion)
            {
            });
            return indel;
        }

        public static HashableIndel GetHashableIndel(GenomeSnippet snippet, PreIndel preIndel, int contextStart, bool debug, bool isSpiked = false)
        {
            var actualReferenceAllele = ActualReferenceAllele(snippet, preIndel, contextStart);

            var actualAltAllele = ActualAltAllele(preIndel, actualReferenceAllele);

            var indelType = actualReferenceAllele.Length > actualAltAllele.Length
                ? AlleleCategory.Deletion
                : AlleleCategory.Insertion;

            string repeatUnit;
            var variantBases = indelType == AlleleCategory.Insertion
                ? actualAltAllele.Substring(1)
                : actualReferenceAllele.Substring(1);

            const int maxRepeatUnitLength = 2;
            var isRepeat = StitchingLogic.OverlapEvaluator.IsRepeat(variantBases, maxRepeatUnitLength, out repeatUnit);

            var isDuplication = Helper.IsDuplication(snippet.Sequence, preIndel.ReferencePosition, isRepeat, repeatUnit, actualAltAllele);

            var numRepeatsLeft = 0;
            var numRepeats = 0;

            if (indelType == AlleleCategory.Insertion && preIndel.Length > 3)
            {
                var currentPos = preIndel.ReferencePosition - snippet.StartPosition;
                while (true)
                {
                    // TODO < or <=
                    if (snippet.Sequence.Length <= currentPos + preIndel.Length)
                    {
                        break;
                    }
                    // Need to go both directions because we're allowing inexact.
                    var referenceAfterInsertion = snippet.Sequence.Substring(currentPos, preIndel.Length);

                    bool stillMatch = false;
                    if (referenceAfterInsertion != variantBases)
                    {
                        var numMismatches = Helper.GetHammingNumMismatches(referenceAfterInsertion, variantBases);
                        if (numMismatches <= Math.Max(1,preIndel.Length/6))
                        {
                            stillMatch = true;
                        }
                    }
                    else
                    {
                        stillMatch = true;
                    }

                    if (stillMatch)
                    {
                        numRepeats++;
                        currentPos += preIndel.Length;
                    }
                    else
                    {
                        break;
                    }
                }

                var currentPosLeft = preIndel.ReferencePosition - preIndel.Length - snippet.StartPosition;
                while (true)
                {
                    // Need to go both directions because we're allowing inexact.
                    if (currentPosLeft < 0)
                    {
                        break;
                    }
                    var referenceAfterInsertion = snippet.Sequence.Substring(currentPosLeft, preIndel.Length);

                    bool stillMatch = false;
                    if (referenceAfterInsertion != variantBases)
                    {
                        var numMismatches = Helper.GetHammingNumMismatches(referenceAfterInsertion, variantBases);
                        if (numMismatches <= Math.Max(1, preIndel.Length / 6))
                        {
                            stillMatch = true;
                        }
                    }
                    else
                    {
                        stillMatch = true;
                    }

                    if (stillMatch)
                    {
                        numRepeatsLeft++;
                        currentPosLeft -= preIndel.Length;
                    }
                    else
                    {
                        break;
                    }
                }

            }

            string newRepeatUnit;
            var repeats = Helper.ComputeRMxNLengthForIndel(preIndel.ReferencePosition - snippet.StartPosition, variantBases, snippet.Sequence, 6, out newRepeatUnit);
            if (repeats >= 6) // TODO make this configurable?
            {
                isRepeat = true;
                repeatUnit = newRepeatUnit;
            }

            string otherIndel = "";
            if (preIndel.InMulti)
            {
                var otherAsPre = GetIndelKey(preIndel.OtherIndel);
                otherAsPre.ReferenceAllele = ActualReferenceAllele(snippet, otherAsPre, contextStart);
                otherAsPre.AlternateAllele = ActualAltAllele(otherAsPre, otherAsPre.ReferenceAllele);
                otherIndel = Helper.CandidateToString(otherAsPre);
            }

            var length = Math.Abs(actualReferenceAllele.Length - actualAltAllele.Length);
            var isUntrustworthyInRepeatRegion = false;
            if (length == 1)
            {
                isUntrustworthyInRepeatRegion = Helper.IsInHomopolymerStretch(snippet.Sequence, preIndel.ReferencePosition);
            }

            // TODO ADD TESTS!!
            var refPrefix = ReferencePrefix(snippet, preIndel, contextStart);
            var refSuffix = ReferenceSuffix(snippet, preIndel, contextStart);

            //Read-end repeats of this repeat unit that are this length or smaller should not be trusted as insertion evidence, but larger ones can
            var numBasesBeforeInsertionUnique = NumBasesBeforeInsertionUnique(indelType, isRepeat, repeatUnit, actualAltAllele, refSuffix);
            if (numBasesBeforeInsertionUnique >= refSuffix.Length - 1)
            {
                refSuffix = ReferenceSuffix(snippet, preIndel, contextStart, 100);
                numBasesBeforeInsertionUnique = NumBasesBeforeInsertionUnique(indelType, isRepeat, repeatUnit, actualAltAllele, refSuffix);
            }


            //if (indelIdentifier.IsRepeat && indelIdentifier.Type == AlleleCategory.Insertion &&  indelIdentifier.RepeatUnit.Length >= 2)

            var indelIdentifier = new HashableIndel
            {
                Chromosome = preIndel.Chromosome,
                ReferencePosition = preIndel.ReferencePosition,
                ReferenceAllele = actualReferenceAllele,
                AlternateAllele = actualAltAllele,
                Type = indelType,
                Length = length,
                Score = preIndel.Score,
                InMulti = preIndel.InMulti,
                OtherIndel = otherIndel,
                IsRepeat = isRepeat,
                RepeatUnit = repeatUnit,
                IsDuplication = isDuplication,
                IsUntrustworthyInRepeatRegion = isUntrustworthyInRepeatRegion,
                RefPrefix = refPrefix,
                RefSuffix = refSuffix,
                NumBasesInReferenceSuffixBeforeUnique = numBasesBeforeInsertionUnique,
                NumRepeatsNearby = repeats,
                NumApproxDupsLeft = numRepeatsLeft,
                NumApproxDupsRight = numRepeats,
                IsSpiked = isSpiked,
                PossiblePartial = isRepeat && indelType == AlleleCategory.Insertion && repeatUnit.Length >= 3,
                Observations = preIndel.Observations,
                FromSoftclip = preIndel.FromSoftclip
            };

            indelIdentifier = Helper.CopyHashable(indelIdentifier, otherIndel);

            if (isDuplication && debug)
            {
                Console.WriteLine($"Found a duplication: {indelIdentifier.StringRepresentation}");
            }

            if (isRepeat && debug)
            {
                Console.WriteLine($"Found a repeat: {indelIdentifier.StringRepresentation}, {repeatUnit}");
            }

            return indelIdentifier;
        }

        private static int NumBasesBeforeInsertionUnique(AlleleCategory indelType, bool isRepeat, string repeatUnit,
            string actualAltAllele, string refSuffix)
        {
            var numBasesBeforeInsertionUnique = 0;
            if (indelType == AlleleCategory.Insertion)
            {
                var sequenceToCheckFor = isRepeat ? repeatUnit : actualAltAllele;

                for (int i = 0; i < refSuffix.Length - sequenceToCheckFor.Length; i += sequenceToCheckFor.Length)
                {
                    if (refSuffix.Substring(i, sequenceToCheckFor.Length) == sequenceToCheckFor)
                    {
                        numBasesBeforeInsertionUnique++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return numBasesBeforeInsertionUnique;
        }

        private static string ActualAltAllele(PreIndel preIndel, string actualReferenceAllele)
        {
            var actualAltAllele =
                actualReferenceAllele.Length == 1
                    ? actualReferenceAllele +
                      preIndel.AlternateAllele.Substring(1)
                    : actualReferenceAllele[0].ToString();
            return actualAltAllele;
        }

        private static string ReferencePrefix(GenomeSnippet snippet, PreIndel preIndel, int contextStart)
        {
            var offset = Math.Max(10, 3 * preIndel.Length);
            var prefixStart = Math.Max(0, preIndel.ReferencePosition - 1 - contextStart - offset - 1);
            var prefixLength = preIndel.ReferencePosition - prefixStart;
            var prefixSequence = snippet.Sequence.Substring(prefixStart, prefixLength);
            return prefixSequence;
        }
        private static string ReferenceSuffix(GenomeSnippet snippet, PreIndel preIndel, int contextStart, int minLength = 10)
        {
            var offset = Math.Max(minLength, 3 * preIndel.Length);
            var prefixSequence = snippet.Sequence.Substring(                                
                preIndel.ReferencePosition + preIndel.ReferenceAllele.Length - 1 - contextStart, offset);
            return prefixSequence;
        }

        private static string ActualReferenceAllele(GenomeSnippet snippet, PreIndel preIndel, int contextStart)
        {
            var actualReferenceAllele = snippet.Sequence.Substring(
                preIndel.ReferencePosition - 1 - contextStart, preIndel.ReferenceAllele.Length);
            return actualReferenceAllele;
        }

        private static List<HashableIndel> GetFinalIndelsForChromosome(List<PreIndel> indelsForChrom, ChrReference chrReference, bool debug)
        {
            int numSkippedWeakShortComplex = 0;
            int numRepeatLotsCompetitors = 0;

            var indelsdict = new Dictionary<HashableIndel, List<PreIndel>>();
            var chromIndelContexts = new List<HashableIndel>();

            var snippet = new GenomeSnippet
            {
                Chromosome = chrReference.Name,
                Sequence = chrReference.Sequence,
                StartPosition = 0
            };
            var contextStart = 0;

            // TODO REFACTOR OUT FILTERING, TO MATCH SPEC STRUCTURE
            var numCandidates = indelsForChrom.Count();
            // TODO consider changing how this threshold is calculated
            var medianIndelSupport = indelsForChrom.Any() ? 
                indelsForChrom.Select(x => x.Observations).OrderBy(x => x).ToList()[numCandidates / 2] : 0;
            var thresholdForUntrustworthyRepeat = medianIndelSupport / 5;

            var priorIndels = new List<PreIndel>()
            {   
                //new PreIndel(new CandidateAllele("chr14", 38210558, "C", "CGCTCTGAGCCCGGGCCACGCAGGG",
                //    AlleleCategory.Insertion)){Score = 10}
            };
            indelsForChrom.AddRange(priorIndels);
            foreach (var candidateIndel in indelsForChrom)
            {
                var additionalIndels = new List<HashableIndel>();

                var indelIdentifier = GetHashableIndel(snippet, candidateIndel, contextStart, debug);

                //if (indelIdentifier.IsRepeat && indelIdentifier.Type == AlleleCategory.Insertion &&  indelIdentifier.RepeatUnit.Length >= 2)
                //{
                //    var currentAlternate = indelIdentifier.AlternateAllele;

                //    for (int i = 0; i < 10; i++)
                //    {
                //        currentAlternate += indelIdentifier.RepeatUnit;
                //        var additionalRepeat = new PreIndel(new CandidateAllele(candidateIndel.Chromosome, candidateIndel.ReferencePosition, candidateIndel.ReferenceAllele, currentAlternate, candidateIndel.Type));
                //        var indelIdentifier2 = GetHashableIndel(snippet, additionalRepeat, contextStart, debug, true);
                //        additionalIndels.Add(indelIdentifier2);
                //    }
                //}

                if (indelIdentifier.Score == 0)
                {
                    continue;
                }
                if (indelIdentifier.IsUntrustworthyInRepeatRegion && candidateIndel.Observations < thresholdForUntrustworthyRepeat && !indelIdentifier.InMulti)
                {
                    if (debug)
                    {
                        Logger.WriteToLog(
                        $"Skipping variant {candidateIndel} because it is a weak, short variant in a complex region (Support: {candidateIndel.Observations}).");

                    }

                    numSkippedWeakShortComplex++;
                    continue;
                }

                if (!indelsdict.TryGetValue(indelIdentifier, out var indelsForIdentifier))
                {
                    indelsForIdentifier = new List<PreIndel>();
                    indelsdict.Add(indelIdentifier, indelsForIdentifier);
                }

                indelsdict[indelIdentifier].Add(candidateIndel);

                foreach (var indelIdentifer2 in additionalIndels)
                {
                    if (!indelsdict.TryGetValue(indelIdentifer2, out var indelsForIdentifier2))
                    {
                        indelsForIdentifier2 = new List<PreIndel>();
                        indelsdict.Add(indelIdentifer2, indelsForIdentifier2);
                    }

                    indelsdict[indelIdentifer2].Add(candidateIndel);

                }

            }

            int numSkippedEffectiveSame = 0;
            var toRemove = new List<HashableIndel>();
            // TODO this ordering allows the top left-aligned insertion and right-aligned deletion. I prefer left-aligning all but it seems the convention is to right-align the deletion (or is this only in cases of snp-deletion combos?).
            foreach (var indel in indelsdict.Keys.OrderByDescending(x=>x.Score).ThenBy(x=> x.Type == AlleleCategory.Insertion ? x.ReferencePosition : (x.ReferencePosition * -1)))
            {
                // Collapse neighbor deletions that have essentially the same consequence (todo should we do this with insertions too?)
                if (indel.InMulti)
                {
                    continue;
                }
                if (toRemove.Contains(indel))
                {
                    continue;
                }

                // TODO should threshold relate to num repeats nearby?
                var thresholdForNearby = 75;
                //var nearbySameLengthIndels =
                //    indelsdict.Keys.Where(x =>  !x.Equals(indel) && !x.InMulti && Math.Abs(indel.ReferencePosition - x.ReferencePosition) <= thresholdForNearby && 
                //                                x.Type == indel.Type && x.Length == indel.Length && x.Score * 2 < indel.Score);

                var nearbySameLengthIndels =
                    indelsdict.Keys.Where(x => !x.Equals(indel) && !x.InMulti && Math.Abs(indel.ReferencePosition - x.ReferencePosition) <= thresholdForNearby &&
                                               x.Type == indel.Type && x.Length == indel.Length && (x.Score * 2 < indel.Score || x.FromSoftclip));

                if (nearbySameLengthIndels.Any())
                {
                    var snipWidth = thresholdForNearby * 2;
                    var snipStart = Math.Max(indel.ReferencePosition - snipWidth - snippet.StartPosition, 0);
                    var snipEndAdjustment = indel.Type == AlleleCategory.Deletion ? indel.Length : 0;
                    var snipEnd = Math.Min(indel.ReferencePosition - snippet.StartPosition + snipWidth + snipEndAdjustment, snippet.Sequence.Length);
                    var preLength = indel.ReferencePosition - snippet.StartPosition - snipStart;
                    var postStart = snipStart + preLength + snipEndAdjustment;
                    var variantSeq = indel.Type == AlleleCategory.Deletion
                        ? ""
                        : indel.AlternateAllele.Substring(1);
                    var effectiveSequence = snippet.Sequence.Substring(snipStart, preLength) + variantSeq + snippet.Sequence.Substring(postStart, snipEnd - postStart);

                    foreach (var nearIndel in nearbySameLengthIndels)
                    {
                        var snipEndAdjustment2 = nearIndel.Type == AlleleCategory.Deletion ? nearIndel.Length : 0;
                        var preLength2 = nearIndel.ReferencePosition - snippet.StartPosition - snipStart;
                        var postStart2 = snipStart + preLength2 + snipEndAdjustment2;
                        var variantSeq2 = nearIndel.Type == AlleleCategory.Deletion
                            ? ""
                            : nearIndel.AlternateAllele.Substring(1);
                        var effectiveSequence2 = snippet.Sequence.Substring(snipStart, preLength2) + variantSeq2 + snippet.Sequence.Substring(postStart2, snipEnd - postStart2);
                        var mismatches = 0;
                        var mismatchStrings = new List<string>();
                        for (int i = 0; i < effectiveSequence.Length; i++)
                        {
                            if (effectiveSequence[i] != effectiveSequence2[i])
                            {
                                mismatches++;
                                mismatchStrings.Add($"{effectiveSequence[i]}{effectiveSequence2[i]}");
                            }
                        }

                        if (debug)
                        {
                            Console.WriteLine(
                                $"{indel.StringRepresentation} ({indel.Score}) vs {nearIndel.StringRepresentation} ({nearIndel.Score})");
                            Console.WriteLine(effectiveSequence);
                            Console.WriteLine(effectiveSequence2);

                            Console.WriteLine($"Mismatches: {mismatches}");
                            Console.WriteLine(string.Join(",",mismatchStrings));
                            Console.WriteLine();
                        }

                        var mismatchStringsAreSwap =
                            mismatches == 2 && mismatchStrings[0][0] == mismatchStrings[1][1] &&
                            mismatchStrings[0][1] == mismatchStrings[1][0];
                        // If there are 0-1 mismatches, or if there is a swap of the same bases, these are probably the same.
                        if (mismatches <= 1 || (mismatches ==2 && mismatchStringsAreSwap))
                        {
                            numSkippedEffectiveSame++;

                            if (debug)
                            {
                                Logger.WriteToLog(
                                $"Removing {nearIndel.StringRepresentation} ({nearIndel.Score}) from contention as its consequence is extremely similar to {indel.StringRepresentation} ({indel.Score})");
                            }
                            toRemove.Add(nearIndel);
                            // TODO do we want to add the score from the removed indels to the kept one?
                        }
                    }
                }
            }

            foreach (var removeIndel in toRemove.Distinct())
            {
                indelsdict.Remove(removeIndel);
            }

            toRemove.Clear();

            foreach (var indel in indelsdict.Keys)
            {
                if (indel.InMulti)
                {
                    continue;
                }
                if (toRemove.Contains(indel))
                {
                    continue;
                }

                var variantsAtSamePos =
                    indelsdict.Keys.Where(x => x.ReferencePosition == indel.ReferencePosition && x.Type == indel.Type && 
                                               !x.Equals(indel) && !x.InMulti).ToList();

                var numVariantsAtSamePos = variantsAtSamePos.Count();
                var variantsRemovedFromSamePos = 0;
                if (numVariantsAtSamePos > 0)
                {
                    //if (indel.IsRepeat && indel.Type == AlleleCategory.Insertion && indel.RepeatUnit.Length >= 2 && !indel.IsSpiked)
                    //{
                    //    //Console.WriteLine($"Skipping: {indel}");
                    //    continue;
                    //}

                    //Console.WriteLine($"{numVariantsAtSamePos} at {indel.StringRepresentation} ({string.Join(",",variantsAtSamePos.Select(x=>x.StringRepresentation))})");

                    foreach (var variantsAtSamePo in variantsAtSamePos)
                    {
                        var otherIsSubstring = (indel.AlternateAllele.Length >= variantsAtSamePo.AlternateAllele.Length && indel.AlternateAllele.Substring(0,variantsAtSamePo.AlternateAllele.Length) == variantsAtSamePo.AlternateAllele);
                        var otherIsWeak = (variantsAtSamePo.Observations <= 2 && variantsAtSamePo.Observations <= indel.Observations);
                        var otherVariantIsWeakSubstringOfThisInsertion = indel.Type == AlleleCategory.Insertion && (otherIsWeak && otherIsSubstring);
                        //Console.WriteLine($"{variantsAtSamePo.StringRepresentation}, {otherIsSubstring},{otherIsWeak},{otherVariantIsWeakSubstringOfThisInsertion}");
                        if (((variantsAtSamePo.Score * 2 < indel.Score && variantsAtSamePo.Observations * 2 < indel.Observations) || otherVariantIsWeakSubstringOfThisInsertion) && !variantsAtSamePo.HardToCall)
                        {
                            //Console.WriteLine($"Removing {variantsAtSamePo.StringRepresentation} for being at same pos ({variantsAtSamePo.Score} vs {indel.Score}, {otherIsSubstring}, {otherIsWeak}) ");
                            toRemove.Add(variantsAtSamePo);
                            variantsRemovedFromSamePos++;
                        }
                    }

                    if (numVariantsAtSamePos - variantsRemovedFromSamePos > 2)
                    {
                        toRemove.Add(indel);
                        toRemove.AddRange(variantsAtSamePos);

                        if (debug)
                        {
                            Logger.WriteToLog(
                            $"Skipping variant {indel.StringRepresentation} ({indel.Score}) and {numVariantsAtSamePos} competitors because it's a repeat with lots of competitors and there is no clear strong candidate ({(string.Join(",", variantsAtSamePos.Select(x => x.Score)))}).");}
                    }
                    else
                    {
                        // Note that this could be an issue if there are somatic indels at the same position as germline indels

                        if (debug)
                        {
                            Logger.WriteToLog(
                            $"Removing {variantsRemovedFromSamePos} of {numVariantsAtSamePos} variants at same position as {indel.StringRepresentation} ({indel.Score}) ({(string.Join(",", variantsAtSamePos.Select(x => x.Score)))}).");}
                    }
                }


            }

            foreach (var removeIndel in toRemove.Distinct())
            {
                if (removeIndel.IsSpiked)
                {
                    continue;
                }

                indelsdict.Remove(removeIndel);
            }

            if (debug)
            {
                Logger.WriteToLog(
                    $"Skipped {numRepeatLotsCompetitors} for being a repeat with lots of competitors and there is no clear strong candidate.");
                Logger.WriteToLog(
                    $"Skipped {numSkippedWeakShortComplex} for being a weak, short variant in a complex region.");
                Logger.WriteToLog(
                    $"Skipped {numSkippedEffectiveSame} for being effectively the same as a much stronger variant.");
            }

            chromIndelContexts = indelsdict.Keys.ToList();

            return chromIndelContexts;
        }

    }
}