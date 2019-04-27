using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Alignment.Domain.Sequencing;
using TestUtilities;
using Xunit;

namespace StitchingLogic.Tests
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
                   baseRead.SequencedBaseDirectionMap == testRead.SequencedBaseDirectionMap;
        }

        public static void SetReadDirections(Read read, DirectionType directionType)
        {
            read.SequencedBaseDirectionMap = Enumerable.Repeat(directionType, read.ReadLength).ToArray();
        }

        public static DirectionType[] BuildDirectionMap(IEnumerable<IEnumerable<DirectionType>> segments)
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

        public static void TestUnstitchableReads(Read read1, Read read2, int minQscore, Action<IEnumerable<Read>> assertions, bool useSoftclippedBases = false)
        {
            var alignmentSet = new AlignmentSet(read1, read2);
            var stitcher = GetStitcher(minQscore, useSoftclippedBases: useSoftclippedBases);
            stitcher.TryStitch(alignmentSet);

            Assert.Equal(2, alignmentSet.ReadsForProcessing.Count);

            assertions(alignmentSet.ReadsForProcessing.Select(r => r as Read).ToList());
        }

        public static void VerifyDirectionType(DirectionType[] expectedDirections, DirectionType[] actualDirections)
        {
            Assert.Equal(expectedDirections.Length, actualDirections.Length);
            for (int i = 0; i < expectedDirections.Length; i++)
                Assert.Equal(expectedDirections[i], actualDirections[i]);
        }

        public static IAlignmentStitcher GetStitcher(int minBaseCallQuality, bool xcStitcher = false, bool nifyDisagreements = false, bool useSoftclippedBases = true, bool ignoreProbeSoftclips = true, uint minMapQuality = 20, bool DontStitchHomopolymerBridge = false)
        {
            return new BasicStitcher(minBaseCallQuality, nifyDisagreements: nifyDisagreements, useSoftclippedBases: useSoftclippedBases, ignoreProbeSoftclips: ignoreProbeSoftclips, minMapQuality: minMapQuality, dontStitchHomopolymerBridge: DontStitchHomopolymerBridge);
        }

        public static void CompareQuality(byte[] q1, byte[] q2)
        {
            Assert.Equal(q1.Length, q2.Length);
            for (int i = 0; i < q1.Length; i++)
                Assert.Equal(q1[i], q2[i]);
        }

        public static AlignmentSet GetOverlappingReadSet(bool withXCTag = false)
        {
            var read1 = ReadTestHelper.CreateRead("chr1", "ATCGATCG", 12345, new CigarAlignment("8M"), qualityForAll: 30);

            var read2_overlap = ReadTestHelper.CreateRead("chr1", "ATCGTT", 12349, new CigarAlignment("6M"), qualityForAll: 30);

            return new AlignmentSet(read1, read2_overlap);
        }

        public static Read GetMergedRead(AlignmentSet alignmentSet)
        {
            Assert.Equal(1, alignmentSet.ReadsForProcessing.Count);
            var mergedRead = alignmentSet.ReadsForProcessing.First() as Read;
            return mergedRead;
        }

        public static void TryStitchAndAssertFailed(IAlignmentStitcher stitcher, AlignmentSet alignmentSet)
        {
            Assert.False(stitcher.TryStitch(alignmentSet).Stitched);
        }

        public static void TryStitchAndAssertAddedSeparately(IAlignmentStitcher stitcher, AlignmentSet alignmentSet)
        {
            Assert.True(stitcher.TryStitch(alignmentSet).Stitched);
            Assert.True(alignmentSet.ReadsForProcessing.Contains(alignmentSet.PartnerRead1));
            Assert.True(alignmentSet.ReadsForProcessing.Contains(alignmentSet.PartnerRead2));
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