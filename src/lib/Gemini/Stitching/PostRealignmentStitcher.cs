using System;
using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;

namespace Gemini.Stitching
{
    public class PostRealignmentStitcher : IReadRestitcher
    {
        private readonly IReadPairHandler _stitchedPairHandler;
        private readonly IStatusHandler _statusHandler;

        public PostRealignmentStitcher(IReadPairHandler stitchedPairHandler, IStatusHandler statusHandler)
        {
            _stitchedPairHandler = stitchedPairHandler;
            _statusHandler = statusHandler;
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

        public List<BamAlignment> GetRestitchedReads(ReadPair pair, BamAlignment origRead1, BamAlignment origRead2, int? r1Nm, int? r2Nm, bool realignedAroundPairSpecific)
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
                var stitchedReads = _stitchedPairHandler.ExtractReads(pair);
                if (stitchedReads.Count == 1)
                {
                    if (realignedAroundPairSpecific)
                    {
                        // Check that the stitched read represents an overall improvement over the original reads

                        // TODO
                        // Split the stitched read into StitchF=F+S and StitchR=S+R, and compare StitchF to OrigF and StitchR to OrigR
                        // But I need the sequence info for this - for now, just check the NMs + num softclips

                        var nmStitched = stitchedReads[0].GetIntTag("NM"); // TODO wait a minute, stitched reads at this point still don't have true NM...
                        var scStitched = stitchedReads[0].CigarData.GetPrefixClip() +
                                         stitchedReads[0].CigarData.GetSuffixClip();
                        var r1Sc = origRead1.CigarData.GetPrefixClip() +
                                   origRead1.CigarData.GetSuffixClip();
                        var r2Sc = origRead2.CigarData.GetPrefixClip() +
                                   origRead2.CigarData.GetSuffixClip();

                        var origMess1 = r1Nm + r1Sc;
                        var origMess2 = r2Nm + r2Sc;
                        var stitchedMess = nmStitched + scStitched;

                        // TODO arbitrary number, but this is just a dummy-ish method for now
                        if (((stitchedMess - origMess1) + (stitchedMess - origMess2)) > 2)
                        {
                            badRestitchAfterPairSpecificRealign = true;
                        }
                    }

                    if (!badRestitchAfterPairSpecificRealign)
                    {
                        //_statusCounter.AddStatusCount(
                        //    $"Successfully stitched after attempted {(realignedAroundPairSpecific ? "PairSpecific ":"")}realign (r1:{realignedR1}, r2:{realignedR2})");
                        _statusHandler.AddStatusCount($"Successfully stitched after realign (ps: {realignedAroundPairSpecific})");
                        _statusHandler.AddCombinedStatusStringTags("OC", pair.Read1, pair.Read2, stitchedReads[0]);
                        _statusHandler.AddCombinedStatusStringTags("OS", pair.Read1, pair.Read2, stitchedReads[0]);
                        _statusHandler.AddCombinedStatusStringTags("RS", pair.Read1, pair.Read2, stitchedReads[0]);
                        _statusHandler.AddCombinedStatusStringTags("RC", pair.Read1, pair.Read2, stitchedReads[0]);
                        _statusHandler.AddCombinedStatusStringTags("RX", pair.Read1, pair.Read2, stitchedReads[0]);

                        TagUtils.ReplaceOrAddStringTag(ref stitchedReads[0].TagData, "SC",
                            pair.Read1.GetStringTag("SC") + pair.Read1.CigarData + "," + pair.Read2.CigarData);

                        if (r1Nm > 0 || r2Nm > 0)
                        {
                            // TODO add real NM calculation here?
                            TagUtils.ReplaceOrAddIntTag(ref stitchedReads[0].TagData, "NM", Math.Max(r1Nm.Value, r2Nm.Value));
                        }
                        // TODO reenable?
                        //_statusHandler.AppendStringTag("RC", $"{origRead1.GetStringTag("RC")},GoodRestitchAfter{(realignedAroundPairSpecific ? "PairSpecific" : "")}Realign", stitchedReads[0]);

                    }
                }
                else
                {
                    _statusHandler.AddStatusCount($"Failed stitching after realign (ps: {realignedAroundPairSpecific})");
                    _statusHandler.AppendStatusStringTag("RC",$"Bad after attempted pair-specific realign (ps: {realignedAroundPairSpecific})", origRead1);
                    _statusHandler.AppendStatusStringTag("RC", $"Bad after attempted pair-specific realign (ps: {realignedAroundPairSpecific})", origRead2);
                }

                if (badRestitchAfterPairSpecificRealign)
                {
                    _statusHandler.AddStatusCount($"Restitching was too messy after realign (ps: {realignedAroundPairSpecific})");
                    _statusHandler.AppendStatusStringTag("RC", "BadRestitchAfterPairSpecificRealign", origRead1);
                    _statusHandler.AppendStatusStringTag("RC", "BadRestitchAfterPairSpecificRealign", origRead2);

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
                return disagreeingReads;
            }

            //if (!badRestitchAfterPairSpecificRealign && _isSnowball)
            //{
            //    foreach (var bamAlignment in reads)
            //    {
            //        //IndelEvidenceHelper.FindIndelsAndRecordEvidence(bamAlignment, _targetFinder, _lookup,
            //        //    true, _chromosome, 30, true);
            //    }
            //}

            return reads;
        }
    }
}