using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Pisces.Processing.Utility;
using StitchingLogic.Models;

namespace StitchingLogic
{
    public class CigarReconciler
    {
        private readonly ReadStatusCounter _statusCounter;
        private bool _debug;
        private readonly bool _ignoreProbeSoftclips;
        private readonly bool _allowTerminalClipsToSupportOverlappingDels;
        private bool _useSoftclippedBases;
        private readonly StitchedPosition[] _stitchPositions;
        private int _positionsUsed;

        public CigarReconciler(ReadStatusCounter statusCounter, bool useSoftclippedBases, bool debug, bool ignoreProbeSoftclips, int maxReadLength, bool allowTerminalClipsToSupportOverlappingDels = true)
        {
            _statusCounter = statusCounter;
            _useSoftclippedBases = useSoftclippedBases;
            _debug = debug;
            _ignoreProbeSoftclips = ignoreProbeSoftclips;
            _allowTerminalClipsToSupportOverlappingDels = allowTerminalClipsToSupportOverlappingDels;

            var maxStitchedReadLength = maxReadLength*2 - 1;

            _stitchPositions = new StitchedPosition[maxStitchedReadLength];

            // For performance reasons, we keep one collection of StitchedPositions and recycle them for each pair we try to stitch
            for (var i = 0; i < maxStitchedReadLength; i++)
            {
                _stitchPositions[i] = new StitchedPosition();
            }
        }

        public StitchingInfo GetStitchedCigar(CigarAlignment cigar1, int pos1, CigarAlignment cigar2, int pos2, bool reverseFirst, bool pairIsOutie)
        {
            var positions = GetStitchedSites(cigar1, cigar2, pos2, pos1);

            var success = true;

            var stitchingInfo = ReconcileSites(positions, reverseFirst, out success, pairIsOutie ? (int) cigar2.GetPrefixClip() : (int)cigar1.GetPrefixClip(), pairIsOutie ? (int) (cigar1.GetReadSpan() - (int) cigar1.GetSuffixClip()) : (int)(cigar2.GetReadSpan() - (int)cigar2.GetSuffixClip()), pairIsOutie);

            return success ? stitchingInfo : null;
        }

