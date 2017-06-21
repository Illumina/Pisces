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
    public class BasicStitcherTests
    {
        private Read GetStitchedRead(AlignmentSet alignmentSet)
        {
            var merger = new ReadMerger(0,false,false, false, new ReadStatusCounter(), false, true);
            var stitchedRead = merger.GenerateNifiedMergedRead(alignmentSet, false);
            return stitchedRead;
        }
        [Fact]
        public void GenerateNifiedMergedRead()
        {
            var read1 = ReadTestHelper.CreateRead("chr1", "AAAAA", 2,
                new CigarAlignment("1S4M"));

            var read2 = ReadTestHelper.CreateRead("chr1", "AAAAA", 2,
                new CigarAlignment("4M1S"));
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            var alignmentSet = new AlignmentSet(read1, read2);
            var stitchedRead = GetStitchedRead(alignmentSet);
            Assert.Equal("1S4M1S", stitchedRead.StitchedCigar.ToString());
            Assert.Equal("NNNNNN", stitchedRead.Sequence);
            Assert.Equal("1F4S1R", stitchedRead.CigarDirections.ToString());

            StitcherTestHelpers.SetReadDirections(read1, DirectionType.Reverse);
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Forward);

            stitchedRead = GetStitchedRead(alignmentSet);
            Assert.Equal("1S4M1S", stitchedRead.StitchedCigar.ToString());
            Assert.Equal("NNNNNN", stitchedRead.Sequence);
            Assert.Equal("1R4S1F", stitchedRead.CigarDirections.ToString());

            StitcherTestHelpers.SetReadDirections(read1, DirectionType.Forward);
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            // Insertion that we don't know what to do with -> Nified match
            read1 = ReadTestHelper.CreateRead("chr1", "AAAAA", 2,
                new CigarAlignment("1S3M1I"));
            alignmentSet = new AlignmentSet(read1,read2);
            stitchedRead = GetStitchedRead(alignmentSet);
            Assert.Equal("1S4M1S", stitchedRead.StitchedCigar.ToString());
            Assert.Equal("NNNNNN", stitchedRead.Sequence);
            Assert.Equal("1F4S1R", stitchedRead.CigarDirections.ToString());

            // Read 1 goes to end of read 2
            read1 = ReadTestHelper.CreateRead("chr1", "AAAAAA", 2,
                new CigarAlignment("1S3M2I"));
            alignmentSet = new AlignmentSet(read1, read2);
            stitchedRead = GetStitchedRead(alignmentSet);
            Assert.Equal("1S5M", stitchedRead.StitchedCigar.ToString());
            Assert.Equal("NNNNNN", stitchedRead.Sequence);
            Assert.Equal("1F5S", stitchedRead.CigarDirections.ToString());

            // Read 1 goes past read 2
            read1 = ReadTestHelper.CreateRead("chr1", "AAAAAAA", 2,
    new CigarAlignment("1S3M3I"));
            alignmentSet = new AlignmentSet(read1, read2);
            stitchedRead = GetStitchedRead(alignmentSet);
            Assert.Equal("1S6M", stitchedRead.StitchedCigar.ToString());
            Assert.Equal("NNNNNNN", stitchedRead.Sequence);
            Assert.Equal("1F5S1F", stitchedRead.CigarDirections.ToString());

        }

        [Fact]
        public void TryStitch_RealExamples()
        {
            // Real example from Kristina's problematic variant #73
            var read1Bases =
                "GAAGCCACACTGACGTGCCTCTCCCTCCCTCCAGGAAGCCTTCCAGGAAGCCTACGTGATGGCCAGCGTGGACAACCCCCACGTGTGCCGCCTGCTGGGCATCTGCCTCACCTCCACCGTGCAGCTCATCACGCAGCTCATGCCCTTCGG";
            var read2Bases =
                "AGGAAGCCTTCCAGGAAGCCTACGTGATGGCCAGCGTGGACAACCCCCACGTGTGCCGCCTGCTGGGCATCTGCCTCACCTCCACCGTGCAGCTCATCACGCAGCTCATGCCCTTCGGCTGCCTCCTGGACTATGTCCGGGAACACAAAG";

            var read1 = ReadTestHelper.CreateRead("chr7", read1Bases, 55248972, new CigarAlignment("20S9M12I109M"));
            var read2 = ReadTestHelper.CreateRead("chr7", read2Bases, 55248981, new CigarAlignment("9S120M21S"));
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            var stitcher = new BasicStitcher(10, useSoftclippedBases: false);
            var alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            var mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal("20S9M12I120M21S", mergedRead.CigarData.ToString());
            Assert.Equal("41F109S32R", mergedRead.CigarDirections.ToString());

            // Shouldn't stitch - problem Yu was having (tried to merge and then said the base at position 158 is null).
            read1Bases =
                "CGACGCTCTTGCGATCTTCAAAGCAATAGGATGGGTGATCAGGGATGTTGCTTACAAGAAAAGAACTGCCATACAGCTTCAACAACAACTTCTTCCACCCACCCCTAAAATGATGCTAAAAAGTAAGTCATCTCTGGTTCTCCCCCGATT";
            read2Bases =
                "TCAAAGCAATAGGATGGATGATCAGAGATGTTGCTTACAAGAAAAGAACTGCCATACAGCTTCAACAACAACTTCTTCCACTCCCCCCTAAAGTGATGCTAAAAAGTAAATCATCCCTGTTTCTCCCCCGTTCGCGAATTTCTACGATCG";

            read1 = ReadTestHelper.CreateRead("chr7", read1Bases, 109465121, new CigarAlignment("44S56M1I23M26S"));
            read2 = ReadTestHelper.CreateRead("chr7", read2Bases, 109465121, new CigarAlignment("27S55M1I24M43S"));
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            stitcher = new BasicStitcher(10, useSoftclippedBases: true);
            alignmentSet = new AlignmentSet(read1, read2);
            Assert.False(stitcher.TryStitch(alignmentSet));
        }

        [Fact]
        public void TryStitch_InsertionEdge()
        {
            TestMerge(3, "2S4M", 2, "3M");
            TestMerge(1, "1M2I3M", 2, "6M", 1, "1M2I6M");
            TestMerge(1, "5M1I", 4, "2M2I2M", 1, "5M2I2M");
            TestMerge(1, "4M2S", 1, "6M", 1, "6M");

            //TestMerge(3, "2S1I4M", 2, "1S3M");
            // Not valid:
            // 0 1 2 3 4 5
            //      *M M M M
            //    *M M M 

            TestMerge(1, "4M2I", 4, "1M2I3M");
            TestMerge(1, "3M2I1M", 4, "2I4M");
            TestMerge(1, "3M", 1, "3M2S", 1, "3M2S");
            TestMerge(2, "1S2M2I1M", 4, "2S2M2S");
            TestMerge(2, "1S2M2I1M", 4, "2M2S");
            TestMerge(2, "1S2M2I1M", 3, "1M2I2M2S");
            TestMerge(2, "1S2M2I1M", 3, "2S1M2I2M2S");

            // Uneven overlapping softclips at suffix end: SCProbeDeletionInputs_SoftclippedDeletion-3
            TestMerge(3, "2S2M2D2S", 2, "3M2D1M2S", 2, "1S3M2D1M2S");

            // Should not be stitchable.
            TestMerge(1, "3M2I", 4, "2I4M", shouldMerge: false);

            // PICS-343: Don't stitch unanchored
            //PiscesUnitTestScenarios_GapSituations_Gaps Inputs_-8
            //TestMerge(2, "1S2M2S", 7, "2S2M1S", shouldMerge: true);

            // PICS-347: Don't allow stitcher to create internal softclips
            //PiscesUnitTestScenarios_GapSituations_Gaps Inputs_Gaps-7
            TestMerge(3, "2S1M2S", 6, "2S2M2S", shouldMerge: false);
            //PiscesUnitTestScenarios_Insertions_Insertion Inputs_Insertion-6
            TestMerge(1, "3M2I", 4, "2I4M", shouldMerge: false);

            //PiscesUnitTestScenarios_Insertions_Insertion Inputs_Insertion-7
            TestMerge(1, "5M1I", 4, "2M2I2M", 1, "5M2I2M", "3F3S3R");

            //PiscesUnitTestScenarios_SCProbeInsertions_SCProbeInsertion Inputs_SoftclippedInsertion-1
            TestMerge(2, "1S1M2I1M", 3, "1S4M1S", 2, "1S1M2I4M1S", "3F2S4R");
        }

        [Fact]
        public void TryStitch_SoftclipDeletionOverlaps()
        {
            // In response to PICS-341.
            // PiscesUnitTestScenarios_GapSituations_Gaps Inputs_Gaps-4
            TestMerge(2, "1S3M1S", 3, "2M2D1M2S", 2, "1S3M2D1M2S", "2F5S2R"); 

            // Another variation I made up
            TestMerge(2, "1S3M2S", 3, "3M2D1M2S", 2, "1S4M2D1M2S", "2F6S2R");

            // PiscesUnitTestScenarios_SCProbeDeletions_SCProbeDeletion Inputs_SoftclippedDeletion-1
            TestMerge(2, "1S1M2D5M", 5, "2S3M2S", 2, "1S1M2D5M", "1R6S2F"); 

            // PiscesUnitTestScenarios_SCProbeDeletions_SCProbeDeletion Inputs_SoftclippedDeletion-3
            TestMerge(3, "2S2M2D2S", 2, "3M2D1M2S", 2, "1S3M2D1M2S", "1F1R5S1F1R"); 

            // PiscesUnitTestScenarios_SCProbeDeletions_SCProbeDeletion Inputs_SoftclippedDeletion-4
            TestMerge(2, "1S3M1S", 3, "2M2D1M2S", 2, "1S3M2D1M2S", "2F5S2R");

            // PiscesUnitTestScenarios_SCProbeDeletions_SCProbeDeletion Inputs_SoftclippedDeletion-5
            // The prefix clip on R2 indicates uncertainty, give it the whole deletion from R1 and kick the S to the M
            TestMerge(2, "1S1M2D4M", 4, "1S1D4M1S", 2, "1S1M2D4M1S", "1F7S1R");

            // PiscesUnitTestScenarios_SoftClippedDeletions_SoftclippedDeletion Inputs_SoftclippedDeletion-1
            TestMerge(2, "1M2D5M", 5, "2S4M", 2, "1S1M2D5M", "1R7S1F"); 

            // PiscesUnitTestScenarios_SoftClippedDeletions_SoftclippedDeletion Inputs_SoftclippedDeletion-3
            TestMerge(1, "4M2S", 2, "3M2D3M", 1, "4M2D3M", "1F7S1R");

            // PiscesUnitTestScenarios_SoftClippedDeletions_SoftclippedDeletion Inputs_SoftclippedDeletion-5
            TestMerge(1, "2M2D4M", 4, "1S1D5M", 1, "2M2D5M", "1F7S1R");

            // TODO compound indel situations
        }

        [Fact]
        public void TryStitch_KissingReads()
        {
            // Kissing reads can merge if only one of them has a softclip at the kissing point
            // PiscesUnitTestScenarios_SCProbeInsertions_SCProbeInsertion Inputs_SoftclippedInsertion-6
            TestMerge(2, "1S1M2S", 3, "1M2S", 2, "1S2M2S", "2F1S1F1R");


            // Doesn't stitch if both reads have softclip at kissing point - TODO determine appropriate behavior here.
            // PiscesUnitTestScenarios_SoftClippedInsertions_SoftClippedInsertion Inputs_SoftclippedInsertion-7
            //TestMerge(1, "2M2S", 3, "1S3M");
        }

        [Fact]
        public void TryStitch_InsertionEndingInSoftclip()
        {
            // PiscesUnitTestScenarios_UnstitchableInsertions_UnstitchableIns Inputs_UnstitchableInsertions-3
            TestMerge(2, "1S2M2I1M", 2, "2M2I2S", 2, "1S2M2I1M1S", "1F4S1F1R");

            // PiscesUnitTestScenarios_SCProbeInsertions_SCProbeInsertion Inputs_SoftclippedInsertion-7
            TestMerge(4, "3S2M1S", 4, "2M2I2S", 4, "3S2M2I2S", "3F3S3R");

            // PiscesUnitTestScenarios_SCProbeInsertions_SCProbeInsertion Inputs_SoftclippedInsertion-8
            //TestMerge(3, "2S4M2S", 4, "3M2I1S", 3, "2S4M2I")

            // PiscesUnitTestScenarios_UnstitchableInsertions_UnstitchableIns Inputs_UnstitchableInsertions-3
            TestMerge(2, "1S2M2I1M", 2, "2M2I2S", 2, "1S2M2I1M1S", "1F4S1F1R");
        }

        [Fact]
        public void TryStitch_IgnoreProbeSoftclips()
        {
            TestMerge(3, "2S4M", 1, "6M", 1, "6M", "2R4S");
            TestMerge(1, "6M", 3, "2S4M", 1, "6M", "6S");
            TestMerge(1, "6M", 3, "4M2S", 1, "6M2S", "2F4S2R");
            TestMerge(3, "2S4M", 3, "4M1S", 3, "2S4M1S", "2F4S1R");
            TestMerge(2, "1S6M", 5, "2S3M2S", 2, "1S6M2S", "2F5S2R");

            // SSMMMM
            //  SMMMMS
            // Probe clips only count toward directionality if they are alone.
            TestMerge(3, "2S4M", 3, "1S4M1S", 3, "2S4M1S", "1F1R4S1R");

        }


        [Fact]
        public void RedistributeSoftclipPrefixes()
        {
            TestMerge(5, "2I3M", 5, "2S3M", 5, "2I3M", "5S");
            TestMerge(5, "2S3M", 5, "2I3M", 5, "2I3M", "2R3S");

            TestMerge(5, "2I3M", 5, "2S3M", 5, "2I3M", "5S", ignoreProbeSoftclips: false);
            TestMerge(5, "2S3M", 5, "2I3M", 5, "2I3M", "2R3S", ignoreProbeSoftclips: true);
        }

        [Fact]
        public void RedistributeSoftclipSuffixes()
        {
            // Redistributing softclip suffixes
            // S/I | /M --> 
            TestMerge(1, "3M1S", 1, "3M1I1M", 1, "3M1I1M", "4S1R", ignoreProbeSoftclips: false);
            TestMerge(1, "3M1S", 1, "3M1I1M", 1, "3M1I1M", "4S1R", ignoreProbeSoftclips: true);
            // S/ | /M -> / |S/M
            TestMerge(1, "3M1S", 1, "4M", 1, "4M", "4S", ignoreProbeSoftclips: false);
            TestMerge(1, "3M1S", 1, "4M", 1, "4M", "4S", ignoreProbeSoftclips: true);
            // SS/ | /M, / | /M --> / | S/M, / |S/M
            TestMerge(1, "3M2S", 1, "5M", 1, "5M", "5S", ignoreProbeSoftclips: false);
            TestMerge(1, "3M2S", 1, "5M", 1, "5M", "5S", ignoreProbeSoftclips: true);
            // SS/ | /M, /S| / --> / |S/M, S/S| / 
            TestMerge(1, "3M2S", 1, "4M1S", 1, "4M1S", "5S", ignoreProbeSoftclips: false);
            TestMerge(1, "3M2S", 1, "4M1S", 1, "4M1S", "4S1F", ignoreProbeSoftclips: true);
            // SS/ | /M, /SS| / --> / |S/M, S/S| / 
            TestMerge(1, "3M2S", 1, "4M2S", 1, "4M2S", "5S1R", ignoreProbeSoftclips: false);
            TestMerge(1, "3M2S", 1, "4M2S", 1, "4M2S", "4S1F1R", ignoreProbeSoftclips: true);

            // Suffix has lots of S to give away, and other read has multiple Is
            TestMerge(1, "3M5S", 1, "3M2I1M", 1, "3M2I1M2S", "6S2F", ignoreProbeSoftclips: false);
            TestMerge(1, "3M5S", 1, "3M2I1M", 1, "3M2I1M2S", "6S2F", ignoreProbeSoftclips: true);
        }

        [Fact]
        public void TryStitch_LongReads()
        {
            // Variable configured read lengths
            StitchWithReadLength(200, false);
            StitchWithReadLength(201, false);
            StitchWithReadLength(1024, false);
            StitchWithReadLength(1025, true);
        }

        private void StitchWithReadLength(int maxExpectedReadLength, bool greaterThanDefault)
        {
            // Combined read length at exactly max expected read length - always worked
            TestMerge(1, maxExpectedReadLength + "M", 1, maxExpectedReadLength + "M",
                1, maxExpectedReadLength + "M", maxExpectedReadLength + "S",
                maxReadLength: maxExpectedReadLength);

            // Combined read length just above max expected single read length
            TestMerge(1, maxExpectedReadLength + "M", 2, maxExpectedReadLength + "M",
                1, maxExpectedReadLength + 1 + "M", "1F" + (maxExpectedReadLength - 1) + "S" + "1R",
                maxReadLength: maxExpectedReadLength);

            // Single base overlap -- basically the longest stitched read possible
            TestMerge(1, maxExpectedReadLength + "M", maxExpectedReadLength, maxExpectedReadLength + "M",
                1, (2 * maxExpectedReadLength - 1) + "M", (maxExpectedReadLength - 1) + "F" + "1S" + (maxExpectedReadLength - 1) + "R",
                maxReadLength: maxExpectedReadLength);

            // Reads longer than maxExpectedReadLength would combine to longer than expected, even though they would otherwise stitch. Throw exception.
            Assert.Throws<ArgumentException>(() => TestMerge(1, maxExpectedReadLength + 1 + "M", maxExpectedReadLength, maxExpectedReadLength + 1 + "M",
                1, null, null,
                maxReadLength: maxExpectedReadLength));

            // No overlap -- shouldn't merge anyway
            TestMerge(1, maxExpectedReadLength + "M", maxExpectedReadLength + 1, maxExpectedReadLength + "M",
                1, null, null, shouldMerge: false,
                maxReadLength: maxExpectedReadLength);

            // Do not pass maxReadLength, use default
            if (greaterThanDefault)
            {
                Assert.Throws<ArgumentException>(
                    () => TestMerge(1, maxExpectedReadLength + "M", maxExpectedReadLength, maxExpectedReadLength + "M",
                        1, (2 * maxExpectedReadLength - 1) + "M",
                        (maxExpectedReadLength - 1) + "F" + "1S" + (maxExpectedReadLength - 1) + "R"));
            }
            else
            {
                TestMerge(1, maxExpectedReadLength + "M", maxExpectedReadLength, maxExpectedReadLength + "M",
                    1, (2 * maxExpectedReadLength - 1) + "M",
                    (maxExpectedReadLength - 1) + "F" + "1S" + (maxExpectedReadLength - 1) + "R");
            }

        }

        private void TestMerge(int pos1, string cigar1, int pos2, string cigar2, int posStitch = 0, string cigarStitch = "", string stitchDirections = "", bool shouldMerge = true, bool ignoreProbeSoftclips = true, int? maxReadLength = null)
        {
            var r1Bases = new string('A', (int)new CigarAlignment(cigar1).GetReadSpan());
            var r2Bases = new string('A', (int)new CigarAlignment(cigar2).GetReadSpan());
            var read1 = ReadTestHelper.CreateRead("chr1", r1Bases, pos1,
             new CigarAlignment(cigar1));

            var read2 = ReadTestHelper.CreateRead("chr1", r2Bases, pos2,
                new CigarAlignment(cigar2));
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            BasicStitcher stitcher;

            if (maxReadLength != null)
            {
                stitcher = new BasicStitcher(10, ignoreProbeSoftclips: ignoreProbeSoftclips,
                    maxReadLength: maxReadLength.Value);
            }
            else
            {
                // Use the default
                stitcher = new BasicStitcher(10, ignoreProbeSoftclips: ignoreProbeSoftclips);
            }

            if (!shouldMerge)
            {
                var alignmentSet = new AlignmentSet(read1, read2);
                Assert.False(stitcher.TryStitch(alignmentSet));

                //StitcherTestHelpers.TestUnstitchableReads(read1, read2, 0, null);
            }
            else
            {
                var alignmentSet = new AlignmentSet(read1, read2);
                var didStitch = stitcher.TryStitch(alignmentSet);
                Assert.True(didStitch);

                var mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
                Console.WriteLine(mergedRead.Position + " " + mergedRead.CigarData);
                Console.WriteLine("---------------");
                if (cigarStitch != "")
                {
                    Assert.Equal(posStitch, mergedRead.Position);
                    Assert.Equal(cigarStitch, mergedRead.CigarData.ToString());
                }
                if (stitchDirections != "")
                {
                    Assert.Equal(stitchDirections, mergedRead.CigarDirections.ToString());
                }
            }
        }



        [Fact]
        public void TryStitch_ReCo()
        {
            // Real example from ReCo, was failing to generate the correct stitched cigar
            var read1Bases =
                "GTACTCCTACAGTCCCACCCCTCCCCTATAAACCTTATGAATCCCCGTTCACTTAGATGCCAGCTTGGCAAGGAAGGGAAGTACACATCTGTTGACAGTAATGAAATATCCTTGATAAGGATTTAAATTTTGGATGTGCTG";
            var read2Bases =
                "ACCTACAGTCCCACCCCTCCCCTATAAACCTTAGGAATCCCCGTTCACTTAGATGCCAGCTTGGCAAGGAAGGGAAGTACACATCTGTTGACAGTAATGAAATATCCTTGATAAGGATTTAAATTTTGGATGTGCTGAGCT";

            // 8             9
            // 3 4 5 6 7 8 9 0 1 2
            // s s s s s M M M M M ...
            // - - - - M M M M M M ...
            // F F F F R S S S S S ... // Stitched directions if we don't allow softclip to contribute
            // F F F F S S S S S S ... // Stitched directions if we do allow softclip to contribute

            var read1 = ReadTestHelper.CreateRead("chr21", read1Bases, 16685488,
                new CigarAlignment("5S136M"));

            var read2 = ReadTestHelper.CreateRead("chr21", read2Bases, 16685487,
                new CigarAlignment("137M4S"));
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            var stitcher = new BasicStitcher(10, useSoftclippedBases: false);
            var alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            // Without allowing softclips to count to support, should still get a M at an M/S overlap, but it won't be stitched.
            var mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal("4S137M4S", mergedRead.CigarData.ToString());
            var expectedDirections = StitcherTestHelpers.BuildDirectionMap(new List<IEnumerable<DirectionType>>
                {
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Forward, 4),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Reverse, 1),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Stitched, 136),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Reverse, 4)
                });
            StitcherTestHelpers.VerifyDirectionType(expectedDirections, mergedRead.CigarDirections.Expand().ToArray());

            stitcher = new BasicStitcher(10, useSoftclippedBases: true);
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal("4S137M4S", mergedRead.CigarData.ToString());
            expectedDirections = StitcherTestHelpers.BuildDirectionMap(new List<IEnumerable<DirectionType>>
                {
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Forward, 4),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Reverse, 1),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Stitched, 136),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Reverse, 4)
                });
            StitcherTestHelpers.VerifyDirectionType(expectedDirections, mergedRead.CigarDirections.Expand().ToArray());

            // If we're not ignoring probe softclips, go back to the original expected directions (1 more stitched from probe)
            stitcher = new BasicStitcher(10, useSoftclippedBases: true, ignoreProbeSoftclips: false);
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal("4S137M4S", mergedRead.CigarData.ToString());
            expectedDirections = StitcherTestHelpers.BuildDirectionMap(new List<IEnumerable<DirectionType>>
                {
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Forward, 4),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Stitched, 137),
                    StitcherTestHelpers.BuildDirectionSegment(DirectionType.Reverse, 4)
                });
            StitcherTestHelpers.VerifyDirectionType(expectedDirections, mergedRead.CigarDirections.Expand().ToArray());

        }

        [Fact]
        public void TryStitch_NoXC_Unstitchable()
        {

            var read1 = ReadTestHelper.CreateRead("chr1", "ATCGATCG", 12345,
                new CigarAlignment("8M"), qualityForAll: 30);

            var read2_noOverlap = ReadTestHelper.CreateRead("chr1", "A", 2384,
                new CigarAlignment("1M"), qualityForAll: 30);

            var read2_overlap = ReadTestHelper.CreateRead("chr1", "ATCGTT", 12349,
                new CigarAlignment("1I5M"), qualityForAll: 30);

            var read2_diffChrom = ReadTestHelper.CreateRead("chr2", "ATCGTT", 12349,
                new CigarAlignment("6M"), qualityForAll: 30);

            var read2_nonOverlap_border = ReadTestHelper.CreateRead("chr1", "AT", 12343,
                new CigarAlignment("2M"), qualityForAll: 30);

            var stitcher = StitcherTestHelpers.GetStitcher(10);
            ;
            // -----------------------------------------------
            // Either of the partner reads is missing*
            // *(only read that could be missing is read 2, if read 1 was missing couldn't create alignment set)
            // -----------------------------------------------
            // Should throw an exception
            var alignmentSet = new AlignmentSet(read1, null);
            Assert.Throws<ArgumentException>(() => stitcher.TryStitch(alignmentSet));

            // -----------------------------------------------
            // No overlap, reads are far away
            // -----------------------------------------------
            // Shouldn't stitch
            alignmentSet = new AlignmentSet(read1, read2_noOverlap);
            stitcher.TryStitch(alignmentSet);
            Assert.Equal(2, alignmentSet.ReadsForProcessing.Count);
            StitcherTestHelpers.TestUnstitchableReads(read1, read2_noOverlap, 0, (unStitchableReads) =>
            {
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read1, x)));
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read2_noOverlap, x)));
            });

            // -----------------------------------------------
            // No overlap, reads are directly neighboring
            // -----------------------------------------------
            // Shouldn't stitch
            alignmentSet = new AlignmentSet(read1, read2_nonOverlap_border);
            stitcher.TryStitch(alignmentSet);
            Assert.Equal(2, alignmentSet.ReadsForProcessing.Count);
            StitcherTestHelpers.TestUnstitchableReads(read1, read2_nonOverlap_border, 0, (unStitchableReads) =>
            {
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read1, x)));
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read2_nonOverlap_border, x)));
            });

            // -----------------------------------------------
            // No overlap, reads on diff chromosomes
            // -----------------------------------------------
            // Shouldn't stitch
            alignmentSet = new AlignmentSet(read1, read2_diffChrom);
            stitcher.TryStitch(alignmentSet);
            Assert.Equal(2, alignmentSet.ReadsForProcessing.Count);
            StitcherTestHelpers.TestUnstitchableReads(read1, read2_diffChrom, 0, (unStitchableReads) =>
            {
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read1, x)));
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read2_diffChrom, x)));
            });

            // -----------------------------------------------
            // Has overlap, but cigars are incompatible
            // -----------------------------------------------
            // Shouldn't stitch
            alignmentSet = new AlignmentSet(read1, read2_overlap);
            stitcher.TryStitch(alignmentSet);
            Assert.Equal(2, alignmentSet.ReadsForProcessing.Count);
            StitcherTestHelpers.TestUnstitchableReads(read1, read2_overlap, 0, (unStitchableReads) =>
            {
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read1, x)));
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read2_overlap, x)));
            });

            // -----------------------------------------------
            // Has overlap, but cigars are incompatible, but read 2 starts with SC
            // -----------------------------------------------
            // Overlap is just S and I - should stitch
            // 5678----90123456789
            // MMMMIIII
            //     SSSSMMMM
            var read1_withIns = ReadTestHelper.CreateRead("chr1", "ATCGATCG", 12345,
                new CigarAlignment("4M4I"), qualityForAll: 30);
            var read2_withSC = ReadTestHelper.CreateRead("chr1", "ATCGATCG", 12349,
                new CigarAlignment("4S4M"), qualityForAll: 30);
            alignmentSet = new AlignmentSet(read1_withIns, read2_withSC);
            
            //stitcher.TryStitch(alignmentSet);
            //Assert.Equal(1, alignmentSet.ReadsForProcessing.Count);
            //Assert.Equal("4M4I4M", alignmentSet.ReadsForProcessing.First().CigarData.ToString());

            // Overlap is S and some disagreeing ops with I - should not stitch
            read2_withSC = ReadTestHelper.CreateRead("chr1", "ATCGATCG", 12348,
                new CigarAlignment("2S1D6M"), qualityForAll: 30);
            alignmentSet = new AlignmentSet(read1_withIns, read2_withSC);
            stitcher.TryStitch(alignmentSet);
            Assert.Equal(2, alignmentSet.ReadsForProcessing.Count);
            StitcherTestHelpers.TestUnstitchableReads(read1_withIns, read2_withSC, 0, (unStitchableReads) =>
            {
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read1_withIns, x)));
                Assert.Equal(1, unStitchableReads.Count(x => StitcherTestHelpers.VerifyReadsEqual(read2_withSC, x)));
            });

        }

        [Fact]
        public void TryStitch_NoXC_Stitchable()
        {
            //Reads without XC tags that do overlap should be added as one merged read in basic stitcher
            var basicStitcher = StitcherTestHelpers.GetStitcher(10);
            var alignmentSet = StitcherTestHelpers.GetOverlappingReadSet();
            basicStitcher.TryStitch(alignmentSet);
            Assert.Equal(1, alignmentSet.ReadsForProcessing.Count);
        }

        [Fact]
        public void TryStitch_CalculateStitchedCigar()
        {
            // -----------------------------------------------
            // Read position maps disagree
            // -----------------------------------------------
            // Should throw out the pair
            var read1 = ReadTestHelper.CreateRead("chr1", "ATCGATCG", 12345,
                new CigarAlignment("2M2D3M1D3M"), qualityForAll: 30); //Within the overlap, we have a deletion so there will be a shifting of positions from that point on

            var read2 = ReadTestHelper.CreateRead("chr1", "ATCGATCG", 12349,
                new CigarAlignment("8M"), qualityForAll: 30);

            var stitcher = StitcherTestHelpers.GetStitcher(10);
            var alignmentSet = new AlignmentSet(read1, read2);
            Assert.True(!alignmentSet.ReadsForProcessing.Any());

            // -----------------------------------------------
            // When calculating stitched cigar, stitched cigar should have 
            //  - everything from read1 before the overlap 
            //  - everything from read2 starting from the overlap
            // But since we ensure that the position maps agree in the overlap region, it's really not a matter of one taking precedence over the other
            //  1234...   1 - - 2 3 4 5 6 - - 7 8 9 0
            //  Read1     X X X X X X X X - - - - -
            //  Read1     M I I M M M M M - - - - -
            //  Read2     - - - X X X X X X X X - -
            //  Read2     - - - M M M M M I M M - -
            // -----------------------------------------------

            // Stitched cigar should have R1's insertion from before the overlap and R2's insertion from after the overlap
            read1 = ReadTestHelper.CreateRead("chr1", "ATCGATCG", 12341,
                new CigarAlignment("1M2I5M"), qualityForAll: 30);

            read2 = ReadTestHelper.CreateRead("chr1", "ATCGATCG", 12342,
                new CigarAlignment("5M1I2M"), qualityForAll: 30);

            stitcher = StitcherTestHelpers.GetStitcher(10);
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);

            Assert.Equal("1M2I5M1I2M", StitcherTestHelpers.GetMergedRead(alignmentSet).CigarData.ToString());
        }

        [Fact]
        public void TryStitch_ConsensusSequence()
        {
            ExecuteConsensusTests(true);
            ExecuteConsensusTests(false);
        }

        private void ExecuteConsensusTests(bool nifyDisagreements)
        {
            // 1234...   1 - - 2 3 4 5 6 - - 7 8 9 0 //Reference Positions
            // Read1     X X X X X X X X - - - - -
            // Read1     M I I M M M M M - - - - -
            // Read1     T T T T T T T T - - - - -
            // Read2     - - - X X X X X X X X - -
            // Read2     - - - M M M M M I M M - -
            // Read2     - - - A A A A A A A A - -

            var r1qualities = 30;
            var r2qualities = 20;

            var read1 = ReadTestHelper.CreateRead("chr1", "TTTTTTTT", 12341,
                new CigarAlignment("1M2I5M"), qualityForAll: (byte)r1qualities);

            var read2 = ReadTestHelper.CreateRead("chr1", "AAAAAAAA", 12342,
                new CigarAlignment("5M1I2M"), qualityForAll: (byte)r2qualities);

            var stitcher = StitcherTestHelpers.GetStitcher(10, false, nifyDisagreements: nifyDisagreements);
            var alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);

            // Merged    A T C ? ? ? ? ? T C G - -
            // Merged    M I I M M M M M I M M - -
            // Merged    0 1 2 3 4 5 6 7 8 9 0 1 2

            var overlapStart = 3;
            var overlapEnd = 8;
            var overlapLength = 5;

            //Consensus sequence should have everything from read1 for positions before overlap
            var mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal("TTT", mergedRead.Sequence.Substring(0, overlapStart));

            //Consensus sequence should have everything from read2 for positions after overlap
            Assert.Equal("AAA", mergedRead.Sequence.Substring(overlapEnd, 3));

            //Consensus sequence should have an N where we have two high-quality (both above min) disagreeing bases
            Assert.Equal(nifyDisagreements? "NNNNN":"TTTTT", mergedRead.Sequence.Substring(overlapStart, 5));

            //Consensus sequence should have 0 quality where we have two high-quality (both above min) disagreeing bases
            Assert.True(mergedRead.Qualities.Take(overlapStart).All(q => q == r1qualities));
            Assert.True(mergedRead.Qualities.Skip(overlapStart).Take(overlapLength).All(q => q == 0));
            Assert.True(mergedRead.Qualities.Skip(overlapEnd).Take(mergedRead.Sequence.Length - overlapEnd).All(q => q == r2qualities));

            //Consensus sequence should take higher quality base if one or more of the bases is below min quality

            //Read 2 trumps whole overlap
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 5, 5, 5, 5 };
            read2.BamAlignment.Qualities = new byte[] { 40, 40, 40, 40, 40, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(nifyDisagreements ? "NNNNN" : read2.Sequence.Substring(0, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal(nifyDisagreements ? "TTTNNNNNAAA" : "TTTAAAAAAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(nifyDisagreements ? new byte[] { 30, 30, 30, 0, 0, 0, 0, 0, 20, 19, 18 } : new byte[] { 30, 30, 30, 40, 40, 40, 40, 40, 20, 19, 18 }, mergedRead.Qualities);

            //Read 1 trumps whole overlap
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 40, 40, 40, 40, 40 };
            read2.BamAlignment.Qualities = new byte[] { 5, 5, 5, 5, 5, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(nifyDisagreements ? "NNNNN" : read1.Sequence.Substring(3, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal(nifyDisagreements ? "TTTNNNNNAAA" : "TTTTTTTTAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(nifyDisagreements ? new byte[] { 30, 30, 30, 0, 0, 0, 0, 0, 20, 19, 18 } : new byte[] { 30, 30, 30, 40, 40, 40, 40, 40, 20, 19, 18 }, mergedRead.Qualities);

            //Little bit of each
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 45, 5, 45, 5 };
            read2.BamAlignment.Qualities = new byte[] { 40, 5, 40, 5, 40, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(nifyDisagreements ? "TTTNNNNNAAA" : "TTTATATAAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(nifyDisagreements ? new byte[] { 30, 30, 30, 0, 0, 0, 0, 0, 20, 19, 18 } : new byte[] { 30, 30, 30, 40, 45, 40, 45, 40, 20, 19, 18 }, mergedRead.Qualities);

            //Consensus sequence should take base and assign the higher quality if both bases agree
            var read2_agreeingBases = ReadTestHelper.CreateRead("chr1", "TTTTTTTT", 12342,
                new CigarAlignment("5M1I2M"), new byte[] { 40, 5, 40, 5, 40, 20, 19, 18 });
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 45, 5, 45, 5 };
            alignmentSet = new AlignmentSet(read1, read2_agreeingBases);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal("TTTTTTTTTTT", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(new byte[] { 30, 30, 30, 45, 50, 45, 50, 45, 20, 19, 18 }, mergedRead.Qualities);

            //Bases disagree and both are below minimum quality, read1>read2 : take base/q from read1
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 8, 8, 8, 8, 8 };
            read2.BamAlignment.Qualities = new byte[] { 5, 5, 5, 5, 5, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(nifyDisagreements ? "NNNNN" : read1.Sequence.Substring(3, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal(nifyDisagreements ? "TTTNNNNNAAA" : "TTTTTTTTAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(nifyDisagreements ? new byte[] { 30, 30, 30, 0, 0, 0, 0, 0, 20, 19, 18 } : new byte[] { 30, 30, 30, 8, 8, 8, 8, 8, 20, 19, 18 }, mergedRead.Qualities);

            //Bases disagree and both are below minimum quality, read2>read1 : take base/q from read2
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 5, 5, 5, 5 };
            read2.BamAlignment.Qualities = new byte[] { 8, 8, 8, 8, 8, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(nifyDisagreements ? "NNNNN" : read2.Sequence.Substring(0, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal(nifyDisagreements ? "TTTNNNNNAAA" : "TTTAAAAAAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(nifyDisagreements ? new byte[] { 30, 30, 30, 0, 0, 0, 0, 0, 20, 19, 18 } : new byte[] { 30, 30, 30, 8, 8, 8, 8, 8, 20, 19, 18 }, mergedRead.Qualities);

            //Bases disagree and both are below minimum quality, read1==read2 : take base/q from read1
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 5, 5, 5, 5 };
            read2.BamAlignment.Qualities = new byte[] { 5, 5, 5, 5, 5, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(nifyDisagreements ? "NNNNN" : read1.Sequence.Substring(3, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal(nifyDisagreements ? "TTTNNNNNAAA" : "TTTTTTTTAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(nifyDisagreements ? new byte[] { 30, 30, 30, 0, 0, 0, 0, 0, 20, 19, 18 } : new byte[] { 30, 30, 30, 5, 5, 5, 5, 5, 20, 19, 18 }, mergedRead.Qualities);

        }


	}
}