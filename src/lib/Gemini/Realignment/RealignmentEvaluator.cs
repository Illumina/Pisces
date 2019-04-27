using Alignment.Domain.Sequencing;
using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;
using Gemini.Models;
using Gemini.Types;
using Gemini.Utility;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using System.Collections.Generic;
using System.Linq;
using Extensions = ReadRealignmentLogic.Utlity.Extensions;

namespace Gemini.Realignment
{
    public class RealignmentEvaluator : IRealignmentEvaluator
    {
        private readonly char[] _suspectCigarOps = { 'S', 'I', 'D', 'X' };
        private readonly string _chromosome;

        private readonly IChromosomeIndelSource _indelSource;
        private readonly IStatusHandler _statusCounter;
        private readonly IReadRealigner _readRealigner;
        private readonly IRealignmentJudger _judger;
        private readonly bool _trackActualMismatches;
        private readonly bool _checkSoftclipsForMismatches;
        private readonly Dictionary<HashableIndel, int[]> _indelOutcomes = new Dictionary<HashableIndel, int[]>();
        private readonly bool _allowRescoringOrig0;
        private readonly bool _softclipUnknownIndels;
        private readonly IRegionFilterer _regionFilterer;
        private bool _lightDebug;

        public RealignmentEvaluator(IChromosomeIndelSource indelSource, IStatusHandler statusCounter,
            IReadRealigner readRealinger, IRealignmentJudger judger, string chromosome, bool trackActualMismatches, 
            bool checkSoftclipsForMismatches, bool allowRescoringOrig0, bool softclipUnknownIndels, IRegionFilterer regionFilterer, 
            bool lightDebug)
        {
            _indelSource = indelSource;
            _statusCounter = statusCounter;
            _readRealigner = readRealinger;
            _judger = judger;
            _chromosome = chromosome;
            _trackActualMismatches = trackActualMismatches;
            _checkSoftclipsForMismatches = checkSoftclipsForMismatches;
            _allowRescoringOrig0 = allowRescoringOrig0;
            _softclipUnknownIndels = softclipUnknownIndels;
            _regionFilterer = regionFilterer;
            _lightDebug = lightDebug;
        }