        public StitchingInfo ReconcileSites(List<StitchedPosition> positions, bool r1IsReverse, out bool success, int prefixProbeClipEnd, int suffixProbeClipStart, bool pairIsOutie, bool leftAlignUnmapped = true)
        {
            var stitchingInfo = new StitchingInfo();
            success = true;

            // Assumption is that exactly one read is forward and one read is reverse, and each component read is only one direction
            var r1DirectionType = r1IsReverse ? DirectionType.Reverse : DirectionType.Forward;
            var r2DirectionType = r1IsReverse ? DirectionType.Forward : DirectionType.Reverse;

            RedistributeSoftclips(positions, true);
            RedistributeSoftclips(positions, false);

            var indexInR1 = -1;
            var indexInR2 = -1;
            for (var i = 0; i < positions.Count; i++)
            {
                StitchedPosition positionBefore = null;
                if (i > 0)
                {
                    positionBefore = positions[i - 1];
                }
                var stitchPosition = positions[i];

                if (stitchPosition.UnmappedPrefix.R1HasInsertion() && !stitchPosition.UnmappedPrefix.R2Ops.Any())
                {
                    if (stitchPosition.MappedSite?.R2Ops.Count(x => x.IsReferenceSpan()) > 0 &&
                        positionBefore?.MappedSite?.R2Ops.Count(x => x.IsReferenceSpan()) > 0)
                    {
                        success = false;
                        return null;
                    }
                }
                if (stitchPosition.UnmappedPrefix.R2HasInsertion() && !stitchPosition.UnmappedPrefix.R1Ops.Any())
                {
                    if (stitchPosition.MappedSite?.R1Ops.Count(x => x.IsReferenceSpan()) > 0 &&
                        positionBefore?.MappedSite?.R1Ops.Count(x => x.IsReferenceSpan()) > 0)
                    {
                        success = false;
                        return null;
                    }
                }

                foreach (var stitchSite in new List<StitchedSite>() { stitchPosition.UnmappedPrefix, stitchPosition.MappedSite })
                {
                    var unmappedSite = stitchSite as UnmappedStretch;
                    var rightAlign = unmappedSite != null && unmappedSite.IsPrefix; //&& !unmappedSite.IsSuffix;

                    var offset = Math.Abs(stitchSite.R1Ops.Count - stitchSite.R2Ops.Count);
                    var r1StretchLonger = stitchSite.R1Ops.Count > stitchSite.R2Ops.Count;

                    for (var j = 0; j < Math.Max(stitchSite.R1Ops.Count(), stitchSite.R2Ops.Count()); j++)
                    {                       
                        int r1StretchIndex;
                        int r2StretchIndex;
                        if (rightAlign)
                        {
                            r1StretchIndex = r1StretchLonger ? j : j - offset;
                            r2StretchIndex = r1StretchLonger ? j - offset : j;
                        }
                        else
                        {
                            r1StretchIndex = j;
                            r2StretchIndex = j;
                        }
                        var r1Op = r1StretchIndex >= 0 && stitchSite.R1Ops.Count > r1StretchIndex ? stitchSite.R1Ops[r1StretchIndex] : null;
                        var r2Op = r2StretchIndex >= 0 && stitchSite.R2Ops.Count > r2StretchIndex ? stitchSite.R2Ops[r2StretchIndex] : null;

                        var combinedOp = GetCombinedOp(r1Op, r2Op);

                        if (combinedOp == null)
                        {
                            success = false;
                            if (_debug)
                            {
                                Logger.WriteToLog(string.Format("Could not stitch operations {0} and {1}.", r1Op?.Type,
                                    r2Op?.Type));
                            }
                            _statusCounter.AddDebugStatusCount("Could not stitch operations");
                            return null;
                        }

                        stitchingInfo.StitchedCigar.Add(combinedOp);
                        var r1opUsed = r1Op != null;
                        var r2opUsed = r2Op != null;

                        if (combinedOp.Type != 'S')
                        {
                            if (!_useSoftclippedBases && r2Op?.Type == 'S')
                            {
                                r2opUsed = false;
                            }
                            if (!_useSoftclippedBases && r1Op?.Type == 'S')
                            {
                                r1opUsed = false;
                            }
                        }

                        if (r1opUsed && r1Op.IsReadSpan())
                        {
                            indexInR1++;
                        }
                        if (r2opUsed && r2Op.IsReadSpan())
                        {
                            indexInR2++;
                        }

                        if (_ignoreProbeSoftclips)
                        {
                            if (r1opUsed && r1Op.Type == 'S')
                            {
                                var isProbeSoftclip  = (pairIsOutie && indexInR1 >= suffixProbeClipStart) ||
                                                       (!pairIsOutie && indexInR1 < prefixProbeClipEnd);

                                // If this is a probe softclip, don't stitch it
                                if (isProbeSoftclip && r2opUsed)
                                {
                                    r1opUsed = false;
                                    if (pairIsOutie)
                                    {
                                        stitchingInfo.IgnoredProbeSuffixBases ++;
                                    }
                                    else
                                    {
                                        stitchingInfo.IgnoredProbePrefixBases++;
                                    }
                                }
                            }
                            if (r2opUsed && r2Op.Type == 'S')
                            {
                                var isProbeSoftclip = (pairIsOutie && indexInR2 < prefixProbeClipEnd) ||
                                                      (!pairIsOutie && indexInR2 >= suffixProbeClipStart);
                                if (isProbeSoftclip && r1opUsed)
                                {
                                    r2opUsed = false;
                                    if (pairIsOutie)
                                    {
                                        stitchingInfo.IgnoredProbePrefixBases++;
                                    }
                                    else
                                    {
                                        stitchingInfo.IgnoredProbeSuffixBases++;
                                    }
                                }
                            }
                            // TODO support scenarios where R1 and R2 are both in probe softclips, if necessary. Otherwise, if this is really never going to happen, throw an exception if we see it.
                            if (!r1opUsed && !r2opUsed)
                            {
                                throw new Exception("Stitching exception: Both R1 and R2 are in probe softclip regions at overlapping position.");
                            }
                        }
                        var stitched = r1opUsed && r2opUsed;
                        stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp()
                        {
                            Direction =
                                stitched
                                    ? DirectionType.Stitched
                                    : (r1opUsed ? r1DirectionType : r2DirectionType),
                            Length = 1
                        });
                    }
                }

            }

