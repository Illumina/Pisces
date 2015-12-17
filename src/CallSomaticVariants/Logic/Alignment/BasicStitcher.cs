using System;
using System.Linq;
using System.Text;
using CallSomaticVariants.Infrastructure;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;
using SequencingFiles;

namespace CallSomaticVariants.Logic.Alignment
{
    public class StitcherConfig
    {
        public bool RequireXcTagToStitch { get; set; }
        public int MinimumBaseCallQuality { get; set; }
    }

    public abstract class BaseStitcher : IAlignmentStitcher
    {
        protected int _minBaseCallQuality;

        protected BaseStitcher(int minBaseCallQuality)
        {
            _minBaseCallQuality = minBaseCallQuality;
        }

        public abstract void TryStitch(AlignmentSet pairedAlignment);

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
        protected Read GenerateConsensus(Read read1, Read read2, CigarAlignment stitchedCigar, OverlapBoundary overlapBoundary)
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
                    if (q1 >= _minBaseCallQuality && q2 >= _minBaseCallQuality)
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

            var mergedRead = new Read(read1.Chromosome, new BamAlignment()
            {
                Bases = stitchedBasesSb.ToString(),
                Position = read1.Position - 1,
                Qualities = stitchedQualities,
                CigarData = stitchedCigar
            }, true)
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
        public BasicStitcher(int minBaseCallQuality) : base(minBaseCallQuality)
        {
        }

        public OverlapBoundary GetOverlapBoundary(Read read1, Read read2)
        {
            var read1ReferencePositions = read1.PositionMap.Where(p => p != -1).ToList();

            if (read2.Position < read1ReferencePositions.Min() || read2.Position > read1ReferencePositions.Max())
                return null; // no overlap

            var overlapBoundary = new OverlapBoundary();

            // find anchor or read1 on read2
            var read1MaxPosition = read1.PositionMap.Max();
            var indexOfR2StartInR1 = FindPosition(read1.PositionMap, read2.Position);
            var indexOfR1MaxPosInR2 = FindPosition(read2.PositionMap, read1MaxPosition);

            // grab a valid anchor and compute overlap from there
            // don't rely on position map once you have a valid anchor
            if (indexOfR2StartInR1 != -1)
            {
                var remainingR1Length = read1.ReadLength - (int)read1.CigarData.GetSuffixClip() - indexOfR2StartInR1;
                overlapBoundary.OverlapLength = Math.Min((int)read2.CigarData.GetReadSpanBetweenClippedEnds(), remainingR1Length);

                overlapBoundary.Read1 = new ReadIndexBoundary()
                {
                    StartIndex = indexOfR2StartInR1,
                    EndIndex = indexOfR2StartInR1 + overlapBoundary.OverlapLength - 1
                };

                overlapBoundary.Read2 = new ReadIndexBoundary()
                {
                    StartIndex = (int)read2.CigarData.GetPrefixClip(),
                    EndIndex = (int)read2.CigarData.GetPrefixClip() + overlapBoundary.OverlapLength - 1
                };
            }
            else if (indexOfR1MaxPosInR2 != -1)
            {
                var preceedingR2Length = indexOfR1MaxPosInR2 - (int)read2.CigarData.GetPrefixClip() + 1;
                overlapBoundary.OverlapLength = Math.Min((int)read1.CigarData.GetReadSpanBetweenClippedEnds(), preceedingR2Length);

                overlapBoundary.Read1 = new ReadIndexBoundary()
                {
                    StartIndex = read1.ReadLength - (int)read1.CigarData.GetSuffixClip() - overlapBoundary.OverlapLength,
                    EndIndex = read1.ReadLength - (int)read1.CigarData.GetSuffixClip() - 1
                };

                overlapBoundary.Read2 = new ReadIndexBoundary()
                {
                    StartIndex = indexOfR1MaxPosInR2 - overlapBoundary.OverlapLength + 1,
                    EndIndex = indexOfR1MaxPosInR2
                };
            }
            else
            {
                throw new Exception("Unable to find anchor between reads");
            }

            //overlapBoundary.R1ClippedEndIndex = read1.ReadLength - (int)read1.CigarData.GetSuffixClip() - 1;
            //overlapBoundary.R2ClippedStartIndex = (int)read2.CigarData.GetPrefixClip();
            //overlapBoundary.R1ClippedEndIndex - overlapBoundary.IndexOfR2StartInR1 + 1
            overlapBoundary.TotalStitchedLength = overlapBoundary.Read1.StartIndex +
                                                  overlapBoundary.OverlapLength +
                                                  (read2.ReadLength - overlapBoundary.Read2.EndIndex - 1);

            return overlapBoundary;
        }

        public override void TryStitch(AlignmentSet set)
        {
            if (set.PartnerRead1 == null || set.PartnerRead2 == null)
                throw new ArgumentException("Set has missing read.");

            if (set.PartnerRead1.Chromosome != set.PartnerRead2.Chromosome)
                throw new ArgumentException("Partner reads are from different chromosomes.");

            // not available, stitch on our own
            // determine overlap boundaries
            var overlapBoundary = GetOverlapBoundary(set.PartnerRead1, set.PartnerRead2);

            if (overlapBoundary == null)
            {
                // no overlap, add separately
                set.ReadsForProcessing.Add(set.PartnerRead1);
                set.ReadsForProcessing.Add(set.PartnerRead2);
            }
            else
            {
                CigarAlignment stitchedCigar;

                stitchedCigar = GetStitchedCigar(set, overlapBoundary);

                var mergedRead = GenerateConsensus(set.PartnerRead1, set.PartnerRead2, stitchedCigar, overlapBoundary);
                set.ReadsForProcessing.Add(mergedRead);
            }
        }

        private CigarAlignment GetStitchedCigar(AlignmentSet set, OverlapBoundary overlapBoundary)
        {
            // preferentially take XC tag if available
            if (set.PartnerRead1.StitchedCigar != null &&
                set.PartnerRead2.StitchedCigar != null)
            {
                // make sure it corresponds to expected length
                var stitchedCigar = set.PartnerRead1.StitchedCigar;
                if (stitchedCigar.GetReadSpan() == overlapBoundary.TotalStitchedLength)
                    return stitchedCigar;
            }

            return CalculateStitchedCigar(set.PartnerRead1, set.PartnerRead2, overlapBoundary);
        }

        // attempt to stitch reads according to where they align to the reference
        // this is a conservative approach that requires consensus between individual reads' cigars.  it is not the same approach as implemented in amplicon aligner
        private CigarAlignment CalculateStitchedCigar(Read read1, Read read2, OverlapBoundary overlapBoundary)
        {
            // compare position maps between read1 and read2 in overlap region
            for (var i = 0; i < overlapBoundary.OverlapLength; i++)
            {
                if (read1.PositionMap[overlapBoundary.Read1.StartIndex + i] != read2.PositionMap[overlapBoundary.Read2.StartIndex + i])
                    throw new ReadsNotStitchableException("Disagreement in read position maps");
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