using System;
using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Infrastructure;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic.Alignment;
using CallSomaticVariants.Models;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Alignment
{
    public static class StitcherTestHelpers
    {
        public static bool VerifyReadsEqual(Read baseRead, Read testRead)
        {
            return baseRead.Chromosome == testRead.Chromosome &&
                   baseRead.Position == testRead.Position &&
                   baseRead.PositionMap == testRead.PositionMap &&
                   baseRead.Sequence == testRead.Sequence &&
                   baseRead.Qualities == testRead.Qualities &&
                   baseRead.DirectionMap == testRead.DirectionMap;
        }

        public static void SetReadDirections(Read read, DirectionType directionType)
        {
            read.DirectionMap = Enumerable.Repeat(directionType, read.ReadLength).ToArray();
        }

        public static DirectionType[] BuildDirectionMap(IEnumerable<IEnumerable<DirectionType>> segments )
        {
            var readCoverageDirections = new List<DirectionType>();

            foreach (var segment in segments)
            {
                readCoverageDirections.AddRange(segment);
            }

            return readCoverageDirections.ToArray();
        }

        public static IEnumerable<DirectionType> BuildDirectionSegment(DirectionType directionType, int length)
        {
            var filledSegment = Enumerable.Repeat(directionType, length);
            return filledSegment;
        }

        public static void TestUnstitchableReads(Read read1, Read read2, int minQscore, Action<IEnumerable<Read>> assertions)
        {
            var alignmentSet = new AlignmentSet(read1, read2);
            var stitcher = GetStitcher(minQscore);
            stitcher.TryStitch(alignmentSet);

            Assert.Equal(2, alignmentSet.ReadsForProcessing.Count);

            assertions(alignmentSet.ReadsForProcessing);
        }

        public static void VerifyDirectionType(DirectionType[] expectedDirections, DirectionType[] actualDirections)
        {
            Assert.Equal(expectedDirections.Length, actualDirections.Length);
            for (int i = 0; i < expectedDirections.Length; i++)
                Assert.Equal(expectedDirections[i], actualDirections[i]);
        }
     
        public static BaseStitcher GetStitcher(int minBaseCallQuality, bool xcStitcher = false)
        {
            if (xcStitcher)
            {
                return new XCStitcher(minBaseCallQuality);
            }
            return new BasicStitcher(minBaseCallQuality);
        }

        public static void CompareQuality(byte[] q1, byte[] q2)
        {
            Assert.Equal(q1.Length, q2.Length);
            for (int i = 0; i < q1.Length; i++)
                Assert.Equal(q1[i], q2[i]);
        }

        public static AlignmentSet GetOverlappingReadSet(bool withXCTag = false)
        {
            var read1 = TestHelper.CreateRead("chr1", "ATCGATCG", 12345, new CigarAlignment("8M"), qualityForAll:30);

            var read2_overlap = TestHelper.CreateRead("chr1", "ATCGTT", 12349, new CigarAlignment("6M"), qualityForAll:30);

            return new AlignmentSet(read1, read2_overlap);
        }

        public static Read GetMergedRead(AlignmentSet alignmentSet)
        {
            Assert.Equal(1,alignmentSet.ReadsForProcessing.Count);
            var mergedRead = alignmentSet.ReadsForProcessing.First();
            return mergedRead;
        }

        public static void TryStitchAndAssertFailed(IAlignmentStitcher stitcher, AlignmentSet alignmentSet)
        {
            Assert.Throws<ReadsNotStitchableException>(()=>stitcher.TryStitch(alignmentSet));
        }

        public static void AssertReadsNotStitched(AlignmentSet alignmentSet, Read read1, Read read2)
        {
            AssertReadsNotProcessed(alignmentSet);
        }

        private static void AssertReadsProcessedSeparately(AlignmentSet alignmentSet, Read read1, Read read2)
        {
            Assert.Equal(2, alignmentSet.ReadsForProcessing.Count());
            Assert.Contains(read1, alignmentSet.ReadsForProcessing);
            Assert.Contains(read2, alignmentSet.ReadsForProcessing);
            //Assert.True(alignmentSet.ReadsForProcessing.All(x=>x.StitchedCigarString==null)); // this doesn't work in the case of disagreeing cigar strings causing unstitchability
        }

        private static void AssertReadsNotProcessed(AlignmentSet alignmentSet)
        {
            Assert.True(!alignmentSet.ReadsForProcessing.Any());
        }


    }
}