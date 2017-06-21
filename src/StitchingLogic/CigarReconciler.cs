using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Common.IO.Utility;
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

        // For performance reasons, avoid allocating memory repeatedly
        private readonly StitchedPosition[] _stitchPositions;
        private readonly List<StitchedPosition> _stitchPositionsList;
        private readonly List<CigarOp> _expandedCigar1;
        private readonly List<CigarOp> _expandedCigar2;

        private int _positionsUsed;

        public CigarReconciler(ReadStatusCounter statusCounter, bool useSoftclippedBases, bool debug, bool ignoreProbeSoftclips, int maxReadLength, bool allowTerminalClipsToSupportOverlappingDels = true)
        {
            _statusCounter = statusCounter;
            _useSoftclippedBases = useSoftclippedBases;
            _debug = debug;
            _ignoreProbeSoftclips = ignoreProbeSoftclips;
            _allowTerminalClipsToSupportOverlappingDels = allowTerminalClipsToSupportOverlappingDels;

            var maxStitchedReadLength = maxReadLength * 2 - 1;
            _stitchPositions = new StitchedPosition[maxStitchedReadLength];
            _stitchPositionsList = new List<StitchedPosition>(maxStitchedReadLength);
            _expandedCigar1 = new List<CigarOp>(maxReadLength);
            _expandedCigar2 = new List<CigarOp>(maxReadLength);

            // For performance reasons, we keep one collection of StitchedPositions and recycle them for each pair we try to stitch
            for (var i = 0; i < _stitchPositions.Length; i++)
            {
                _stitchPositions[i] = new StitchedPosition();
            }
        }

        public StitchingInfo GetStitchedCigar(CigarAlignment cigar1, int pos1, CigarAlignment cigar2, int pos2, bool reverseFirst, bool pairIsOutie)
        {
            // This list is cleared rather than reallocated to avoid excess garbage collection.
            _stitchPositionsList.Clear();

            GetStitchedSites(cigar1, cigar2, pos2, pos1);

            var success = true;

            var stitchingInfo = ReconcileSites(reverseFirst, out success, pairIsOutie ? (int) cigar2.GetPrefixClip() : (int)cigar1.GetPrefixClip(), pairIsOutie ? (int) (cigar1.GetReadSpan() - (int) cigar1.GetSuffixClip()) : (int)(cigar2.GetReadSpan() - (int)cigar2.GetSuffixClip()), pairIsOutie);

            return success ? stitchingInfo : null;
        }

        public StitchingInfo ReconcileSites(bool r1IsReverse, out bool success, int prefixProbeClipEnd, int suffixProbeClipStart, bool pairIsOutie, bool leftAlignUnmapped = true)
        {
            var stitchingInfo = new StitchingInfo();
            success = true;

            // Assumption is that exactly one read is forward and one read is reverse, and each component read is only one direction
            var r1DirectionType = r1IsReverse ? DirectionType.Reverse : DirectionType.Forward;
            var r2DirectionType = r1IsReverse ? DirectionType.Forward : DirectionType.Reverse;

            RedistributeSoftclips(true);
            RedistributeSoftclips(false);

            var indexInR1 = -1;
            var indexInR2 = -1;
            for (var i = 0; i < _stitchPositionsList.Count; i++)
            {
                StitchedPosition positionBefore = null;
                if (i > 0)
                {
                    positionBefore = _stitchPositionsList[i - 1];
                }
                var stitchPosition = _stitchPositionsList[i];

                if (stitchPosition.UnmappedPrefix.R1HasInsertion() && (stitchPosition.UnmappedPrefix.R2Ops.Count == 0))
                {
                    if (stitchPosition.MappedSite != null && stitchPosition.MappedSite.R2Ops.Any(x => x.IsReferenceSpan()) &&
                        positionBefore != null && positionBefore.MappedSite != null && positionBefore.MappedSite.R2Ops.Any(x => x.IsReferenceSpan()))
                    {
                        success = false;
                        return null;
                    }
                }
                if (stitchPosition.UnmappedPrefix.R2HasInsertion() && (stitchPosition.UnmappedPrefix.R1Ops.Count == 0))
                {
                    if (stitchPosition.MappedSite != null && stitchPosition.MappedSite.R1Ops.Any(x => x.IsReferenceSpan()) &&
                        positionBefore != null && positionBefore.MappedSite != null && positionBefore.MappedSite.R1Ops.Any(x => x.IsReferenceSpan()))
                    {
                        success = false;
                        return null;
                    }
                }

                success = ReconcileSite(stitchPosition.UnmappedPrefix, stitchingInfo, prefixProbeClipEnd, suffixProbeClipStart, pairIsOutie, r1DirectionType, r2DirectionType, ref indexInR1, ref indexInR2);
                if (!success)
                {
                    return null;
                }

                success = ReconcileSite(stitchPosition.MappedSite, stitchingInfo, prefixProbeClipEnd, suffixProbeClipStart, pairIsOutie, r1DirectionType, r2DirectionType, ref indexInR1, ref indexInR2);
                if (!success)
                {
                    return null;
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

        private bool ReconcileSite(StitchedSite stitchSite, StitchingInfo stitchingInfo, int prefixProbeClipEnd, int suffixProbeClipStart, bool pairIsOutie, DirectionType r1DirectionType, DirectionType r2DirectionType, ref int indexInR1, ref int indexInR2)
        {
            bool success = true;

            var unmappedSite = stitchSite as UnmappedStretch;
            var rightAlign = unmappedSite != null && unmappedSite.IsPrefix; //&& !unmappedSite.IsSuffix;

            var offset = Math.Abs(stitchSite.R1Ops.Count - stitchSite.R2Ops.Count);
            var r1StretchLonger = stitchSite.R1Ops.Count > stitchSite.R2Ops.Count;

            for (var j = 0; j < Math.Max(stitchSite.R1Ops.Count, stitchSite.R2Ops.Count); j++)
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
                CigarOp? r1Op = null;
                if (r1StretchIndex >= 0 && stitchSite.R1Ops.Count > r1StretchIndex)
                {
                    r1Op = stitchSite.R1Ops[r1StretchIndex];
                }

                CigarOp? r2Op = null;
                if (r2StretchIndex >= 0 && stitchSite.R2Ops.Count > r2StretchIndex)
                {
                    r2Op = stitchSite.R2Ops[r2StretchIndex];
                }

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
                    return success;
                }

                stitchingInfo.StitchedCigar.Add(combinedOp.Value);
                var r1opUsed = r1Op != null;
                var r2opUsed = r2Op != null;

                if (combinedOp.Value.Type != 'S')
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

                if (r1opUsed && r1Op.Value.IsReadSpan())
                {
                    indexInR1++;
                }
                if (r2opUsed && r2Op.Value.IsReadSpan())
                {
                    indexInR2++;
                }

                if (_ignoreProbeSoftclips)
                {
                    if (r1opUsed && r1Op.Value.Type == 'S')
                    {
                        var isProbeSoftclip = (pairIsOutie && indexInR1 >= suffixProbeClipStart) ||
                                                (!pairIsOutie && indexInR1 < prefixProbeClipEnd);

                        // If this is a probe softclip, don't stitch it
                        if (isProbeSoftclip && r2opUsed)
                        {
                            r1opUsed = false;
                            if (pairIsOutie)
                            {
                                stitchingInfo.IgnoredProbeSuffixBases++;
                            }
                            else
                            {
                                stitchingInfo.IgnoredProbePrefixBases++;
                            }
                        }
                    }
                    if (r2opUsed && r2Op.Value.Type == 'S')
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
                        throw new InvalidDataException("Stitching exception: Both R1 and R2 are in probe softclip regions at overlapping position.");
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

            return success;
        }

        private void RedistributeSoftclips(bool operateOnR1)
        {
            var thisReadNum = operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2;
            var otherReadNum = operateOnR1 ? ReadNumber.Read2 : ReadNumber.Read1;
            StitchedPosition suffixToAdd = null;

            for (var indexInPositions = 0; indexInPositions < _stitchPositionsList.Count; indexInPositions++)
            {
                // Try to spread bookending softclips across the further-extending positions on the opposite read

                var stitchPosition = _stitchPositionsList[indexInPositions];
                StitchedPosition nextStitchPos = null;
                StitchedPosition previousStitchPos = null;

                if (indexInPositions <= _stitchPositionsList.Count - 2)
                {
                    nextStitchPos = _stitchPositionsList[indexInPositions + 1];
                }
                if (indexInPositions > 0)
                {
                    previousStitchPos = _stitchPositionsList[indexInPositions - 1];
                }


                var isSuffixPosition = indexInPositions == _stitchPositionsList.Count -1 || (nextStitchPos!= null && (nextStitchPos.UnmappedPrefix.GetOpsForRead(thisReadNum).Count == 0) &&
                                       (nextStitchPos.MappedSite.GetOpsForRead(thisReadNum).Count == 0));
                var isPrefixPosition = indexInPositions == 0 || (previousStitchPos!= null && (previousStitchPos.UnmappedPrefix.GetOpsForRead(thisReadNum).Count == 0) &&
                                       (previousStitchPos.MappedSite.GetOpsForRead(thisReadNum).Count == 0));

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
                        while (opsToGiveAway.Count != 0)
                        {
                            var indexToRatchetTo = indexInPositions + stitchPosCount;

                            if (indexToRatchetTo > _stitchPositionsList.Count - 1)
                            {
                                suffixToAdd = new StitchedPosition();
                                suffixToAdd.UnmappedPrefix.SetOpsForRead(thisReadNum, opsToGiveAway);
                                break;
                            }
                            var currentRatchetedStitchPos = _stitchPositionsList[indexToRatchetTo];

                            // First support the unmappeds at this site
                            while (true)
                            {
                                if (currentRatchetedStitchPos.UnmappedPrefix.GetOpsForRead(otherReadNum).Count ==
                                    currentRatchetedStitchPos.UnmappedPrefix.GetOpsForRead(thisReadNum).Count
                                    || opsToGiveAway.Count == 0)
                                {
                                    break;
                                }
                                currentRatchetedStitchPos.UnmappedPrefix.AddOpsForRead(thisReadNum, new List<CigarOp> { opsToGiveAway.First() });
                                opsToGiveAway.RemoveAt(0);
                            }

                            // Support the mappeds at this site next
                            var otherSideOpsAtSite = currentRatchetedStitchPos.MappedSite.GetOpsForRead(otherReadNum);
                            var siteHasOtherSideMapped = (otherSideOpsAtSite.Count != 0);

                            if (_allowTerminalClipsToSupportOverlappingDels && siteHasOtherSideMapped && otherSideOpsAtSite.All(s => s.Type == 'D'))
                            {
                                // By virtue of us having a terminal S here, we can say we support this deletion, and kick the S over to support the ops at the other side of the deletion
                                // Assumption is that there's only one op at that site.
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(thisReadNum, new List<CigarOp> { new CigarOp(otherSideOpsAtSite.First().Type, otherSideOpsAtSite.First().Length) });
                            }
                            else if (siteHasOtherSideMapped && (opsToGiveAway.Count != 0) && (currentRatchetedStitchPos.MappedSite.GetOpsForRead(thisReadNum).Count == 0))
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
                        while (opsToGiveAway.Count != 0)
                        {
                            stitchPosCount++;
                            var indexToRatchetTo = indexInPositions - stitchPosCount;
                            var penultimateStitchPos = _stitchPositionsList[indexToRatchetTo + 1];

                            if (indexToRatchetTo < 0)
                            {
                                penultimateStitchPos.UnmappedPrefix.SetOpsForRead(thisReadNum, opsToGiveAway);
                                break;
                            }
                            var currentRatchetedStitchPos = _stitchPositionsList[indexToRatchetTo];

                            var otherSideOpsAtPreviousSite = currentRatchetedStitchPos.MappedSite.GetOpsForRead(operateOnR1 ? ReadNumber.Read2 : ReadNumber.Read1);
                            var previousHasSomethingInR2Mapped = (otherSideOpsAtPreviousSite.Count != 0);

                            if (_allowTerminalClipsToSupportOverlappingDels && previousHasSomethingInR2Mapped && otherSideOpsAtPreviousSite.All(s => s.Type == 'D'))
                            {
                                // By virtue of us having a terminal S here, we can say we support this deletion, and kick the S over to support the ops at the other side of the deletion
                                // Assumption is that there's only one op at that site.
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(thisReadNum, new List<CigarOp> { new CigarOp(otherSideOpsAtPreviousSite.First().Type, otherSideOpsAtPreviousSite.First().Length) });
                                continue;
                            }
                            else if (previousHasSomethingInR2Mapped && (opsToGiveAway.Count != 0) && (currentRatchetedStitchPos.MappedSite.GetOpsForRead(operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2).Count == 0))
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
                _stitchPositionsList.Add(suffixToAdd);
            }
        }

        private CigarOp? GetCombinedOp(CigarOp? r1Op, CigarOp? r2Op)
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
            if (r1Op.Value.Type == r2Op.Value.Type)
            {
                return r1Op;
            }

            // TODO - more nuanced resolution
            if (r1Op.Value.Type == 'S')
            {
                return r2Op;
            }
            if (r2Op.Value.Type == 'S')
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

        private void GetStitchedSites(CigarAlignment cigar1, CigarAlignment cigar2, long firstPos2, long firstPos1)
        {
            cigar1.Expand(_expandedCigar1);
            cigar2.Expand(_expandedCigar2);

            _positionsUsed = 0;

            if (firstPos1 < firstPos2)
            {
                AddR1ToList(0);
                AddR2ToList((int)(firstPos2 - firstPos1));
            }
            else
            {
                AddR2ToList(0);
                AddR1ToList((int)(firstPos1 - firstPos2));
            }
        }

        private void AddR1ToList(int refPos)
        {
            if (refPos > _stitchPositionsList.Count)
            {
                refPos = _stitchPositionsList.Count;
            }

            foreach (var op in _expandedCigar1)
            {
                if (refPos >= _stitchPositionsList.Count)
                {
                    _stitchPositionsList.Add(GetFreshStitchedPosition());
                }
                if (op.IsReferenceSpan())
                {
                    _stitchPositionsList[refPos].MappedSite.R1Ops.Add(op);
                    refPos++;
                }
                else
                {
                    _stitchPositionsList[refPos].UnmappedPrefix.R1Ops.Add(op);
                }
            }
        }

        private void AddR2ToList(int refPos)
        {
            if (refPos > _stitchPositionsList.Count)
            {
                refPos = _stitchPositionsList.Count;
            }

            foreach (var op in _expandedCigar2)
            {
                if (refPos >= _stitchPositionsList.Count)
                {
                    _stitchPositionsList.Add(GetFreshStitchedPosition());
                }
                if (op.IsReferenceSpan())
                {
                    _stitchPositionsList[refPos].MappedSite.R2Ops.Add(op);
                    refPos++;
                }
                else
                {
                    _stitchPositionsList[refPos].UnmappedPrefix.R2Ops.Add(op);
                }
            }
        }
    }
}