            stitchingInfo.StitchedCigar.Compress();
            stitchingInfo.StitchedDirections.Compress();

            // Don't allow stitching that creates internal softclip
            if (stitchingInfo.StitchedCigar.HasInternalSoftclip())
            {
                success = false;
                return null;
            }

            return stitchingInfo;
        }

        private void RedistributeSoftclips(List<StitchedPosition> positions, bool operateOnR1)
        {
            var thisReadNum = operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2;
            var otherReadNum = operateOnR1 ? ReadNumber.Read2 : ReadNumber.Read1;
            StitchedPosition suffixToAdd = null;

            for (var indexInPositions = 0; indexInPositions < positions.Count; indexInPositions++)
            {
                // Try to spread bookending softclips across the further-extending positions on the opposite read

                var stitchPosition = positions[indexInPositions];
                StitchedPosition nextStitchPos = null;
                StitchedPosition previousStitchPos = null;

                if (indexInPositions <= positions.Count - 2)
                {
                    nextStitchPos = positions[indexInPositions + 1];
                }
                if (indexInPositions > 0)
                {
                    previousStitchPos = positions[indexInPositions - 1];
                }


                var isSuffixPosition = indexInPositions == positions.Count -1 || (nextStitchPos!= null && !nextStitchPos.UnmappedPrefix.GetOpsForRead(thisReadNum).Any() &&
                                       !nextStitchPos.MappedSite.GetOpsForRead(thisReadNum).Any());
                var isPrefixPosition = indexInPositions == 0 || (previousStitchPos!= null && !previousStitchPos.UnmappedPrefix.GetOpsForRead(thisReadNum).Any() &&
                                       !previousStitchPos.MappedSite.GetOpsForRead(thisReadNum).Any());

                if (stitchPosition.UnmappedPrefix.HasValue())
                {
                    stitchPosition.UnmappedPrefix.IsPrefix = isPrefixPosition;
                    stitchPosition.UnmappedPrefix.IsSuffix = isSuffixPosition;
                }

                // If this is a suffix clip, extend to the right.
                if (isSuffixPosition)
                {
                    // TODO only dole out those that go out past R2's unmappeds -- this should be fine now for suffix. need to fix for prefix.
                    if (stitchPosition.UnmappedPrefix.GetOpsForRead(thisReadNum).Count
                        > stitchPosition.UnmappedPrefix.GetOpsForRead(otherReadNum).Count)
                    {
                        var stitchPosCount = 0;
                        var numOpsToGiveAway = 0;
                        var opsToGiveAway = new List<CigarOp>();
                        var originalOps = new List<CigarOp>(stitchPosition.UnmappedPrefix.GetOpsForRead(thisReadNum));

                        for (var index = originalOps.Count - 1; index >= 0 ; index--)
                        {
                            var item = originalOps[index];

                            if (item.Type == 'S')
                            {
                                opsToGiveAway.Add(item);
                                numOpsToGiveAway++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        stitchPosition.UnmappedPrefix.SetOpsForRead(thisReadNum, new List<CigarOp> { });
                        for (var i = 0; i < originalOps.Count - numOpsToGiveAway; i++)
                        {
                            stitchPosition.UnmappedPrefix.AddOpsForRead(thisReadNum, new List<CigarOp> { originalOps[i] });
                        }
                        while (opsToGiveAway.Any())
                        {
                            var indexToRatchetTo = indexInPositions + stitchPosCount;

                            if (indexToRatchetTo > positions.Count - 1)
                            {
                                suffixToAdd = new StitchedPosition();
                                suffixToAdd.UnmappedPrefix.SetOpsForRead(thisReadNum, opsToGiveAway);
                                break;
                            }
                            var currentRatchetedStitchPos = positions[indexToRatchetTo];

                            // First support the unmappeds at this site
                            while (true)
                            {
                                if (currentRatchetedStitchPos.UnmappedPrefix.GetOpsForRead(otherReadNum).Count() ==
                                    currentRatchetedStitchPos.UnmappedPrefix.GetOpsForRead(thisReadNum).Count()
                                    || !opsToGiveAway.Any())
                                {
                                    break;
                                }
                                currentRatchetedStitchPos.UnmappedPrefix.AddOpsForRead(thisReadNum, new List<CigarOp> { opsToGiveAway.First() });
                                opsToGiveAway.RemoveAt(0);
                            }

                            // Support the mappeds at this site next
                            var otherSideOpsAtSite = currentRatchetedStitchPos.MappedSite.GetOpsForRead(otherReadNum);
                            var siteHasOtherSideMapped = otherSideOpsAtSite.Any();

                            if (_allowTerminalClipsToSupportOverlappingDels && siteHasOtherSideMapped && otherSideOpsAtSite.All(s => s.Type == 'D'))
                            {
                                // By virtue of us having a terminal S here, we can say we support this deletion, and kick the S over to support the ops at the other side of the deletion
                                // Assumption is that there's only one op at that site.
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(thisReadNum, new List<CigarOp> { new CigarOp(otherSideOpsAtSite.First().Type, otherSideOpsAtSite.First().Length) });
                            }
                            else if (siteHasOtherSideMapped && opsToGiveAway.Any() && !currentRatchetedStitchPos.MappedSite.GetOpsForRead(thisReadNum).Any())
                            {
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(thisReadNum, new List<CigarOp> { opsToGiveAway.First() });
                                opsToGiveAway.RemoveAt(0);
                            }

                            // Then move on to the next position.

                            // For suffix clips, start with the current site.
                            stitchPosCount++;

                        }
                    }
                }
                if (isPrefixPosition)
                {
                    // TODO only dole out those that go out past R2's unmappeds
                    if (stitchPosition.UnmappedPrefix.GetOpsForRead(thisReadNum).Count
                        > stitchPosition.UnmappedPrefix.GetOpsForRead(otherReadNum).Count)
                    {
                        // While I have any pieces to dole out, 
                        // if the previous position has something from R2 in mapped and nothing from R1, add a piece to R1 mapped
                        // if the previous position does not have something from R2 in mapped, put the whole remainder in R1 unmapped of whatever I'm on now
                        // if the previous position has something from R2 in mapped AND in unmapped and nothing from R1, add a piece to both

                        var stitchPosCount = 0;
                        var numOpsToGiveAway = 0;
                        var opsToGiveAway = new List<CigarOp>();
                        var originalOps = new List<CigarOp>(stitchPosition.UnmappedPrefix.GetOpsForRead(thisReadNum));
                        foreach (var item in originalOps)
                        {
                            if (item.Type == 'S')
                            {
                                opsToGiveAway.Add(item);
                                numOpsToGiveAway++;
                            }
                            else
                            {
                                break;
                            }
                        }
                        stitchPosition.UnmappedPrefix.SetOpsForRead(thisReadNum, new List<CigarOp> { });
                        for (var i = numOpsToGiveAway; i < originalOps.Count; i++)
                        {
                            stitchPosition.UnmappedPrefix.AddOpsForRead(thisReadNum, new List<CigarOp> { originalOps[i] });
                        }
                        while (opsToGiveAway.Any())
                        {
                            stitchPosCount++;
                            var indexToRatchetTo = indexInPositions - stitchPosCount;
                            var penultimateStitchPos = positions[indexToRatchetTo + 1];

                            if (indexToRatchetTo < 0)
                            {
                                penultimateStitchPos.UnmappedPrefix.SetOpsForRead(thisReadNum, opsToGiveAway);
                                break;
                            }
                            var currentRatchetedStitchPos = positions[indexToRatchetTo];

                            var otherSideOpsAtPreviousSite = currentRatchetedStitchPos.MappedSite.GetOpsForRead(operateOnR1 ? ReadNumber.Read2 : ReadNumber.Read1);
                            var previousHasSomethingInR2Mapped = otherSideOpsAtPreviousSite.Any();

                            if (_allowTerminalClipsToSupportOverlappingDels && previousHasSomethingInR2Mapped && otherSideOpsAtPreviousSite.All(s => s.Type == 'D'))
                            {
                                // By virtue of us having a terminal S here, we can say we support this deletion, and kick the S over to support the ops at the other side of the deletion
                                // Assumption is that there's only one op at that site.
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(thisReadNum, new List<CigarOp> { new CigarOp(otherSideOpsAtPreviousSite.First().Type, otherSideOpsAtPreviousSite.First().Length) });
                                continue;
                            }
                            else if (previousHasSomethingInR2Mapped && opsToGiveAway.Any() && !currentRatchetedStitchPos.MappedSite.GetOpsForRead(operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2).Any())
                            {
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2, new List<CigarOp> { opsToGiveAway.Last() });
                                opsToGiveAway.RemoveAt(opsToGiveAway.Count - 1);
                            }
                            else
                            {
                                penultimateStitchPos.UnmappedPrefix.SetOpsForRead(operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2, new List<CigarOp>(opsToGiveAway));
                                break;
                            }

                        }
                    }
                }
            }

            if (suffixToAdd != null)
            {
                positions.Add(suffixToAdd);
            }
        }

        private CigarOp GetCombinedOp(CigarOp r1Op, CigarOp r2Op)
        {
            if (r1Op == null && r2Op == null)
            {
                return null;
            }
            if (r1Op == null)
            {
                return r2Op;
            }
            if (r2Op == null)
            {
                return r1Op;
            }
            if (r1Op.Type == r2Op.Type)
            {
                return r1Op;
            }

            // TODO - more nuanced resolution
            if (r1Op.Type == 'S')
            {
                return r2Op;
            }
            if (r2Op.Type == 'S')
            {
                return r1Op;
            }
            else return null;
        }

        private StitchedPosition GetFreshStitchedPosition()
        {
            if (_positionsUsed >= _stitchPositions.Length)
            {
                throw new ArgumentException("Combined length of reads is greater than the expected maximum stitched read length of " + _stitchPositions.Length);
            }

            var stitchPosition = _stitchPositions[_positionsUsed];
            stitchPosition.Reset();
            _positionsUsed++;
            return stitchPosition;
        }

        private List<StitchedPosition> GetStitchedSites(CigarAlignment cigar1, CigarAlignment cigar2, long firstPos2, long firstPos1)
        {
            var expandedCigar1 = cigar1.Expand();
            var expandedCigar2 = cigar2.Expand();

            var posDict = new Dictionary<int, StitchedPosition>();

            _positionsUsed = 0;

            var refPos = 0;
            foreach (var op in expandedCigar1)
            {
                if (!posDict.ContainsKey(refPos))
                {
                    posDict[refPos] = GetFreshStitchedPosition();
                }
                if (op.IsReferenceSpan())
                {
                    posDict[refPos].MappedSite.R1Ops.Add(op);
                    refPos++;
                }
                else
                {
                    posDict[refPos].UnmappedPrefix.R1Ops.Add(op);
                }
            }

            // Reset the ref pos
            refPos = (int)(firstPos2 - firstPos1);

            foreach (var op in expandedCigar2)
            {
                if (!posDict.ContainsKey(refPos))
                {
                    posDict[refPos] = GetFreshStitchedPosition();
                }
                if (op.IsReferenceSpan())
                {
                    posDict[refPos].MappedSite.R2Ops.Add(op);
                    refPos++;
                }
                else
                {
                    posDict[refPos].UnmappedPrefix.R2Ops.Add(op);
                }
            }

            return posDict.OrderBy(x => x.Key).Select(x => x.Value).ToList();
        }

    }
}