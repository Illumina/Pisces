using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using Pisces.Domain.Models;
using Pisces.Domain.Tests;
using Pisces.Domain.Types;
using Alignment.Domain.Sequencing;
using Xunit;

namespace StitchingLogic.Tests
{
    //StitchedReadsTests copied almost directly from old Pisces
    public class OriginalSVCStitcherScenarios
    {
        [Fact]
        public void TryStitch_MergeReadsSmall()
        {
            //Migrated from old Pisces: Originally called Pisces_MergeReadsSmall

            //test1: happy path

            //0 1 2 3 4 5 6 7 8 9
            //- C A T A T
            //- - - - A T A G G

            var read1 = DomainTestHelper.CreateRead("chr1", "CATAT", 1, new CigarAlignment("5M"), new byte[] { 1, 2, 3, 4, 5 }, 4);
            StitcherTestHelpers.SetReadDirections(read1, DirectionType.Forward);

            var read2 = DomainTestHelper.CreateRead("chr1", "ATAGG", 4, new CigarAlignment("5M"), new byte[] { 1, 20, 30, 40, 50 }, 1);
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            var alignmentSet = new AlignmentSet(read1, read2);
            var stitcher = StitcherTestHelpers.GetStitcher(10, nifyDisagreements: false);
            stitcher.TryStitch(alignmentSet);

            TestSuccesfullyStitchedRead(read1, read2, 0, "8M", (mergedRead) =>
            {
                Assert.Equal(mergedRead.Sequence, "CATATAGG");
                StitcherTestHelpers.CompareQuality(new byte[] { 1, 2, 3, 5, 25, 30, 40, 50 }, mergedRead.Qualities);
                var expectedDirections = StitcherTestHelpers.BuildDirectionMap(new List<IEnumerable<DirectionType>>
                {
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Forward, 3),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Stitched, 2),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Reverse, 3)
                });
                StitcherTestHelpers.VerifyDirectionType(expectedDirections, mergedRead.CigarDirections.Expand().ToArray());
            });

            //test2: different bases, one with low Q

            //0 1 2 3 4 5 6 7 8 9
            //- C A T A G
            //- - - - A T A G G

            read1 = DomainTestHelper.CreateRead("chr1", "CATAG", 1, new CigarAlignment("5M"), new byte[] { 1, 2, 3, 4, 5 }, 4);
            StitcherTestHelpers.SetReadDirections(read1, DirectionType.Reverse);

            read2 = DomainTestHelper.CreateRead("chr1", "ATAGG", 4, new CigarAlignment("5M"), new byte[] { 1, 20, 30, 40, 50 },
                1);
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Forward);


            TestSuccesfullyStitchedRead(read1, read2, 10, "8M", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);

                Assert.Equal("CATATAGG", mergedRead.Sequence);
                StitcherTestHelpers.CompareQuality(new byte[] { 1, 2, 3, 5, 20, 30, 40, 50 }, mergedRead.Qualities);

                var expectedDirections = StitcherTestHelpers.BuildDirectionMap(new List<IEnumerable<DirectionType>>
                {
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Reverse, 3),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Stitched, 2),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Forward, 3)
                });
                StitcherTestHelpers.VerifyDirectionType(expectedDirections, mergedRead.CigarDirections.Expand().ToArray());
            });

            //test3: different bases, both with high Q

            //0 1 2 3 4 5 6 7 8 9
            //- C A T A G
            //- - - - A T A G G

            read1 = DomainTestHelper.CreateRead("chr1", "CATAG", 1, new CigarAlignment("5M"), new byte[] { 100, 200, 200, 200, 200 }, 4);
            read2 = DomainTestHelper.CreateRead("chr1", "ATAGG", 4, new CigarAlignment("5M"), new byte[] { 1, 20, 30, 40, 50 }, 1);

            StitcherTestHelpers.SetReadDirections(read1, DirectionType.Forward);
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            TestSuccesfullyStitchedRead(read1, read2, 10, "8M", (mergedRead)  =>
            {
                Assert.NotNull(mergedRead);

                Assert.Equal(mergedRead.Sequence, "CATANAGG");

                StitcherTestHelpers.CompareQuality(new byte[] { 100, 200, 200, 201, 0, 30, 40, 50 }, mergedRead.Qualities);
                var expectedDirections = StitcherTestHelpers.BuildDirectionMap(new List<IEnumerable<DirectionType>>
                {
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Forward, 3),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Stitched, 2),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Reverse, 3)
                });
                StitcherTestHelpers.VerifyDirectionType(expectedDirections, mergedRead.CigarDirections.Expand().ToArray());

            }, true);
        }

        [Fact]
        public void TryStitch_SoftclipScenarios()
        {
            //Migrated from old Pisces: Originally called Pisces_MergeRead2First
            var sequence =
                "GG"+"GGCCACGCGGGGAGCAGCCTCTGGCATTCTGGGAGCTTCATCTGGACCTGGGTCTTCAGTGAACCATTGTTCAATATCGTCCGGGGACAGCATCAAATCATCCATTGCTTGGGACGGCAAGG"+"GGGACTGTAGATGGGTGAAAAGAGCA";

            var read1 = DomainTestHelper.CreateRead("chr1",
                sequence,
                7579464,
                new CigarAlignment("2S122M26S"),
                Enumerable.Repeat((byte)30, sequence.Length).ToArray(),
                7579464);
            StitcherTestHelpers.SetReadDirections(read1, DirectionType.Forward);

            sequence =
                "GTGTAGGAGCTGCTGGTGCAGG"+"GGCCACGCGGGGAGCAGCCTCTGGCATTCTGGGAGCTTCATCTGGACCTGGGTCTTCAGTGAACAATTGTTCAATATCGTCCGGGGCCAGCATCAAATCATCCATTGCTTGGGACGGCAAGG"+"GGGACT";
            var read2 = DomainTestHelper.CreateRead("chr1",
                sequence,
                7579464,
                new CigarAlignment("22S122M6S"),
                Enumerable.Repeat((byte)30, sequence.Length).ToArray(),
                7579464);
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            string expectedWithNify =
                "GTGTAGGAGCTGCTGGTGCAGG"+"GGCCACGCGGGGAGCAGCCTCTGGCATTCTGGGAGCTTCATCTGGACCTGGGTCTTCAGTGAACNATTGTTCAATATCGTCCGGGGNCAGCATCAAATCATCCATTGCTTGGGACGGCAAGG"+"GGGACTGTAGATGGGTGAAAAGAGCA";

            string expectedWithNoNify =
                "GTGTAGGAGCTGCTGGTGCAGG"+"GGCCACGCGGGGAGCAGCCTCTGGCATTCTGGGAGCTTCATCTGGACCTGGGTCTTCAGTGAACCATTGTTCAATATCGTCCGGGGACAGCATCAAATCATCCATTGCTTGGGACGGCAAGG"+"GGGACTGTAGATGGGTGAAAAGAGCA";
            // both reads have the same reference position, but read2 really starts earlier
            // make sure we behave properly
            TestSuccesfullyStitchedRead(read1, read2, 0, "22S122M26S", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);
                Assert.Equal("22R122S26F", mergedRead.CigarDirections.ToString());
                Assert.Equal(expectedWithNify, mergedRead.Sequence);
            },  nifyDisagreements: true);

            // Test with allowing probe softclips to merge
            TestSuccesfullyStitchedRead(read1, read2, 0, "22S122M26S", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);
                Assert.Equal("20R130S20F", mergedRead.CigarDirections.ToString());
                Assert.Equal(expectedWithNify, mergedRead.Sequence);
            }, nifyDisagreements: true, ignoreProbeSoftclips: false);

            //here, the Q scores where we disagree are equal (30) so we give R1 the preference (R1 usually being better than R2, if we are forced to pick one)
            TestSuccesfullyStitchedRead(read1, read2, 0, "22S122M26S", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);
                Assert.Equal(expectedWithNoNify, mergedRead.Sequence);
            }, nifyDisagreements: false);
        }

        [Fact]
        public void Pisces_MergeBugExample()
        {
            //Migrated from old Pisces: Originally called Pisces_MergeBugExample

            //test1: happy path

            //0 1 2 3 4 5 6 7 8 9
            //- C A T A T
            //- - - - A T A G G

            var read1 = DomainTestHelper.CreateRead("chr1",
                "TAAAGGTTTTGCTATCGGCATGCCAGTGTGCGAATTTGATATGGTTAAAGATCCAGAAGTACAGGACTTCCGCAGAAATATTTTGAACGTTTGTAAAGAA",
                178917497,
                new CigarAlignment("25S75M"),
                new byte[]
                {
                    27, 28, 11, 28, 27, 29, 20, 20, 31, 31, 31, 31, 27, 27, 32, 31, 29, 34, 34, 29, 34, 11, 12, 12, 23, 12,
                    23, 12, 23, 32, 13, 22, 10, 20, 10, 32, 36, 34, 28, 31, 13, 13, 24, 32, 24, 13, 24, 32, 31, 37, 36,
                    12, 31, 12, 12, 12, 13, 12, 20, 12, 12, 32, 28, 12, 12, 20, 10, 20, 32, 20, 32, 10, 10, 20, 9, 9, 20,
                    36, 12, 12, 12, 34, 12, 12, 23, 27, 32, 12, 23, 23, 23, 12, 20, 10, 11, 11, 28, 20, 34, 10
                },
                178917546);

            var read2 = DomainTestHelper.CreateRead("chr1",
                "GAAATATTCTGAACGTTTGTAAAGAAGCTGTGGATCTTAGGGACCTCAATTCACCTCATAGTAGAACAATGTATGTCTATCCTCCAAATGTAGAATCTTC",
                178917546,
                new CigarAlignment("71M29S"),
                new byte[]
                {
                    36, 33, 37, 37, 32, 14, 33, 36, 34, 32, 36, 23, 11, 20, 30, 35, 37, 35, 28, 38, 33, 30, 32, 12, 35, 39,
                    37, 37, 36, 32, 32, 23, 14, 14, 32, 32, 37, 32, 23, 12, 30, 22, 23, 12, 32, 32, 14, 32, 15, 34, 30,
                    22, 14, 36, 30, 34, 31, 39, 39, 39, 38, 39, 39, 38, 34, 36, 30, 34, 34, 30, 34, 34, 34, 32, 32, 33,
                    34, 37, 37, 31, 36, 37, 30, 37, 33, 30, 33, 31, 33, 33, 33, 33, 33, 33, 33, 30, 30, 30, 30, 30
                },
                178917497);

            //expected overlap
            //"TAAAG GTTTT GCTAT CGGCA TGCCA GTGTG CGAAT TTGAT ATGGT TAAAG ATCCA GAAGT ACAGG ACTTC CGCAG AAATA TTTTG AACGT TTGTA AAGAA";
            //                                                                                        "G AAATA TTCTG AACGT TTGTA AAGAA GCTGT GGATCTTAGGGACCTCAATTCACCTCATAGTAGAACAATGTATGTCTATCCTCCAAATGTAGAATCTTC";
            // 0     5     10    15    20    25    30    35    40    45    50    55    60     65   70    75      


            TestSuccesfullyStitchedRead(read2, read1, 0, "25S120M29S", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);

                Assert.Equal(mergedRead.Sequence, "TAAAGGTTTTGCTATCGGCATGCCAGTGTGCGAATTTGATATGGTTAAAGATCCAGAAGTACAGGACTTCCGCAGAAATATTNTGAACGTTTGTAAAGAAGCTGTGGATCTTAGGGACCTCAATTCACCTCATAGTAGAACAATGTATGTCTATCCTCCAAATGTAGAATCTTC");
                Assert.Equal(mergedRead.Qualities[0], (byte)27);
                Assert.Equal(mergedRead.Qualities[1], (byte)28);
                Assert.Equal(mergedRead.Qualities[2], (byte)11);

                //overlap is "G AAATA TTTTG AACGT TTGTA AAGAA", string at index 74
                byte r1 = read1.Qualities[74];
                byte r2 = read2.Qualities[0];
                Assert.Equal(mergedRead.Qualities[74], (byte) (Convert.ToInt32(r1) + Convert.ToInt32(r2)));
            }, true);


        }

        [Fact]
        public void Pisces_MergeReadsWithDeletionTests()
        {
            //Migrated from old Pisces: Originally called Pisces_MergeReadsWithDeletionTests

            var read1 = DomainTestHelper.CreateRead("chr1",
                "GAAAATGTGCAGAAGAGGATAGGCAGAAACTCAAAAAACATATAGACAATAACACCAGCACTCCTCCAAATTGCCCAATACTATATACTAAGATTTGTAA",
                115251051,
                new CigarAlignment("25S75M"),
                new byte[100],
                110);

            var read2 = DomainTestHelper.CreateRead("chr1",
                "CCAAATTGCCCAATACTATATACTAAGATTTGTAATTATGCCAAGAAACCATATGCTCACCTTGTTACATCACACATGGCAATCCCATACAACCCTGAGT",
                115251091,
                new CigarAlignment("70M3D5M25S"),
                new byte[100],
                88);

            var action = new Action<Read>((mergedRead1) =>
            {
                Assert.NotNull(mergedRead1);
                Assert.Equal(mergedRead1.Sequence,
                    "GAAAATGTGCAGAAGAGGATAGGCAGAAACTCAAAAAACATATAGACAATAACACCAGCACTCCTCCAAATTGCCCAATACTATATACTAAGATTTGTAATTATGCCAAGAAACCATATGCTCACCTTGTTACATCACACATGGCAATCCCATACAACCCTGAGT");
            });

            TestSuccesfullyStitchedRead(read1, read2, 0, "25S110M3D5M25S", action);


            read1 = DomainTestHelper.CreateRead("chr1",
                "GAAAATGTGCAGAAGAGGATAGGCAGAAACTCAAAAAACATATAGACAATAACACCAGCACTCCTCCAAATTGCCCAATACTATATACTAAGATTTGTAA",
                115251051,
                new CigarAlignment("25S75M"),
                new byte[100],
                110);

            read2 = DomainTestHelper.CreateRead("chr1",
                "CCAAATTGCCCAATACTATATACTAAGATTTGTAATTATGCCAAGAAACCATATGCTCACCTTGTTACATCACACATGGCAATCCCATACAACCCTGAGT",
                115251091,
                new CigarAlignment("70M3D5M25S"),
                new byte[100],
                88);

            TestSuccesfullyStitchedRead(read2, read1, 0, "25S110M3D5M25S", action);
        }

        [Fact]
        public void Pisces_MergeReadsWithInsertionTests()
        {
            //Migrated from old Pisces: Originally called Pisces_MergeReadsWithInsertionTests

            var read1 = DomainTestHelper.CreateRead("chr1",
                "GAAAATGTGCAGAAGAGGATAGGCAGAAACTCAAAAAAACATATAGACAATAACACCAGCACTCCTCCAAATTGCCCAATACTATATACTAAGATTTGTA",
                115251051,
                new CigarAlignment("25S7M1I67M"),
                new byte[100],
                110);

            var read2 = DomainTestHelper.CreateRead("chr1",
                "AATTGCCCAATACTATATACTAAGATTTGTAATTATGCCAAGAAACCATATGCTCACCTTGTTACATCACCACACATGGCAATCCCATACAACCCTGAGT",
                115251094,
                new CigarAlignment("75M25S"),
                new byte[100],
                88);

            TestSuccesfullyStitchedRead(read1, read2, 0, "25S7M1I111M25S", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);
                Assert.Equal(mergedRead.Sequence,
                    "GAAAATGTGCAGAAGAGGATAGGCAGAAACTCAAAAAAACATATAGACAATAACACCAGCACTCCTCCAAATTGCCCAATACTATATACTAAGATTTGTAATTATGCCAAGAAACCATATGCTCACCTTGTTACATCACCACACATGGCAATCCCATACAACCCTGAGT");
            });
        }


        //[Fact] //TODO 
        //This is currently expected to fail. 
        //Because we now allow to stitch between softclips.
        //We should adjust this this unit test, or otherwise clarify the expectation.
        public void Pisces_MergeReadsWithInsertion_BoundaryTests()
        {
            //Migrated from old Pisces: Originally called Pisces_MergeReadsWithInsertion_BoundaryTests

            // insertion at edge of read

            //0 1 2 3 - - - 4 5 6 7 8 9
            //- C A T A T A G G
            //- - - - A T A G G T A A

            var read1 = DomainTestHelper.CreateRead("chr1",
                "CATATAGG",
                1,
                new CigarAlignment("3M3I2M"),
                new byte[8],
                4);

            var read2 = DomainTestHelper.CreateRead("chr1",
                "ATAGGTAA",
                4,
                new CigarAlignment("3S5M"),
                new byte[8],
                1);

            TestSuccesfullyStitchedRead(read1, read2, 0, "3M3I5M", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);
                Assert.Equal(mergedRead.Sequence, "CATATAGGTAA");
            });


            //0 1 2 3 - - - 4 5 6 7 8 9
            //- C A T A T A G G
            //- - - - - T A G G T A A 

            read1 = DomainTestHelper.CreateRead("chr1", "CATATAGG", 1, new CigarAlignment("3M3I2M"),
                new byte[8], 4);

            read2 = DomainTestHelper.CreateRead("chr1", "TAGGTAA", 4, new CigarAlignment("2S5M"),
                new byte[8], 1);

            TestSuccesfullyStitchedRead(read1, read2, 0, "3M3I5M", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);
                Assert.Equal(mergedRead.Sequence, "CATATAGGTAA");
            });

            // TODO - Currently, we are allowing stitching over softclips. Should we really disallow this?
            // TJD: Yes. But lets explictly turn of "usesoftclippedreads" for this test.
            // GB: I am now having trouble understanding why stitching should fail here. I believe UseSoftclippedReads just refers to whether the softclips can contribute to direction.
            //0 1 2 3 - - - 4 5 6 7 8 9
            //- C A T A T A G G
            //- - - - - T A G G T A A  -- Looks like this if we knew it was an insertion...
            //- - T A G G T A A  -- Really looks like this... 

            read1 = DomainTestHelper.CreateRead("chr1", "CATATAGG", 1, new CigarAlignment("3M5S"),
                new byte[8], 4);

            read2 = DomainTestHelper.CreateRead("chr1", "TAGGTAA", 4, new CigarAlignment("2S5M"),
                new byte[8], 1);

            // Allow using softclipped bases: should take the CAT and the GGTAA
            TestSuccesfullyStitchedRead(read1, read2, 0, "8M", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);
                Assert.Equal("1F7S", mergedRead.CigarDirections.ToString());
                Assert.Equal("CATATAGG", mergedRead.Sequence); // For stitched sites (all but the first), if disagreement, takes R1's
            });

            // Allow using softclipped bases and nifying: should take the C and the rest are Ns
            TestSuccesfullyStitchedRead(read1, read2, 0, "8M", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);
                Assert.Equal("1F7S", mergedRead.CigarDirections.ToString());
                Assert.Equal("CNNNNNNN", mergedRead.Sequence); // For stitched sites (all but the first), if disagreement, takes R1's
            }, nifyDisagreements: true);

            // Not using softclipped bases: should take the first 3 from R1 and rest from R2.
            TestSuccesfullyStitchedRead(read1, read2, 0, "8M", (mergedRead) =>
            {
                Assert.NotNull(mergedRead);
                Assert.Equal("3F5R", mergedRead.CigarDirections.ToString());
                Assert.Equal("CATGGTAA", mergedRead.Sequence); // No stitched sites. Just take R1 first 3 and R2 for the last 5
            }, useSoftclippedBases: false);

            //StitcherTestHelpers.TestUnstitchableReads(read1, read2, 0, (unStitchableReads) =>
            //{
            //    Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read1, x)));
            //    Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read2, x)));
            //},
            //false);
        }

        public static void TestSuccesfullyStitchedRead(Read read1, Read read2, int minQscore, string xcTag, Action<Read> assertions, bool nifyDisagreements = false, bool useSoftclippedBases = true, bool ignoreProbeSoftclips = true)
        {
            var alignmentSet = new AlignmentSet(read1, read2);
            var stitcher = StitcherTestHelpers.GetStitcher(minQscore, 
                nifyDisagreements: nifyDisagreements, useSoftclippedBases: useSoftclippedBases, ignoreProbeSoftclips: ignoreProbeSoftclips);
            stitcher.TryStitch(alignmentSet);

            // -----------------------------------------------
            // Basic Stitcher, No XC tag
            // -----------------------------------------------

            CheckMergedRead(xcTag, assertions, alignmentSet);
        }

        private static void CheckMergedRead(string xcTag, Action<Read> assertions, AlignmentSet alignmentSet)
        {
            Assert.Equal(1, alignmentSet.ReadsForProcessing.Count);
            var mergedRead = alignmentSet.ReadsForProcessing.First() as Read;

            Assert.Equal(xcTag, mergedRead.StitchedCigar.ToString());
            Assert.Equal(xcTag, mergedRead.CigarData.ToString());

            assertions(mergedRead);
        }
    }
}