        public BamAlignment GetFinalAlignment(BamAlignment origBamAlignment, out bool changed, out bool forcedSoftclip, out bool confirmed, out bool sketchy,
         List<PreIndel> selectedIndels = null, List<PreIndel> existingIndels = null,
            bool assumeImperfect = true, List<HashableIndel> confirmedAccepteds = null, List<PreIndel> mateIndels = null)
        {
            sketchy = false;
            forcedSoftclip = false;
            bool forcedAlignment = false;
            var presumeStartPositionForForcedAlignment = 0;

            if (origBamAlignment.CigarData.Count == 0)
            {
                // This was something weird that came up in the halo dataset... mapq is 0 but is still mapped, no cigar

                if (origBamAlignment.Position <= 0 && origBamAlignment.FragmentLength != 0) // No sense trying to fiddle with the position otherwise
                {
                    // TODO does this really even move the needle? Is it helping enough to outweigh its weirdness? 
                    var presumedEndPosition = origBamAlignment.MatePosition < origBamAlignment.Position
                        ? origBamAlignment.MatePosition - origBamAlignment.FragmentLength
                        : origBamAlignment.MatePosition + origBamAlignment.FragmentLength;
                    presumeStartPositionForForcedAlignment = presumedEndPosition - origBamAlignment.Bases.Length;
                    forcedAlignment = true;
                }
                else
                {
                    presumeStartPositionForForcedAlignment = origBamAlignment.Position;
                    forcedAlignment = true;
                }

            }

            var anyIndelsAtAll = _regionFilterer.AnyIndelsNearby(origBamAlignment.Position);
            bool isRealignable = true;
            if (anyIndelsAtAll)
            {
                var isImperfectRead = false || ((origBamAlignment.ContainsDisallowedCigarOps(_suspectCigarOps) ||
                                   origBamAlignment.GetIntTag("NM") > 0 || forcedAlignment));
                var isReadWorthCaringAbout = !origBamAlignment.IsDuplicate() && !origBamAlignment.IsSecondary();
                isRealignable = isImperfectRead && isReadWorthCaringAbout && origBamAlignment.Bases.Distinct().Count() > 1;
            }
            else
            {
                _statusCounter.AddStatusCount("No indels nearby at all");
                isRealignable = false;
            }

            if (!isRealignable)
            {
                confirmed = false;
                changed = false;
                sketchy = false;
                return origBamAlignment;
            }

            // TODO maybe flag (or return all) if there's a lot or high quality stuff that we're missing! Esp with pair specific
            var indels = _indelSource.GetRelevantIndels(forcedAlignment ? presumeStartPositionForForcedAlignment : origBamAlignment.Position, 
                mateIndels, confirmedAccepteds);

            // Don't realign around single indels if we already have them
            bool hasExistingUnsanctionedIndels = false;
            bool existingSanctionedIndelIsBest = false;
            bool hasVeryGoodIndel = false;
            bool hasHardToCallIndel = false;
            var existingMatches = new List<PreIndel>();
            HashableIndel existingConfirmedIndel = new HashableIndel();
            var existingMatchHashables = new List<HashableIndel>();

            if (indels.Any() && existingIndels != null && existingIndels.Any())
            {
                var topScore = (float)(indels.Max(x => x.Key.Score));
                var matchesFound = 0;
                var nonPreExistingIndels = new List<KeyValuePair<HashableIndel,GenomeSnippet>>();

                var index = 0;
                foreach (var kvp in indels)
                {
                    var indel = kvp.Key;
                    var matches = existingIndels.Where(e => Helper.IsMatch(e, indel));
                    var isMatch = matches.Any();
                    if (isMatch)
                    {
                        matchesFound++;

                        if (!indel.InMulti && index == 0)
                        {
                            existingSanctionedIndelIsBest = true;
                            existingConfirmedIndel = indel;
                        }

                        var proportionOfTopScore = indel.Score / (float) topScore;
                        if (proportionOfTopScore >= 0.75)
                        {
                            hasVeryGoodIndel = true;
                        }

                        if (indel.HardToCall)
                        {
                            hasHardToCallIndel = true;
                        }

                        existingMatches.AddRange(matches);

                        // TODO do we need special handling of multis?
                        existingMatchHashables.Add(indel);
                    }

                    if (!isMatch || indel.InMulti)
                    {
                        nonPreExistingIndels.Add(kvp);
                    }

                    
                    index++;
                }

                // TODO do we actually want to replace indels with non-pre-existing only? 
                indels = nonPreExistingIndels;

                if (matchesFound == 0)
                {
                    hasExistingUnsanctionedIndels = true;
                }
            }

            // TODO this precludes us from having good multis
            if (existingSanctionedIndelIsBest)
            {
                // If it already had the top ranked indel, there's not really any point in trying to realign around others (here we assume that it's also the best fitting indel for the read, hence why it was originally called by the regular aligner). 
                _statusCounter.AddStatusCount("Existing indel is already the best available");
                changed = false;
                confirmed = true;

                UpdateOutcomeForConfirmed(existingConfirmedIndel);

                if (confirmedAccepteds == null)
                {
                    confirmedAccepteds = new List<HashableIndel>();
                }

                confirmedAccepteds.Add(existingConfirmedIndel);

                return origBamAlignment;
            }


            if (!indels.Any() || origBamAlignment.EndPosition - origBamAlignment.Position > 500)
            {
                if (!indels.Any())
                {
                    // TODO maybe do the forced softclip here if the read did have indels?
                    _statusCounter.AddStatusCount("No indels to realign to");
                    _statusCounter.AppendStatusStringTag("RX", $"{origBamAlignment.GetStringTag("RX")},No indels to realign to", origBamAlignment);
                }
                else
                {
                    _statusCounter.AddStatusCount("Alignment reference span longer than we can realign to");
                }
                changed = false;
                confirmed = false;
                return origBamAlignment;
            }




            // TODO this should relate to cap on indel size... introducing too large of an indel will make us go beyond this context.
            var context = indels.First().Value;
            var orderedIndels = indels.Select(x => x.Key).ToList();
            var numIndels = orderedIndels.Count;

            _statusCounter.AddStatusCount("Realigning to " + numIndels);

            var bamAlignment = new BamAlignment(origBamAlignment);
            if (forcedAlignment)
            {
                bamAlignment.CigarData = new CigarAlignment(origBamAlignment.Bases.Length + "M");
                bamAlignment.Position = presumeStartPositionForForcedAlignment;
            }

            var realignResult = _readRealigner.Realign(new Read(_chromosome, bamAlignment),
                orderedIndels, indels.ToDictionary(x => x.Key, x => x.Value), confirmedAccepteds != null && confirmedAccepteds.Any());

            var acceptedIndels = realignResult?.AcceptedIndels;
            var hasAnyIndels = acceptedIndels != null && acceptedIndels.Any();

            if (realignResult != null)
            {
                _statusCounter.AddStatusCount("Able to realign at all (may still be worse than original)");
                _statusCounter.AppendStatusStringTag("RX", "Able to realign at all(may still be worse than original)", bamAlignment);
            }
            else
            {
                _statusCounter.AddStatusCount("Not able to realign at all");
                _statusCounter.AppendStatusStringTag("RX", "Not able to realign at all", origBamAlignment);
            }

            AlignmentSummary originalAlignmentSummary = null;
            var realignmentUnchanged = true;
            if (realignResult != null)
            {
                originalAlignmentSummary =
                    Extensions.GetAlignmentSummary((new Read(_chromosome, origBamAlignment)), context.Sequence,
                        _trackActualMismatches, _checkSoftclipsForMismatches, context.StartPosition);

                realignmentUnchanged = _judger.RealignmentIsUnchanged(realignResult, origBamAlignment);

                if (originalAlignmentSummary.NumMismatches > 0)
                {
                    // TODO PERF do we still want to use this ever?
                    var sumMismatch = Helper.GetSumOfMismatchQualities(origBamAlignment.Qualities,
                        origBamAlignment.Bases, new Read(_chromosome, origBamAlignment).PositionMap, context.Sequence,
                        context.StartPosition);
                    originalAlignmentSummary.SumOfMismatchingQualities = sumMismatch;
                }

                // Within this logic also checking the same as "!realignmentUnchanged" above.. consolidate this.
                if (selectedIndels != null &&
                    (_judger.RealignmentBetterOrEqual(realignResult, originalAlignmentSummary, confirmedAccepteds != null && confirmedAccepteds.Any())) ||
                    ResultIsGoodEnough(realignResult, origBamAlignment, originalAlignmentSummary,
                        realignmentUnchanged, confirmedAccepteds != null && confirmedAccepteds.Any()))
                {

                    UpdateIndelOutcomes(numIndels, orderedIndels, hasAnyIndels, acceptedIndels, confirmedAccepteds, true, realignResult);

                    if (realignResult.IsSketchy)
                    {
                        sketchy = true;
                    }
                    return AcceptRealignment(origBamAlignment, out changed, selectedIndels, existingIndels, realignResult, originalAlignmentSummary, bamAlignment, hasExistingUnsanctionedIndels, out confirmed);
                }
            }


            // At this point, any good realignment would have been returned. If it's realigned and changed now, it's an unaccepted (not good enough) realignment.
            // If it had an indel to begin with, it's basically a vote that we don't trust that indel. Optionally softclip it out.

            if (!realignmentUnchanged)
            {
                changed = false;
                confirmed = false;

                HandleFailedRealignment(origBamAlignment, ref forcedSoftclip, existingIndels, realignResult, hasExistingUnsanctionedIndels, existingMatches);

                if ((hasVeryGoodIndel || (hasHardToCallIndel && _judger.IsVeryConfident(originalAlignmentSummary))) && !hasExistingUnsanctionedIndels && existingMatchHashables.Any())
                {
                    // It didn't have the tip-top indel, but it had one that was very close, and we tried realigning around the top guys and failed - this one looks better. Give it credit.
                    confirmed = true;
                    foreach (var indel in existingMatchHashables)
                    {
                        UpdateOutcomeForConfirmed(indel);

                        if (confirmedAccepteds != null)
                        {
                            confirmedAccepteds.Add(indel);
                        }
                    }
                }
                UpdateIndelOutcomes(numIndels, orderedIndels, hasAnyIndels, acceptedIndels, confirmedAccepteds, false, realignResult);
            }
            else
            {
                if (acceptedIndels != null)
                {
                    foreach (var indelNum in acceptedIndels)
                    {
                        var indel = orderedIndels[indelNum];

                        UpdateOutcomeForConfirmed(indel);
                    }

                }

                _statusCounter.AddStatusCount("INDEL STATUS\tUnchanged\t" + realignResult?.Indels);
                _statusCounter.AppendStatusStringTag("RX", "Unchanged: " + realignResult?.Indels, origBamAlignment);

                confirmed = true;
                changed = false;
                return origBamAlignment;
            }

            if (realignResult == null)
            {

                if (_softclipUnknownIndels && hasExistingUnsanctionedIndels)
                {
                    var unsanctioned = existingIndels.Where(x => !existingMatches.Contains(x));

                    foreach (var preIndel in unsanctioned.OrderBy(x => x.ReferencePosition))
                    {
                        var reverseClip = false;
                        var clipLength = preIndel.RightAnchor;
                        if (preIndel.LeftAnchor < preIndel.RightAnchor)
                        {
                            reverseClip = true;
                            clipLength = preIndel.LeftAnchor;
                        }

                        // TODO arbitrary number here...
                        // If it's pretty well-anchored, don't remove the indel
                        if (clipLength > 20)
                        {
                            continue;
                        }

                        forcedSoftclip = true;
                        _statusCounter.AddStatusCount("Softclipped out bad indel");
                        _statusCounter.AppendStatusStringTag("RX",
                            $"Softclipped out bad indel({origBamAlignment.CigarData},{string.Join(",", existingIndels)}... No realignment",
                            origBamAlignment);
                        _statusCounter.AddStatusCount("INDEL STATUS\tRemoved\t" + string.Join("|", existingIndels));
                        OverlappingIndelHelpers.SoftclipAfterIndel(origBamAlignment,
                            reverseClip, preIndel.ReferencePosition);
                    }
                }

            }

            _statusCounter.AppendStatusStringTag("RX", "Realignment failed", origBamAlignment);
            _statusCounter.AddStatusCount("Realignment failed");

            return origBamAlignment;
        }

