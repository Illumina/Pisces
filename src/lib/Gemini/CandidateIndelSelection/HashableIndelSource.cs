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

        public static HashableIndel GetHashableIndel(GenomeSnippet snippet, PreIndel preIndel, int contextStart, bool debug)
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

            const int maxRepeatUnitLength = 3;
            var isRepeat = StitchingLogic.OverlapEvaluator.IsRepeat(variantBases, maxRepeatUnitLength
                , out repeatUnit);

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
                        if (numMismatches <= 1)
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
                        if (numMismatches <= 1)
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
            var numBasesBeforeInsertionUnique = 0;
            if (indelType == AlleleCategory.Insertion)
            {
                var sequenceToCheckFor = isRepeat ? repeatUnit : actualAltAllele;

                for (int i = 0; i < refSuffix.Length - sequenceToCheckFor.Length; i+= sequenceToCheckFor.Length)
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
                NumApproxDupsRight = numRepeats
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
        private static string ReferenceSuffix(GenomeSnippet snippet, PreIndel preIndel, int contextStart)
        {
            var offset = Math.Max(10, 3 * preIndel.Length);
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

            foreach (var candidateIndel in indelsForChrom)
            {
                var indelIdentifier = GetHashableIndel(snippet, candidateIndel, contextStart, debug);

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

            }

            int numSkippedEffectiveSame = 0;
            var toRemove = new List<HashableIndel>();
            foreach (var indel in indelsdict.Keys.OrderByDescending(x=>x.Score))
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
                var nearbySameLengthIndels =
                    indelsdict.Keys.Where(x =>  !x.Equals(indel) && !x.InMulti && Math.Abs(indel.ReferencePosition - x.ReferencePosition) <= thresholdForNearby && 
                                                x.Type == indel.Type && x.Length == indel.Length && x.Score * 2 < indel.Score);

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
                        for (int i = 0; i < effectiveSequence.Length; i++)
                        {
                            if (effectiveSequence[i] != effectiveSequence2[i])
                            {
                                mismatches++;
                            }
                        }

                        if (debug)
                        {
                            Console.WriteLine(
                                $"{indel.StringRepresentation} ({indel.Score}) vs {nearIndel.StringRepresentation} ({nearIndel.Score})");
                            Console.WriteLine(effectiveSequence);
                            Console.WriteLine(effectiveSequence2);

                            Console.WriteLine($"Mismatches: {mismatches}");
                            Console.WriteLine();
                        }

                        if (mismatches <= 1)
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
                    foreach (var variantsAtSamePo in variantsAtSamePos)
                    {
                        if (variantsAtSamePo.Score * 2 < indel.Score && !variantsAtSamePo.HardToCall)
                        {
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