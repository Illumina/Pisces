using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Gemini.CandidateIndelSelection;
using Gemini.FromHygea;
using Gemini.Interfaces;
using Gemini.Models;
using Gemini.Utility;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
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
        private IIndelRanker _ranker;
        private Dictionary<string,int[]> _attemptedRealignments = new Dictionary<string, int[]>();
        private bool _allowRescoringOrig0;
        private readonly bool _softclipUnknownIndels;

        public RealignmentEvaluator(IChromosomeIndelSource indelSource, IStatusHandler statusCounter,
            IReadRealigner readRealinger, IRealignmentJudger judger, string chromosome, bool trackActualMismatches, bool checkSoftclipsForMismatches, bool allowRescoringOrig0, bool softclipUnknownIndels)
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
            _ranker = new IndelRanker();
        }

        public BamAlignment GetFinalAlignment(BamAlignment origBamAlignment, out bool changed, out bool forcedSoftclip, List<PreIndel> selectedIndels = null)
        {
            forcedSoftclip = false;
            if (origBamAlignment.CigarData.Count == 0)
            {
                // Some (at least amplicon) data has shown this: mapq is 0 but is still mapped, no cigar
                changed = false;
                return origBamAlignment;
            }
            var isImperfectRead = (origBamAlignment.ContainsDisallowedCigarOps(_suspectCigarOps) ||
                                   origBamAlignment.GetIntTag("NM") > 0);
            var isReadWorthCaringAbout = !origBamAlignment.IsDuplicate() && !origBamAlignment.IsSecondary();
            var isRealignable = isImperfectRead && isReadWorthCaringAbout && origBamAlignment.Bases.Distinct().Count() > 1;

            if (!isRealignable)
            {
                changed = false;
                return origBamAlignment;
            }

            var indels = _indelSource.GetRelevantIndels(origBamAlignment.Position, selectedIndels);


            if (!indels.Any() || origBamAlignment.CigarData.GetReferenceSpan() > 500)
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
                return origBamAlignment;
            }

            _statusCounter.AddStatusCount("Realigning to " + indels.Count());

            // TODO this should relate to cap on indel size... introducing too large of an indel will make us go beyond this context.
            var context = indels.First().Value;

            var originalAlignmentSummary =
                Extensions.GetAlignmentSummary((new Read(_chromosome, origBamAlignment)), context.Sequence, _trackActualMismatches, _checkSoftclipsForMismatches, context.StartPosition);

            var bamAlignment = new BamAlignment(origBamAlignment);
            var realignResult = _readRealigner.Realign(new Read(_chromosome, bamAlignment),
                indels.Select(x => x.Key).ToList(), indels.ToDictionary(x => x.Key, x => x.Value), _ranker, selectedIndels != null);


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


            if (originalAlignmentSummary.NumMismatches > 0 && realignResult != null)
            {
                //Console.WriteLine("Sum of mismatche:" + origBamAlignment.CigarData + " vs " + realignResult.Cigar);

                // TODO do we still want to use this ever?
                var sumMismatch = Helper.GetSumOfMismatchQualities(origBamAlignment.Qualities, origBamAlignment.Bases, new Read(_chromosome, origBamAlignment).PositionMap, context.Sequence,
                    context.StartPosition);
                originalAlignmentSummary.SumOfMismatchingQualities = sumMismatch;

                //Console.WriteLine("Avg: " + sumMismatch/(float)originalAlignmentSummary.NumMismatches + " vs " + realignResult?.SumOfMismatchingQualities/(float)(realignResult.NumMismatches));
                //Console.WriteLine("Sum: " + sumMismatch  + " vs " + realignResult?.SumOfMismatchingQualities );

            }

            var realignmentUnchanged =
                realignResult != null && _judger.RealignmentIsUnchanged(realignResult, origBamAlignment);

            // Within this logic also checking the same as "!realignmentUnchanged" above.. consolidate this.
            if (realignResult != null && ((selectedIndels != null && (_judger.RealignmentBetterOrEqual(realignResult, originalAlignmentSummary, true)) || ResultIsGoodEnough(realignResult, origBamAlignment, originalAlignmentSummary))))
            {
                AddEvidence(realignResult.Indels, true, realignResult.NumMismatches - originalAlignmentSummary.NumMismatches);
                _statusCounter.AddStatusCount("Accepted: " + realignResult.Indels);

                _statusCounter.AddStatusCount($"Successfully realigned (ps: {selectedIndels != null})");
                _statusCounter.AppendStatusStringTag("RX","Successfully realigned", bamAlignment);

                bamAlignment.Position = realignResult.Position - 1; // 0 base
                bamAlignment.CigarData = realignResult.Cigar;
                bamAlignment.UpdateIntTagData("NM", realignResult.NumMismatches); // update NM tag

                TagUtils.ReplaceOrAddStringTag(ref bamAlignment.TagData, "OC", $"{origBamAlignment.CigarData}");
                TagUtils.ReplaceOrAddStringTag(ref bamAlignment.TagData, "OS", $"{originalAlignmentSummary.NumMatches}M-{originalAlignmentSummary.NumNonNSoftclips}S-{originalAlignmentSummary.NumMismatches}X-{originalAlignmentSummary.NumMismatchesIncludeSoftclip}x-{originalAlignmentSummary.NumInsertedBases}i-{originalAlignmentSummary.NumIndels}Z");
                TagUtils.ReplaceOrAddStringTag(ref bamAlignment.TagData, "RS", $"{realignResult.NumMatches}M-{realignResult.NumNonNSoftclips}S-{realignResult.NumMismatches}X-{realignResult.NumMismatchesIncludeSoftclip}x-{realignResult.NumInsertedBases}i-{realignResult.NumIndels}Z");
                _statusCounter.AppendStatusStringTag("RC", bamAlignment.GetStringTag("RC"), bamAlignment);
                if (bamAlignment.MapQuality <= 20 && realignResult.NumMismatches == 0 &&
                    (_allowRescoringOrig0 || bamAlignment.MapQuality > 0))
                    bamAlignment.MapQuality = 40; // todo what to set this to?  

                changed = true;
                return bamAlignment;
            }

            // At this point, any good realignment would have been returned. If it's realigned and changed now, it's an unaccepted (not good enough) realignment.
            // If it had an indel to begin with, it's basically a vote that we don't trust that indel. Optionally softclip it out.
            if (!realignmentUnchanged)
            {
                if (realignResult != null)
                {
                    AddEvidence(realignResult.Indels, false, realignResult.NumMismatches - originalAlignmentSummary.NumMismatches);
                    _statusCounter.AddStatusCount("Did not accept: " + realignResult.Indels);
                    _statusCounter.AppendStatusStringTag("RX", "Did not accept: " + realignResult.Indels, origBamAlignment);

                    //if (realignResult.NumMismatches -
                    //    originalAlignmentSummary.NumMismatches < 0)
                    //{
                    //    Console.WriteLine(origBamAlignment.Name + " didn't accept indel " + realignResult.Indels + $"{originalAlignmentSummary.NumMatches}M-{originalAlignmentSummary.NumNonNSoftclips}S-{originalAlignmentSummary.NumMismatches}X-{originalAlignmentSummary.NumMismatchesIncludeSoftclip}x-{originalAlignmentSummary.NumInsertedBases}i-{originalAlignmentSummary.NumIndels}Z" + " vs " + $"{realignResult.NumMatches}M-{realignResult.NumNonNSoftclips}S-{realignResult.NumMismatches}X-{realignResult.NumMismatchesIncludeSoftclip}x-{realignResult.NumInsertedBases}i-{realignResult.NumIndels}Z");
                    //}
                }

                // TODO should this actually be happening also to reads that had no indels to realign around (i.e. started with weak indel, and couldn't go anywhere), not just the ones that were changed?
                if (_softclipUnknownIndels && ReadContainsImperfections(origBamAlignment, true))
                {
                    forcedSoftclip = true;
                    _statusCounter.AddStatusCount("Softclipped out bad indel");
                    _statusCounter.AppendStatusStringTag("RX",$"Softclipped out bad indel({origBamAlignment.CigarData}",origBamAlignment);
                    OverlappingIndelHelpers.SoftclipAfterAnyIndel(origBamAlignment,
                        origBamAlignment.IsReverseStrand());
                }
            }

            _statusCounter.AppendStatusStringTag("RX", "Realignment failed", origBamAlignment);
            _statusCounter.AddStatusCount("Realignment failed");
            changed = false;
            return origBamAlignment;
        }


        private bool ReadContainsImperfections(BamAlignment alignment, bool trustSoftClips)
        {
            if (alignment == null)
            {
                return false;
            }
            foreach (CigarOp op in alignment.CigarData)
            {
                if (op.Type == 'I' || op.Type == 'D' || (!trustSoftClips && op.Type == 'S'))
                {
                    return true;
                }
            }

            return false;

        }
        private bool ResultIsGoodEnough(RealignmentResult realignResult, BamAlignment origBamAlignment,
            AlignmentSummary originalAlignmentSummary)
        {
            if (
                _judger.RealignmentIsUnchanged(realignResult, origBamAlignment))
            {
                _statusCounter.AppendStatusStringTag("RX", "Not taking realignment: unchanged", origBamAlignment);
                _statusCounter.AddStatusCount("Not taking realignment: unchanged");
                return false;
            }

            if (!_judger.RealignmentBetterOrEqual(realignResult, originalAlignmentSummary, false))
            {
                _statusCounter.AppendStatusStringTag("RX", $"Realignment failed:not better ({realignResult.Cigar})", origBamAlignment);
                _statusCounter.UpdateStatusStringTag("OS", $"{originalAlignmentSummary.NumMatches}M-{originalAlignmentSummary.NumNonNSoftclips}S-{originalAlignmentSummary.NumMismatches}X-{originalAlignmentSummary.NumMismatchesIncludeSoftclip}x-{originalAlignmentSummary.NumInsertedBases}i-{originalAlignmentSummary.NumIndels}Z", origBamAlignment);
                _statusCounter.UpdateStatusStringTag("RS", $"{realignResult.NumMatches}M-{realignResult.NumNonNSoftclips}S-{realignResult.NumMismatches}X-{realignResult.NumMismatchesIncludeSoftclip}x-{realignResult.NumInsertedBases}i-{realignResult.NumIndels}Z", origBamAlignment);

                _statusCounter.AddStatusCount("Not taking realignment: not better");
                return false;
            }

            return true;

        }

        private void AddEvidence(string indel, bool succeed, int mismatches)
        {
            if (!_attemptedRealignments.ContainsKey(indel))
            {
                _attemptedRealignments[indel] = new int[4];
            }
            _attemptedRealignments[indel][succeed ? 0 : 1]++;
            _attemptedRealignments[indel][succeed ? 2 : 3] += mismatches;
        }
    }
}