        private void UpdateOutcomeForConfirmed(HashableIndel existingConfirmedIndel)
        {
            if (!_indelOutcomes.TryGetValue(existingConfirmedIndel, out var outcomesForIndel))
            {
                // success, failure, Rank, numIndels, multis, confirmed
                outcomesForIndel = new int[8];
                _indelOutcomes.Add(existingConfirmedIndel, outcomesForIndel);
            }

            outcomesForIndel[2]++;
            outcomesForIndel[5]++;
            outcomesForIndel[3]++;
            // TODO this doesn't handle multis at all.
        }

        private BamAlignment AcceptRealignment(BamAlignment origBamAlignment, out bool changed, List<PreIndel> selectedIndels,
            List<PreIndel> existingIndels, RealignmentResult realignResult, AlignmentSummary originalAlignmentSummary,
            BamAlignment bamAlignment, bool hasExistingUnsanctionedIndels, out bool confirmed)
        {
            HandleAcceptedRealignment(origBamAlignment, selectedIndels, existingIndels, realignResult, bamAlignment,
                hasExistingUnsanctionedIndels, originalAlignmentSummary);

            confirmed = false;
            changed = true;

            return bamAlignment;
        }

        private void HandleFailedRealignment(BamAlignment origBamAlignment, ref bool forcedSoftclip, List<PreIndel> existingIndels,
            RealignmentResult realignResult, bool hasExistingUnsanctionedIndels,
            List<PreIndel> existingMatches)
        {
            _statusCounter.AddStatusCount("INDEL STATUS\tRejected\t" + realignResult.Indels);
            _statusCounter.AppendStatusStringTag("RX", "Did not accept: " + realignResult.Indels, origBamAlignment);

            // TODO could this be happening because of a low-ranked indel? Maybe we should be allowing to realign against all indels...
            // TODO STILL should this actually be happening also to reads that had no indels to realign around (i.e. started with weak indel, and couldn't go anywhere), not just the ones that were changed?
            if (_softclipUnknownIndels && hasExistingUnsanctionedIndels)
            {
                var unsanctioned = existingIndels.Where(x => !existingMatches.Contains(x));

                foreach (var preIndel in unsanctioned.OrderBy(x => x.ReferencePosition))
                {
                    var reverseClip = false;
                    var clipLength = preIndel.RightAnchor;
                    if (preIndel.LeftAnchor < preIndel.RightAnchor)
                    {
                        reverseClip = true;
                        clipLength = preIndel.LeftAnchor;
                    }

                    // TODO arbitrary number here...
                    // If it's pretty well-anchored, don't remove the indel
                    if (clipLength > 20)
                    {
                        continue;
                    }

                    forcedSoftclip = true;
                    _statusCounter.AddStatusCount("Softclipped out bad indel");
                    _statusCounter.AppendStatusStringTag("RX",
                        $"Softclipped out bad indel({origBamAlignment.CigarData},{string.Join(",", existingIndels)}...{realignResult?.Indels}",
                        origBamAlignment);
                    _statusCounter.AddStatusCount("INDEL STATUS\tRemoved\t" + string.Join("|", existingIndels));
                    OverlappingIndelHelpers.SoftclipAfterIndel(origBamAlignment,
                        reverseClip, preIndel.ReferencePosition);
                }
            }
        }

