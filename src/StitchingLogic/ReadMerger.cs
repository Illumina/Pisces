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
    public class ReadMerger
    {
        private bool _useSoftclippedBases;
        private bool _allowRescuedInsertionBaseDisagreement;
        private ReadStatusCounter _statusCounter;
        private bool _debug;
        private readonly bool _ignoreProbeSoftclips;
        private bool _nifyDisagreements;
        private int _minBasecallQuality;

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

        public Read GenerateConsensusRead(Read read1, Read read2, StitchingInfo stitchingInfo, bool isOutie)
        {
            var stitchedBases = new List<char>();
            var stitchedQualities = new List<byte>();

            var expandedDirections = stitchingInfo.StitchedDirections.Expand();
            var expandedCigar = stitchingInfo.StitchedCigar.Expand();

            var startIndexInR1 = 0;
            var startIndexInR2 = 0;

            if (!_useSoftclippedBases)
            {
                // If we're not using softclipped bases to count toward stitching-ness, we need to fast-forward ahead of the sofctclip bases in R2
                // That way, we essentially ignore the R2 softclipped bases and start from the "real" calls once we get to the Reverse-only region
                startIndexInR2 += (int)read2.CigarData.GetPrefixClip();
            }

            if (_ignoreProbeSoftclips)
            {
                if (isOutie)
                {
                    startIndexInR2 += stitchingInfo.IgnoredProbePrefixBases;
                }
                else
                {
                    startIndexInR1 += stitchingInfo.IgnoredProbePrefixBases;
                }
            }

            var r1Indexer = new ReadIndexer(startIndexInR1);
            var r2Indexer = new ReadIndexer(startIndexInR2);

            ReadIndexer forwardReadIndexer;
            ReadIndexer reverseReadIndexer;

            // Assumption is that exactly one read is forward and one read is reverse, and each component read is only one direction
            if (read1.SequencedBaseDirectionMap.First() == DirectionType.Forward)
            {
                forwardReadIndexer = r1Indexer;
                reverseReadIndexer = r2Indexer;
            }
            else
            {
                forwardReadIndexer = r2Indexer;
                reverseReadIndexer = r1Indexer;
            }

            for (var i = 0; i < expandedCigar.Count; i++)
            {
                var cigarOp = expandedCigar[i];
                var direction = expandedDirections[i];

                if (cigarOp.Type == 'D') continue;

                var r1Index = r1Indexer.Index;
                var r2Index = r2Indexer.Index;

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
                            throw new Exception("Forward base at index " + forwardReadIndexer.Index + " is null.");
                        }
                        stitchedBases.Add((char)forwardReadIndexer.BaseAtIndex);
                        stitchedQualities.Add((byte)forwardReadIndexer.QualityAtIndex);
                        forwardReadIndexer.Increment();
                        break;
                    case DirectionType.Reverse:
                        if (reverseReadIndexer.BaseAtIndex == null)
                        {
                            throw new Exception("Reverse base at index " + reverseReadIndexer.Index + " is null.");
                        }
                        stitchedBases.Add((char)reverseReadIndexer.BaseAtIndex); // TODO - stringbuilder instead?
                        stitchedQualities.Add((byte)reverseReadIndexer.QualityAtIndex);
                        reverseReadIndexer.Increment();
                        break;
                    case DirectionType.Stitched:
                        if (forwardReadIndexer.BaseAtIndex != null && reverseReadIndexer.BaseAtIndex != null)
                        {
                            if (forwardReadIndexer.BaseAtIndex == reverseReadIndexer.BaseAtIndex)
                            {
                                stitchedBases.Add((char)forwardReadIndexer.BaseAtIndex);
                                var sticheredQuality = Convert.ToInt32((byte)forwardReadIndexer.QualityAtIndex) +
                                                       Convert.ToInt32((byte)reverseReadIndexer.QualityAtIndex);

                                stitchedQualities.Add((byte)sticheredQuality);
                            }
                            else //the bases disagree...
                            {
                                if (_nifyDisagreements)
                                {
                                    // we have disagreeing bases AND we chose to always Nify them
                                    stitchedBases.Add('N');
                                    stitchedQualities.Add(0);
                                }
                                else
                                {
                                    if ((byte)forwardReadIndexer.QualityAtIndex >= reverseReadIndexer.QualityAtIndex)
                                        // Original stitching implementation -- TODO, reconcile this with new reqs.
                                    {
                                        stitchedBases.Add((char)forwardReadIndexer.BaseAtIndex);

                                        if (reverseReadIndexer.QualityAtIndex < _minBasecallQuality)
                                            stitchedQualities.Add((byte)forwardReadIndexer.QualityAtIndex);
                                        else
                                            stitchedQualities.Add(0);
                                        //this was a high Q disagreement, and dangerous! we will filter this base.
                                    }
                                    else
                                    //if ((byte)forwardReadIndexer.QualityAtIndex < reverseReadIndexer.QualityAtIndex) // Original stitching implementation 
                                    {
                                        stitchedBases.Add((char)reverseReadIndexer.BaseAtIndex);
                                        if (forwardReadIndexer.QualityAtIndex < _minBasecallQuality)
                                            stitchedQualities.Add((byte)reverseReadIndexer.QualityAtIndex);
                                        else
                                            stitchedQualities.Add(0);
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
            if (stitchingInfo.StitchedCigar.Count > 0 && stitchingInfo.StitchedCigar.GetReadSpan() != stitchedBases.Count)
            {
                if (_debug)
                {
                    Logger.WriteToLog(string.Format("Invalid cigar '{0}': does not match length {1} of read ({2})", stitchingInfo.StitchedCigar,
                        stitchedBases.Count, read1.Name));
                }

                _statusCounter.AddDebugStatusCount("Invalid cigar does not match length of read");
                return null;
            }

            var mergedRead = new Read(read1.Chromosome, new BamAlignment
            {
                Name = read1.Name,
                Bases = string.Join("", stitchedBases),
                Position = Math.Min(read1.Position - 1, read2.Position - 1),
                Qualities = stitchedQualities.ToArray(),
                CigarData = stitchingInfo.StitchedCigar
            })
            {
                StitchedCigar = stitchingInfo.StitchedCigar,
                CigarDirections = new CigarDirection(stitchingInfo.StitchedDirections.ToString())
            };




            return mergedRead;
        }


    }
}