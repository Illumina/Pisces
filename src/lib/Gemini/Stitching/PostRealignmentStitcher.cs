using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;
using Gemini.Logic;

namespace Gemini.Stitching
{
    public class PostRealignmentStitcher : IReadRestitcher
    {
        private readonly IReadPairHandler _stitchedPairHandler;
        private readonly IStatusHandler _statusHandler;
        private readonly List<string> _tagsToKeepFromR1;

        public PostRealignmentStitcher(IReadPairHandler stitchedPairHandler, IStatusHandler statusHandler, List<string> tagsToKeepFromR1 = null)
        {
            _stitchedPairHandler = stitchedPairHandler;
            _statusHandler = statusHandler;
            _tagsToKeepFromR1 = tagsToKeepFromR1 ?? new List<string>();
        }

        private List<BamAlignment> ShouldRestitch(ReadPair pair)
        {
            var result = OverlappingIndelHelpers.IndelsDisagreeWithStrongMate(pair.Read1, pair.Read2, out bool disagree);

            if (disagree)
            {
                return result;
            }

            return null;
        }

        public List<BamAlignment> GetRestitchedReads(ReadPair pair, BamAlignment origRead1, BamAlignment origRead2, int? r1Nm, int? r2Nm, bool realignedAroundPairSpecific, INmCalculator nmCalculator, bool recalculateNm, bool realignmentIsSketchy = false)
        {
            var reads = new List<BamAlignment>();
            var badRestitchAfterPairSpecificRealign = false;

            var disagreeingReads = realignedAroundPairSpecific ? ShouldRestitch(pair) : null;

            if (disagreeingReads == null)
            {
                // TODO in some cases, we already tried stitching and then are realigning. 
                // ... If nothing realigned, we don't need to try stitching again as we know it already failed. 
                // ... However, in some cases, we haven't tried stitching yet as we were deferring until realignment. 
                // ... For now, it's ok to leave this here, but know that we are wasting time in some cases trying to stitch the same pair again.
                int nmStitched = 0;
                var stitchedReads = _stitchedPairHandler.ExtractReads(pair);
                if (stitchedReads.Count == 1)
                {
                    if (recalculateNm)
                    {
                        nmStitched = nmCalculator.GetNm(stitchedReads[0]);
                        pair.StitchedNm = nmStitched;
                    }

                    if (realignedAroundPairSpecific)
                    {
                        // Check that the stitched read represents an overall improvement over the original reads
                        var scStitched = stitchedReads[0].CigarData.GetPrefixClip() +
                                         stitchedReads[0].CigarData.GetSuffixClip();
                        var r1Sc = origRead1.CigarData.GetPrefixClip() +
                                   origRead1.CigarData.GetSuffixClip();
                        var r2Sc = origRead2.CigarData.GetPrefixClip() +
                                   origRead2.CigarData.GetSuffixClip();

                        var origMess1 = r1Nm + r1Sc;
                        var origMess2 = r2Nm + r2Sc;
                        var stitchedMess = nmStitched + scStitched;

                        if (stitchedMess > (origMess1 + origMess2))
                        {
                            badRestitchAfterPairSpecificRealign = true;
                        }
                    }

                    if (!badRestitchAfterPairSpecificRealign)
                    {
                        _statusHandler.AddStatusCount($"Successfully stitched after realign (ps: {realignedAroundPairSpecific})");
                        _statusHandler.AddCombinedStatusStringTags("OC", pair.Read1, pair.Read2, stitchedReads[0]);
                        _statusHandler.AddCombinedStatusStringTags("OS", pair.Read1, pair.Read2, stitchedReads[0]);
                        _statusHandler.AddCombinedStatusStringTags("RS", pair.Read1, pair.Read2, stitchedReads[0]);
                        _statusHandler.AddCombinedStatusStringTags("RC", pair.Read1, pair.Read2, stitchedReads[0]);
                        _statusHandler.AddCombinedStatusStringTags("RX", pair.Read1, pair.Read2, stitchedReads[0]);

                        stitchedReads[0].ReplaceOrAddStringTag("SC",
                            pair.Read1.GetStringTag("SC") + pair.Read1.CigarData + "," + pair.Read2.CigarData);
                        stitchedReads[0].ReplaceOrAddIntTag("NM", nmStitched, true);

                        foreach (var tag in _tagsToKeepFromR1)
                        {
                            var r1Tag = pair.Read1.GetStringTag(tag);

                            if (r1Tag != null)
                            {
                                stitchedReads[0].ReplaceOrAddStringTag(tag, r1Tag);
                            }
                        }
                    }
                }
                else
                {
                    pair.FailForOtherReason = true;
                    if (realignmentIsSketchy)
                    {
                        badRestitchAfterPairSpecificRealign = true;
                    }
                    _statusHandler.AddStatusCount($"Failed stitching after realign (ps: {realignedAroundPairSpecific})");
                    _statusHandler.AppendStatusStringTag("RC",$"Bad after attempted pair-specific realign (ps: {realignedAroundPairSpecific})", origRead1);
                    _statusHandler.AppendStatusStringTag("RC", $"Bad after attempted pair-specific realign (ps: {realignedAroundPairSpecific})", origRead2);
                }

                if (badRestitchAfterPairSpecificRealign)
                {
                    _statusHandler.AddStatusCount($"Restitching was too messy after realign (ps: {realignedAroundPairSpecific})");
                    _statusHandler.AppendStatusStringTag("RC", "BadRestitchAfterPairSpecificRealign", origRead1);
                    _statusHandler.AppendStatusStringTag("RC", "BadRestitchAfterPairSpecificRealign", origRead2);

                    pair.BadRestitch = true;
                    // Go back to the originals
                    reads.Add(origRead1);
                    reads.Add(origRead2);
                }
                else
                {
                    reads = stitchedReads;
                }
            }
            else
            {
                pair.Disagree = true;
                return disagreeingReads;
            }

            return reads;
        }
    }
}