        private void UpdateIndelOutcomes(int numIndels, List<HashableIndel> orderedIndels, bool hasAnyIndels,
            List<int> acceptedIndels, List<HashableIndel> confirmedAcceptedIndels, bool acceptedRealignment,
            AlignmentSummary realignResult)
        {
            for (int i = 0; i < numIndels; i++)
            {
                var indel = orderedIndels[i];

                int[] outcomesForIndel;

                if (!_indelOutcomes.TryGetValue(indel, out outcomesForIndel))
                {
                    // success, failure, Rank, numIndels, multis, confirmed, acceptRealn, otherAccepted
                    outcomesForIndel = new int[8];
                    _indelOutcomes.Add(indel, outcomesForIndel);
                }

                if (hasAnyIndels && acceptedIndels.Contains(i))
                {
                    
                    outcomesForIndel[0]++;
                    outcomesForIndel[2] += i + 1;

                    if (acceptedRealignment)
                    {
                        outcomesForIndel[6]++;
                    }

                    var realignmentIsVeryConfident = _judger.IsVeryConfident(realignResult);

                    if (realignmentIsVeryConfident)
                    {
                        confirmedAcceptedIndels?.Add(indel);
                    }
                }
                else
                {
                    outcomesForIndel[1]++;

                    if (acceptedRealignment)
                    {
                        outcomesForIndel[7]++;
                    }
                }

                outcomesForIndel[3] += numIndels;
                outcomesForIndel[4] += acceptedIndels?.Count > 1 ? 1 : 0;
            }
        }


    

