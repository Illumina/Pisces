using System;
using System.Linq;
using System.Text;
using Pisces.Interfaces;
using SequencingFiles;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Logic.Alignment
{
    public class StitcherConfig
    {
        public bool RequireXcTagToStitch { get; set; }
        public int MinimumBaseCallQuality { get; set; }
    }

    public abstract class BaseStitcher : IAlignmentStitcher
    {
        private readonly bool _nifyDisagreeingBases;
        protected int MinBaseCallQuality;

        protected BaseStitcher(int minBaseCallQuality, bool nifyDisagreeingBases = false)
        {
            MinBaseCallQuality = minBaseCallQuality;
            _nifyDisagreeingBases = nifyDisagreeingBases;
        }

        public abstract bool TryStitch(AlignmentSet pairedAlignment);

        // make sure individual read cigars make sense against stitched cigar
        protected static void ValidateCigar(CigarAlignment stitchedCigar, CigarAlignment read1Cigar, CigarAlignment read2Cigar)
        {
            if (!stitchedCigar.IsSupported())
                throw new Exception(String.Format("Unsupported cigar: {0}", stitchedCigar));

            ValidateCigar(stitchedCigar, read1Cigar);
            ValidateCigar(stitchedCigar.GetReverse(), read2Cigar.GetReverse());
        }

        // generate consensus read based on stitched cigar and previously determined overlap boundaries
        // todo try different consensus approaches
        protected virtual Read GenerateConsensus(Read read1, Read read2, CigarAlignment stitchedCigar, OverlapBoundary overlapBoundary)
        {
            var totalStitchedLength = (int)stitchedCigar.GetReadSpan();

            // init consensus
            var stitchedBasesSb = new StringBuilder();
            var stitchedQualities = new byte[totalStitchedLength];
            var directionMap = new DirectionType[totalStitchedLength];

            // take everything from read1 for positions before overlap
            stitchedBasesSb.Append(read1.Sequence.Substring(0, overlapBoundary.Read1.StartIndex));
            Array.Copy(read1.Qualities, stitchedQualities, overlapBoundary.Read1.StartIndex);

            for (var i = 0; i < overlapBoundary.Read1.StartIndex; i++)
                directionMap[i] = read1.DirectionMap[i];

            // determine consensus base + qscore in the overlap region
            for (int overlapIdx = 0; overlapIdx < overlapBoundary.OverlapLength; overlapIdx++)
            {
                var read1Index = overlapBoundary.Read1.StartIndex + overlapIdx;
                var read2Index = overlapBoundary.Read2.StartIndex + overlapIdx;

                var base1 = read1.Sequence[read1Index];
                var base2 = read2.Sequence[read2Index];
                var q1 = read1.Qualities[read1Index];
                var q2 = read2.Qualities[read2Index];

                directionMap[read1Index] = DirectionType.Stitched;

                if (base1 == base2)
                {
                    stitchedBasesSb.Append(base1);
                    stitchedQualities[read1Index] = Math.Max(q1, q2);
                }
                else
                {
                    if (_nifyDisagreeingBases || (q1 >= MinBaseCallQuality && q2 >= MinBaseCallQuality))
                    {
                        // we have two high-quality disagreeing bases
                        stitchedBasesSb.Append('N');
                        stitchedQualities[read1Index] = 0;
                    }
                    else
                    {
                        // take the higher quality base
                        stitchedBasesSb.Append(q1 < q2 ? base2 : base1);
                        stitchedQualities[read1Index] = Math.Max(q1, q2);
                    }
                }
            }

            // take everything from read2 for positions after overlap
            stitchedBasesSb.Append(read2.Sequence.Substring(overlapBoundary.Read2.EndIndex + 1));
            Array.Copy(read2.Qualities, overlapBoundary.Read2.EndIndex + 1,
                stitchedQualities, overlapBoundary.Read1.EndIndex + 1,
                read2.Sequence.Length - overlapBoundary.Read2.EndIndex - 1);

            for (var i = overlapBoundary.Read1.EndIndex + 1; i < directionMap.Length; i++)
                directionMap[i] = read2.DirectionMap[overlapBoundary.Read2.EndIndex + 1 + i - (overlapBoundary.Read1.EndIndex + 1)];

            var mergedRead = new Read(read1.Chromosome, new BamAlignment
            {
                Bases = stitchedBasesSb.ToString(),
                Position = read1.Position - 1,
                Qualities = stitchedQualities,
                CigarData = stitchedCigar
            })
            {
                DirectionMap = directionMap,
                StitchedCigar = stitchedCigar
            };
            
            return mergedRead;
        }


        private static void ValidateCigar(CigarAlignment stitchedCigar, CigarAlignment startCigar)
        {
            var mismatch = false;

            for (int i = 0; i < startCigar.Count - 1; i++)
            {
                var stitchedOp = stitchedCigar[i];
                var operation = startCigar[i];

                if (stitchedOp.Type != operation.Type || stitchedOp.Length != operation.Length)
                {
                    mismatch = true;
                    break;
                }
            }

            // check last one separately, mismatch if:
            // - same type but longer length than stitched
            // - different type and not soft clipped
            // - different type and soft clipped but stitched type is not I
            var lastStitchedOp = stitchedCigar[startCigar.Count - 1];
            var lastOperation = startCigar[startCigar.Count - 1];

            mismatch = mismatch || (lastStitchedOp.Type == lastOperation.Type && lastStitchedOp.Length < lastOperation.Length) ||
                       (lastStitchedOp.Type != lastOperation.Type && lastOperation.Type != 'S') ||
                       (lastStitchedOp.Type != lastOperation.Type && lastOperation.Type == 'S' && lastStitchedOp.Type != 'I');

            if (mismatch)
                throw new ApplicationException(String.Format("Unable to stitch: mismatch between stitched '{0}' and read '{1}' cigar", stitchedCigar, startCigar));
        }

        protected static int FindPosition(int[] cycleMap, int position)
        {
            for (int i = 0; i < cycleMap.Length; i++)
            {
                if (cycleMap[i] == position)
                    return i;
            }

            return -1;
        }




        public class OverlapBoundary
        {
            public ReadIndexBoundary Read1 { get; set; }
            public ReadIndexBoundary Read2 { get; set; }

            public int TotalStitchedLength { get; set; }
            public int OverlapLength { get; set; }
        }

        public struct ReadIndexBoundary
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
        }
    }

    public class BasicStitcher : BaseStitcher
    {
        public BasicStitcher(int minBaseCallQuality, bool nifyDisagreements = true) : base(minBaseCallQuality, nifyDisagreements)
        {
        }

        public OverlapBoundary GetOverlapBoundary(Read read1, Read read2, out bool unableToDetermineOverlap)
        {
            unableToDetermineOverlap = false;

            var read2PrefixInsLength = read2.CigarData.GetPrefixInsertionLength();
            var read1SuffixInsLength = read1.CigarData.GetSuffixInsertionLength();

            var overlapBoundary = new OverlapBoundary();

            // find anchor or read1 on read2
            var read1MaxPosition = read1.PositionMap.Max();
            var indexOfR2StartInR1 = FindPosition(read1.PositionMap, read2.Position);
            var indexOfR1MaxPosInR2 = FindPosition(read2.PositionMap, read1MaxPosition);

            // basic check for overlap
            if (read2.Position > read1MaxPosition + 1)
                return null;

            // grab a valid anchor and compute overlap from there
            // don't rely on position map once you have a valid anchor
            if (indexOfR2StartInR1 != -1)
            {
                // adjust for insertion at start of R2
                indexOfR2StartInR1 = Math.Max(0, indexOfR2StartInR1 - read2PrefixInsLength);

                var remainingR1Length = read1.ReadLength - (int)read1.CigarData.GetSuffixClip() - indexOfR2StartInR1;
                overlapBoundary.OverlapLength = Math.Min((int)read2.CigarData.GetReadSpanBetweenClippedEnds(), remainingR1Length);

                overlapBoundary.Read1 = new ReadIndexBoundary
                {
                    StartIndex = indexOfR2StartInR1,
                    EndIndex = indexOfR2StartInR1 + overlapBoundary.OverlapLength - 1
                };

                overlapBoundary.Read2 = new ReadIndexBoundary
                {
                    StartIndex = (int)read2.CigarData.GetPrefixClip(),
                    EndIndex = (int)read2.CigarData.GetPrefixClip() + overlapBoundary.OverlapLength - 1
                };
            }
            else if (indexOfR1MaxPosInR2 != -1)
            {
                // adjust for insertion at end of R1
                indexOfR1MaxPosInR2 = Math.Min(read2.PositionMap.Length, indexOfR1MaxPosInR2 + read1SuffixInsLength);

                var precedingR2Length = indexOfR1MaxPosInR2 - (int) read2.CigarData.GetPrefixClip() + 1;
                overlapBoundary.OverlapLength = Math.Min((int) read1.CigarData.GetReadSpanBetweenClippedEnds(),
                    precedingR2Length);

                overlapBoundary.Read1 = new ReadIndexBoundary
                {
                    StartIndex =
                        read1.ReadLength - (int) read1.CigarData.GetSuffixClip() - overlapBoundary.OverlapLength,
                    EndIndex = read1.ReadLength - (int) read1.CigarData.GetSuffixClip() - 1
                };

                overlapBoundary.Read2 = new ReadIndexBoundary
                {
                    StartIndex = indexOfR1MaxPosInR2 - overlapBoundary.OverlapLength + 1,
                    EndIndex = indexOfR1MaxPosInR2
                };
            }
            // try to rescue special case where both reads end in an insertion and only the insertion overlaps exactly
            // cannot rescue if insertions don't match without doing something more complicated like a local alignment
            else if (read2.Position == read1MaxPosition + 1)
            {
                if (read2PrefixInsLength == 0)
                    return null;

                if (read1SuffixInsLength == read2PrefixInsLength)
                {
                    overlapBoundary.OverlapLength = Math.Min(read1SuffixInsLength, read2PrefixInsLength);
                    overlapBoundary.Read1 = new ReadIndexBoundary
                    {
                        StartIndex =
                            read1.ReadLength - (int) read1.CigarData.GetSuffixClip() - overlapBoundary.OverlapLength,
                        EndIndex = read1.ReadLength - (int) read1.CigarData.GetSuffixClip() - 1
                    };

                    overlapBoundary.Read2 = new ReadIndexBoundary
                    {
                        StartIndex = 0,
                        EndIndex = overlapBoundary.OverlapLength - 1
                    };
                }
                else
                {
                    unableToDetermineOverlap = true;
                    return null;
                }
            }
            else
            {
                unableToDetermineOverlap = true;
                return null;
            }

            //overlapBoundary.R1ClippedEndIndex = read1.ReadLength - (int)read1.CigarData.GetSuffixClip() - 1;
            //overlapBoundary.R2ClippedStartIndex = (int)read2.CigarData.GetPrefixClip();
            //overlapBoundary.R1ClippedEndIndex - overlapBoundary.IndexOfR2StartInR1 + 1
            overlapBoundary.TotalStitchedLength = overlapBoundary.Read1.StartIndex +
                                                  overlapBoundary.OverlapLength +
                                                  (read2.ReadLength - overlapBoundary.Read2.EndIndex - 1);

            return overlapBoundary;
        }

        public override bool TryStitch(AlignmentSet set)
        {
            if (set.PartnerRead1 == null || set.PartnerRead2 == null)
                throw new ArgumentException("Set has missing read.");

            if (set.PartnerRead1.Chromosome != set.PartnerRead2.Chromosome)
                throw new ArgumentException("Partner reads are from different chromosomes.");

            // not available, stitch on our own
            // determine overlap boundaries
            bool unableToDetermineOverlap;
            var overlapBoundary = GetOverlapBoundary(set.PartnerRead1 as Read, set.PartnerRead2 as Read, out unableToDetermineOverlap);

            if (unableToDetermineOverlap)
            {
                return false;
            }
            else if (overlapBoundary == null)
            {
                // no overlap, add separately
                set.ReadsForProcessing.Add(set.PartnerRead1);
                set.ReadsForProcessing.Add(set.PartnerRead2);
            }
            else
            {
                CigarAlignment stitchedCigar;

                stitchedCigar = GetStitchedCigar(set, overlapBoundary);

                if (stitchedCigar == null) // there's an overlap but we can't figure out the cigar
                    return false;

                var mergedRead = GenerateConsensus(set.PartnerRead1 as Read, set.PartnerRead2 as Read, stitchedCigar, overlapBoundary);
                mergedRead.IsDuplex = set.PartnerRead1.IsDuplex || set.PartnerRead2.IsDuplex;
                set.ReadsForProcessing.Add(mergedRead);
            }

            return true;
        }

        private CigarAlignment GetStitchedCigar(AlignmentSet set, OverlapBoundary overlapBoundary)
        {
            // preferentially take XC tag if available
            if (((Read)set.PartnerRead1).StitchedCigar != null &&
                ((Read)set.PartnerRead2).StitchedCigar != null)
            {
                // make sure it corresponds to expected length
                var stitchedCigar = ((Read)set.PartnerRead1).StitchedCigar;
                if (stitchedCigar.GetReadSpan() == overlapBoundary.TotalStitchedLength)
                    return stitchedCigar;
            }

            return CalculateStitchedCigar(set.PartnerRead1 as Read, set.PartnerRead2 as Read, overlapBoundary);
        }

        // attempt to stitch reads according to where they align to the reference
        // this is a conservative approach that requires consensus between individual reads' cigars.  it is not the same approach as implemented in amplicon aligner
        // todo try different stitching approaches
        // todo also make this more understandable
        private CigarAlignment CalculateStitchedCigar(Read read1, Read read2, OverlapBoundary overlapBoundary)
        {
            // compare position maps between read1 and read2 in overlap region
            for (var i = 0; i < overlapBoundary.OverlapLength; i++)
            {
                if (read1.PositionMap[overlapBoundary.Read1.StartIndex + i] !=
                    read2.PositionMap[overlapBoundary.Read2.StartIndex + i])
                    return null;
            }

            // ---------------------------
            // assemble stitched cigar
            // - take cigar from read1 up to anchor, then from read2 at anchor and beyond.  this will take read2's cigar for any overlap.

            var stitchedCigar = read1.CigarData.GetTrimmed(overlapBoundary.Read1.StartIndex);
            for (var i = 0; i < read2.CigarData.Count; i++)
            {
                var operation = read2.CigarData[i];
                if (!(i == 0 && operation.Type == 'S'))
                    stitchedCigar.Add(new CigarOp(operation.Type, operation.Length));
            }

            stitchedCigar.Compress();

            //ValidateCigar(stitchedCigar, read1.CigarData, read2.CigarData);

            return stitchedCigar;
        }



    }
}