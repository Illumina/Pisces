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
    public class ReadMerger
    {
        private const int MaxReadLength = 200;
	    private const int MaxBaseQuality = 93;
        private bool _useSoftclippedBases;
        private bool _allowRescuedInsertionBaseDisagreement;
        private ReadStatusCounter _statusCounter;
        private bool _debug;
        private readonly bool _ignoreProbeSoftclips;
        private bool _nifyDisagreements;
        private int _minBasecallQuality;

        // Allocate these once for performance
        private readonly List<char> _stitchedBases = new List<char>(MaxReadLength);
        private readonly List<byte> _stitchedQualities = new List<byte>(MaxReadLength);

        public ReadMerger(int minBasecallQuality, bool allowRescuedInsertionBaseDisagreement, bool useSoftclippedBases, bool nifyDisagreements, ReadStatusCounter statusCounter, bool debug, bool ignoreProbeSoftclips)
        {
            _minBasecallQuality = minBasecallQuality;
            _allowRescuedInsertionBaseDisagreement = allowRescuedInsertionBaseDisagreement;
            _useSoftclippedBases = useSoftclippedBases;
            _nifyDisagreements = nifyDisagreements;
            _statusCounter = statusCounter;
            _debug = debug;
            _ignoreProbeSoftclips = ignoreProbeSoftclips;
        }

        // TODO I strongly advise we remove this logic altogether. It is brittle, incomplete, and hasn't been shown to reliably improve results.
        public Read GenerateNifiedMergedRead(AlignmentSet set, bool useSoftclippedBases)
        {
            var read1InsertionAdjustedEnd = set.PartnerRead1.ClipAdjustedEndPosition +
                                            set.PartnerRead1.CigarData.GetSuffixInsertionLength();
            var read2InsertionAdjustedEnd = set.PartnerRead2.ClipAdjustedEndPosition +
                                            set.PartnerRead2.CigarData.GetSuffixInsertionLength();

            var read1LongerThanRead2 = read2InsertionAdjustedEnd < read1InsertionAdjustedEnd;

            var furthestRight = read1LongerThanRead2 ? read1InsertionAdjustedEnd : read2InsertionAdjustedEnd;

            var nifiedStitchedLength = furthestRight + 1 - set.PartnerRead1.ClipAdjustedPosition;

            var prefixClip = set.PartnerRead1.CigarData.GetPrefixClip();
            var suffixClip = read1LongerThanRead2
                ? set.PartnerRead1.CigarData.GetSuffixClip()
                : set.PartnerRead2.CigarData.GetSuffixClip();
            if (read2InsertionAdjustedEnd == read1InsertionAdjustedEnd)
                suffixClip = Math.Min(set.PartnerRead1.CigarData.GetSuffixClip(),
                    set.PartnerRead2.CigarData.GetSuffixClip());

            if (prefixClip + suffixClip >= nifiedStitchedLength)
            {
                throw new ArgumentException($"Reads cannnot be Nified using this simple algorithm. The prefix and suffix sofctlips overlap for reads: {set.PartnerRead1.Name} {set.PartnerRead1.Position}:{set.PartnerRead1.CigarData} and {set.PartnerRead2.Position}:{set.PartnerRead2.CigarData}");
            }
            var nifiedStitchedCigar = new CigarAlignment((prefixClip > 0 ? string.Format("{0}S", prefixClip) : "")
                                                         + string.Format("{0}M", nifiedStitchedLength - prefixClip - suffixClip)
                                                         + (suffixClip > 0 ? string.Format("{0}S", suffixClip) : ""));
            var beforeOverlap = (useSoftclippedBases ? set.PartnerRead2.ClipAdjustedPosition : set.PartnerRead2.Position) -
                                set.PartnerRead1.ClipAdjustedPosition;
            var afterOverlap = read1LongerThanRead2
                ? (read1InsertionAdjustedEnd - read2InsertionAdjustedEnd)
                : (read2InsertionAdjustedEnd - read1InsertionAdjustedEnd);
            var r1Forward = set.PartnerRead1.SequencedBaseDirectionMap.First() == DirectionType.Forward;
            var beforeOverlapDirection = r1Forward ? "F" : "R";
            var afterOverlapDirection = read1LongerThanRead2 ? (r1Forward ? "F" : "R") : (r1Forward ? "R" : "F");

            var nifiedStitchedDirections = (beforeOverlap > 0
                ? string.Format("{0}{1}", beforeOverlap, beforeOverlapDirection)
                : "")
                                           + string.Format("{0}S", nifiedStitchedLength - beforeOverlap - afterOverlap)
                                           +
                                           (afterOverlap > 0 ? string.Format("{0}{1}", afterOverlap, afterOverlapDirection) : "");

            var mergedRead = new Read(set.PartnerRead1.Chromosome, new BamAlignment
            {
                Name = set.PartnerRead1.Name,
                Bases = new string('N', nifiedStitchedLength),
                Position = Math.Min(set.PartnerRead1.Position - 1, set.PartnerRead2.Position - 1),
                Qualities = Enumerable.Repeat((byte)0, nifiedStitchedLength).ToArray(),
                CigarData = nifiedStitchedCigar
            })
            {
                StitchedCigar = nifiedStitchedCigar,
                CigarDirections = new CigarDirection(nifiedStitchedDirections)
            };
            return mergedRead;
        }

        public Read GenerateConsensusReadForSimple(Read read1, Read read2, StitchingInfo stitchingInfo, bool isOutie)
        {
            _stitchedBases.Clear();
            _stitchedQualities.Clear();

            var startIndexInR1 = 0;
            var startIndexInR2 = 0;

            var r1PrefixClip = (int)read1.CigarData.GetPrefixClip();
            var r2PrefixClip = (int)read2.CigarData.GetPrefixClip();

            var r1SuffixClipEnd = (int)read1.CigarData.GetReadSpan();
            var r2SuffixClipEnd = (int)read2.CigarData.GetReadSpan();

            var r1SuffixClipBegin = r1SuffixClipEnd - read1.CigarData.GetSuffixClip();
            var r2SuffixClipBegin = r2SuffixClipEnd - read2.CigarData.GetSuffixClip();


            if (!_useSoftclippedBases)
            {
                if (r2PrefixClip == 0)
                {
                    // If we're not using softclipped bases to count toward stitching-ness, we need to fast-forward ahead of the sofctclip bases in R2
                    // That way, we essentially ignore the R2 softclipped bases and start from the "real" calls once we get to the Reverse-only region
                    startIndexInR2 += (int)read2.CigarData.GetPrefixClip();
                }
            }

            if (_ignoreProbeSoftclips)
            {
                if (isOutie)
                {
                    startIndexInR2 += stitchingInfo.IgnoredProbePrefixBases;
                }
                else if (r2PrefixClip == 0)
                {
                    startIndexInR1 += stitchingInfo.IgnoredProbePrefixBases;
                }
            }

            var r1Indexer = new ReadIndexer(startIndexInR1);
            var r2Indexer = new ReadIndexer(startIndexInR2);

            ReadIndexer forwardReadIndexer;
            ReadIndexer reverseReadIndexer;

            var read1Reverse = false;
            // Assumption is that exactly one read is forward and one read is reverse, and each component read is only one direction
            if (read1.SequencedBaseDirectionMap.First() == DirectionType.Forward)
            {
                forwardReadIndexer = r1Indexer;
                reverseReadIndexer = r2Indexer;
            }
            else
            {
                read1Reverse = true;
                forwardReadIndexer = r2Indexer;
                reverseReadIndexer = r1Indexer;
            }

            var r1SoftclipBeforeR2 = read2.ClipAdjustedPosition - read1.ClipAdjustedPosition;
            var r2SoftclipBeforeR1 = read1.ClipAdjustedPosition - read2.ClipAdjustedPosition;



            CigarDirectionExpander cigarDirectionExpander = new CigarDirectionExpander(stitchingInfo.StitchedDirections);
            for (CigarExtensions.CigarOpExpander cigarExpander = new CigarExtensions.CigarOpExpander(stitchingInfo.StitchedCigar);
                cigarExpander.IsNotEnd() && cigarDirectionExpander.IsNotEnd();
                cigarExpander.MoveNext(), cigarDirectionExpander.MoveNext())
            {
                var cigarType = cigarExpander.Current;
                var direction = cigarDirectionExpander.Current;

                if (cigarType == 'D') continue;

                var r1Index = r1Indexer.Index;
                var r2Index = r2Indexer.Index;

                if (r1SoftclipBeforeR2 > 0)
                {
                    if (r1Index == r1SoftclipBeforeR2 && !r2Indexer.StartedIndexing)
                    {
                        r2Indexer.StartIndexing();
                    }
                }
                else if (r2SoftclipBeforeR1 > 0)
                {
                    if (r2Index == r2SoftclipBeforeR1 && !r1Indexer.StartedIndexing)
                    {
                        r1Indexer.StartIndexing();
                    }
                }
                else
                {
                    r1Indexer.StartIndexing();
                    r2Indexer.StartIndexing();
                }

                // Start moving in read if needed
                switch (direction)
                {
                    case DirectionType.Forward:
                        if (!forwardReadIndexer.StartedIndexing)
                        {
                            forwardReadIndexer.StartIndexing();
                        }
                        break;
                    case DirectionType.Reverse:
                        if (!reverseReadIndexer.StartedIndexing)
                        {
                            reverseReadIndexer.StartIndexing();
                        }
                        break;
                    case DirectionType.Stitched:
                        if (!forwardReadIndexer.StartedIndexing)
                        {
                            forwardReadIndexer.StartIndexing();
                        }
                        if (!reverseReadIndexer.StartedIndexing)
                        {
                            reverseReadIndexer.StartIndexing();
                        }
                        break;
                }

                var forwardIndex = read1Reverse ? r2Index : r1Index;
                var reverseIndex = read1Reverse ? r1Index : r2Index;
                var forwardPrefixClip = read1Reverse ? r2PrefixClip : r1PrefixClip;
                var reversePrefixClip = read1Reverse ? r1PrefixClip : r2PrefixClip;
                var reverseSuffixClipEnd = read1Reverse ? r1SuffixClipEnd : r2SuffixClipEnd;
                var forwardSuffixClipEnd = read1Reverse ? r2SuffixClipEnd : r1SuffixClipEnd;
                var forwardSuffixClipBegin = read1Reverse ? r2SuffixClipBegin : r1SuffixClipBegin;
                var reverseSuffixClipBegin = read1Reverse ? r1SuffixClipBegin : r2SuffixClipBegin;

                // If R1 & R2 are both in prefix softclips, favor R2 as more "real" and skip over the R1 base
                if (forwardReadIndexer.StartedIndexing && forwardIndex >= 0 && forwardIndex < forwardPrefixClip)
                {
                    if (reverseReadIndexer.StartedIndexing && reverseIndex >= 0 && reverseIndex < reversePrefixClip)
                    {
                        direction = DirectionType.Reverse;
                        forwardReadIndexer.Increment();
                    }
                }

                // If R1 & R2 are both in suffix softclips, favor R1 as more "real" and skip over the R2 base
                if (reverseReadIndexer.StartedIndexing && reverseIndex >= reverseSuffixClipBegin && reverseIndex < reverseSuffixClipEnd)
                {
                    if (forwardReadIndexer.StartedIndexing && forwardIndex >= forwardSuffixClipBegin && forwardIndex < forwardSuffixClipEnd)
                    {
                        direction = DirectionType.Forward;
                        reverseReadIndexer.Increment();
                    }
                }


                if (r1Index >= 0 && r1Index < read1.BamAlignment.Bases.Length)
                {
                    r1Indexer.BaseAtIndex = read1.BamAlignment.Bases[r1Index];
                    r1Indexer.QualityAtIndex = read1.Qualities[r1Index];
                }
                else
                {
                    r1Indexer.BaseAtIndex = null;
                    r1Indexer.QualityAtIndex = null;
                }
                if (r2Index >= 0 && r2Index < read2.BamAlignment.Bases.Length)
                {
                    r2Indexer.BaseAtIndex = read2.BamAlignment.Bases[r2Index];
                    r2Indexer.QualityAtIndex = read2.Qualities[r2Index];
                }
                else
                {
                    r2Indexer.BaseAtIndex = null;
                    r2Indexer.QualityAtIndex = null;
                }

                switch (direction)
                {
                    case DirectionType.Forward:
                        if (forwardReadIndexer.BaseAtIndex == null)
                        {
                            throw new InvalidDataException("Forward base at index " + forwardReadIndexer.Index + " is null.");
                        }
                        _stitchedBases.Add((char)forwardReadIndexer.BaseAtIndex);
                        _stitchedQualities.Add((byte)forwardReadIndexer.QualityAtIndex);
                        forwardReadIndexer.Increment();
                        break;
                    case DirectionType.Reverse:
                        if (reverseReadIndexer.BaseAtIndex == null)
                        {
                            throw new InvalidDataException("Reverse base at index " + reverseReadIndexer.Index + " is null.");
                        }
                        _stitchedBases.Add((char)reverseReadIndexer.BaseAtIndex); // TODO - stringbuilder instead?
                        _stitchedQualities.Add((byte)reverseReadIndexer.QualityAtIndex);
                        reverseReadIndexer.Increment();
                        break;
                    case DirectionType.Stitched:
                        if (forwardReadIndexer.BaseAtIndex != null && reverseReadIndexer.BaseAtIndex != null)
                        {
                            if (forwardReadIndexer.BaseAtIndex == reverseReadIndexer.BaseAtIndex)
                            {
                                _stitchedBases.Add((char)forwardReadIndexer.BaseAtIndex);
                                var sumQuality = Convert.ToInt32((byte)forwardReadIndexer.QualityAtIndex) +
                                                       Convert.ToInt32((byte)reverseReadIndexer.QualityAtIndex);

                                var sticheredQuality = sumQuality > MaxBaseQuality ? MaxBaseQuality : sumQuality;

                                _stitchedQualities.Add((byte)sticheredQuality);
                            }
                            else //the bases disagree...
                            {
                                if (_nifyDisagreements)
                                {
                                    // we have disagreeing bases AND we chose to always Nify them
                                    _stitchedBases.Add('N');
                                    _stitchedQualities.Add(0);
                                }
                                else
                                {
                                    if ((byte)forwardReadIndexer.QualityAtIndex >= reverseReadIndexer.QualityAtIndex)
                                    // Original stitching implementation -- TODO, reconcile this with new reqs.
                                    {
                                        _stitchedBases.Add((char)forwardReadIndexer.BaseAtIndex);

                                        if (reverseReadIndexer.QualityAtIndex < _minBasecallQuality)
                                            _stitchedQualities.Add((byte)forwardReadIndexer.QualityAtIndex);
                                        else
                                            _stitchedQualities.Add(0);
                                        //this was a high Q disagreement, and dangerous! we will filter this base.
                                    }
                                    else
                                    //if ((byte)forwardReadIndexer.QualityAtIndex < reverseReadIndexer.QualityAtIndex) // Original stitching implementation 
                                    {
                                        _stitchedBases.Add((char)reverseReadIndexer.BaseAtIndex);
                                        if (forwardReadIndexer.QualityAtIndex < _minBasecallQuality)
                                            _stitchedQualities.Add((byte)reverseReadIndexer.QualityAtIndex);
                                        else
                                            _stitchedQualities.Add(0);
                                        //this was a high Q disagreement, and dangerous! we will filter this base.
                                    }
                                }
                            }
                        }

                        forwardReadIndexer.Increment();
                        reverseReadIndexer.Increment();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Validate stitched cigar
            var r2CigarLength = read2.CigarData.Cast<CigarOp>().Sum(op => (int)op.Length);
            var r1CigarLength = read1.CigarData.Cast<CigarOp>().Sum(op => (int)op.Length);

            var stitchedCigarLength = stitchingInfo.StitchedCigar.Cast<CigarOp>().Sum(op => (int)op.Length);
            var earliestStart = Math.Min(read1.ClipAdjustedPosition, read2.ClipAdjustedPosition);
            var latestEnd = Math.Max(read1.ClipAdjustedPosition + r1CigarLength, read2.ClipAdjustedPosition + r2CigarLength);
            //var latestEnd = Math.Max(read1.ClipAdjustedPosition + read1.CigarData.GetReadSpan(), read2.ClipAdjustedPosition + read2.CigarData+ stitchingInfo.InsertionAdjustment);

            if (stitchedCigarLength != (latestEnd - earliestStart))
            {
                // TODO what is really the point of this???
                if (_debug)
                {
                    Logger.WriteToLog(string.Format(
                        "Attempted stitched cigar {0} is not consistent with component reads {1}:{2} and {3}:{4}",
                        stitchingInfo.StitchedCigar, read1.Position, read1.CigarData, read2.Position,
                        read2.CigarData));
                }
                _statusCounter.AddDebugStatusCount("Attempted stitched cigar not consistent with component reads");
                //return null;
            }

            // TODO investigate if these are ever worth handling
            if (stitchingInfo.StitchedCigar.Count > 0 && stitchingInfo.StitchedCigar.GetReadSpan() != _stitchedBases.Count)
            {
                if (_debug)
                {
                    Logger.WriteToLog(string.Format("Invalid cigar '{0}': does not match length {1} of read ({2})", stitchingInfo.StitchedCigar,
                        _stitchedBases.Count, read1.Name));
                }

                _statusCounter.AddDebugStatusCount("Invalid cigar does not match length of read");
                return null;
            }

            var mergedRead = new Read(read1.Chromosome, new BamAlignment
            {
                Name = read1.Name,
                Bases = string.Join("", _stitchedBases),
                Position = Math.Min(read1.Position - 1, read2.Position - 1),
                Qualities = _stitchedQualities.ToArray(),
                CigarData = stitchingInfo.StitchedCigar
            })
            {
                StitchedCigar = stitchingInfo.StitchedCigar
            };

            return mergedRead;
        }

        public Read GenerateConsensusRead(Read read1, Read read2, StitchingInfo stitchingInfo, bool isOutie)
        {
            var mergedRead = new Read(read1.Chromosome, new BamAlignment
            {
                Name = read1.Name,
                Bases = string.Join("", stitchingInfo.StitchedBases),
                Position = Math.Min(read1.Position - 1, read2.Position - 1),
                Qualities = stitchingInfo.StitchedQualities.ToArray(),
                CigarData = stitchingInfo.StitchedCigar
            })
            {
                StitchedCigar = stitchingInfo.StitchedCigar
            };

            return mergedRead;
        }

    }
}