        private void HandleAcceptedRealignment(BamAlignment origBamAlignment, List<PreIndel> selectedIndels, 
            List<PreIndel> existingIndels,
            RealignmentResult realignResult, BamAlignment bamAlignment, bool hasExistingUnsanctionedIndels,
            AlignmentSummary originalAlignmentSummary)
        {

            bamAlignment.Position = realignResult.Position - 1; // 0 base
            bamAlignment.CigarData = realignResult.Cigar;

            if (_lightDebug)
            {
                AddStatusInfo(origBamAlignment, selectedIndels, existingIndels, realignResult, bamAlignment, hasExistingUnsanctionedIndels, originalAlignmentSummary);
            }

            _statusCounter.AppendStatusStringTag("RC", bamAlignment.GetStringTag("RC"), bamAlignment);
            if (bamAlignment.MapQuality <= 20 && realignResult.NumMismatches == 0 &&
                (_allowRescoringOrig0 || bamAlignment.MapQuality > 0))
                bamAlignment.MapQuality = 40; // todo what to set this to?  

            // Nify if using pair-specific indels
            if (realignResult.NifiedAt != null && realignResult.NifiedAt.Any())
            {
                foreach (var i in realignResult.NifiedAt)
                {
                    bamAlignment.Qualities[i] = 0;
                }

                _statusCounter.AddStatusCount(
                    $"Successfully realigned with mismatch-insertion quality adjusted (ps: {selectedIndels != null})");
                _statusCounter.AppendStatusStringTag("RX",
                    $"Successfully realigned with mismatch-insertion quality adjusted ({string.Join(",", realignResult.NifiedAt)}",
                    bamAlignment);
            }
        }

        private void AddStatusInfo(BamAlignment origBamAlignment, List<PreIndel> selectedIndels, List<PreIndel> existingIndels,
            RealignmentResult realignResult, BamAlignment bamAlignment, bool hasExistingUnsanctionedIndels,
            AlignmentSummary originalAlignmentSummary)
        {
            _statusCounter.AddStatusCount("INDEL STATUS\tAccepted\t" + realignResult.Indels);

            _statusCounter.AddStatusCount($"Successfully realigned (ps: {selectedIndels != null})");
            _statusCounter.AppendStatusStringTag("RX",
                $"Successfully realigned after {realignResult.Attempts} attempts, indel is {string.Join("|", realignResult.AcceptedIndels)}",
                bamAlignment);

            if (existingIndels != null && existingIndels.Any())
            {
                _statusCounter.AppendStatusStringTag("RX",
                    $"Orig indels:{string.Join("|", existingIndels)}__New indels:{realignResult.Indels}",
                    bamAlignment);
                _statusCounter.AddStatusCount(
                    $"Replaced existing indels (nonsanctioned: {hasExistingUnsanctionedIndels})");
            }

            bamAlignment.ReplaceOrAddStringTag("OC", $"{origBamAlignment.CigarData}");
            bamAlignment.ReplaceOrAddStringTag("OS",
                $"{originalAlignmentSummary.NumMatches}M-{originalAlignmentSummary.NumNonNSoftclips}S-{originalAlignmentSummary.NumMismatches}X-{originalAlignmentSummary.NumMismatchesIncludeSoftclip}x-{originalAlignmentSummary.NumInsertedBases}i-{originalAlignmentSummary.NumIndels}Z-{originalAlignmentSummary.SumOfMismatchingQualities}Q");
            bamAlignment.ReplaceOrAddStringTag("RS",
                $"{realignResult.NumMatches}M-{realignResult.NumNonNSoftclips}S-{realignResult.NumMismatches}X-{realignResult.NumMismatchesIncludeSoftclip}x-{realignResult.NumInsertedBases}i-{realignResult.NumIndels}Z-{realignResult.SumOfMismatchingQualities}Q");
        }


        public Dictionary<HashableIndel, int[]> GetIndelOutcomes()
        {
            return _indelOutcomes;
        }


        private bool ResultIsGoodEnough(RealignmentResult realignResult, BamAlignment origBamAlignment,
            AlignmentSummary originalAlignmentSummary, bool realignmentUnchanged, bool isPairAware)
        {
            if (realignmentUnchanged)
            {
                if (realignResult.NifiedAt.Any())
                {
                    return true;
                }
                _statusCounter.AppendStatusStringTag("RX", "Not taking realignment: unchanged", origBamAlignment);
                _statusCounter.AddStatusCount("Not taking realignment: unchanged");
                return false;
            }

            if (!_judger.RealignmentBetterOrEqual(realignResult, originalAlignmentSummary, isPairAware))
            {
                _statusCounter.AppendStatusStringTag("RX", $"Realignment failed:not better ({originalAlignmentSummary.Cigar}->{realignResult.Cigar}): {realignResult.Conclusion}", origBamAlignment);
                _statusCounter.UpdateStatusStringTag("OS", $"{originalAlignmentSummary.NumMatches}M-{originalAlignmentSummary.NumNonNSoftclips}S-{originalAlignmentSummary.NumMismatches}X-{originalAlignmentSummary.NumMismatchesIncludeSoftclip}x-{originalAlignmentSummary.NumInsertedBases}i-{originalAlignmentSummary.NumIndels}Z-{originalAlignmentSummary.SumOfMismatchingQualities}Q", origBamAlignment);
                _statusCounter.UpdateStatusStringTag("RS", $"{realignResult.NumMatches}M-{realignResult.NumNonNSoftclips}S-{realignResult.NumMismatches}X-{realignResult.NumMismatchesIncludeSoftclip}x-{realignResult.NumInsertedBases}i-{realignResult.NumIndels}Z-{realignResult.SumOfMismatchingQualities}Q", origBamAlignment);

                _statusCounter.AddStatusCount("Not taking realignment: not better");
                return false;
            }

            return true;

        }
    }
}