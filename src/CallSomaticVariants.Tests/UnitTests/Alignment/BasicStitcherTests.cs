using System;
using System.Linq;
using CallSomaticVariants.Logic.Alignment;
using CallSomaticVariants.Models;
using CallSomaticVariants.Tests.UnitTests.Models;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Alignment
{
    public class BasicStitcherTests
    {
        [Fact]
        public void TryStitch_NoXC_Unstitchable()
        {

            var read1 = TestHelper.CreateRead("chr1", "ATCGATCG", 12345,
                new CigarAlignment("8M"), qualityForAll: 30);

            var read2_noOverlap = TestHelper.CreateRead("chr1", "A", 2384,
                new CigarAlignment("1M"), qualityForAll: 30);

            var read2_overlap = TestHelper.CreateRead("chr1", "ATCGTT", 12349,
                new CigarAlignment("6M"), qualityForAll: 30);

            var read2_diffChrom = TestHelper.CreateRead("chr2", "ATCGTT", 12349,
                new CigarAlignment("6M"), qualityForAll: 30);

            var read2_nonOverlap_border = TestHelper.CreateRead("chr1", "AT", 12343,
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
            // Should throw exception
            alignmentSet = new AlignmentSet(read1, read2_diffChrom);
            var ex = Assert.Throws<ArgumentException>(() => stitcher.TryStitch(alignmentSet));
            Assert.Contains("Partner reads are from different chromosomes", ex.Message, StringComparison.InvariantCultureIgnoreCase); // This is brittle but since a variety of exceptions can happen in this process want to make sure it's this specific one


        }
        [Fact]
        public void OverlapBoundary()
        {
            // ---------------------------------
            // no overlap
            // ---------------------------------
            var overlap = ExecuteOverlapTest(60, "5S5M5S", 65, "5S5M5S");
            Assert.Equal(null, overlap);

            // ---------------------------------
            // standard example
            // R1   ssxxxxxxxxxss
            // R2       ssxxxxxxxxxss
            // ---------------------------------
            overlap = ExecuteOverlapTest(50, "1S1M1I1M1D1M1S", 51, "1S1M1D5M2S");
            Assert.Equal(3, overlap.Read1.StartIndex);
            Assert.Equal(4, overlap.Read1.EndIndex);
            Assert.Equal(1, overlap.Read2.StartIndex);
            Assert.Equal(2, overlap.Read2.EndIndex);
            Assert.Equal(2, overlap.OverlapLength);
            Assert.Equal(11, overlap.TotalStitchedLength);

            // ---------------------------------
            // read1 end extends beyond read2 end, read2 softclip was large (real world example)
            // R1   ssxxxxxxxxxxss
            // R2       ssxxxxsssssss
            // ---------------------------------
            overlap = ExecuteOverlapTest(55155723, "37S25M2D62M26S", 55155723, "27S25M2D61M37S");
            Assert.Equal(37, overlap.Read1.StartIndex);
            Assert.Equal(122, overlap.Read1.EndIndex);
            Assert.Equal(27, overlap.Read2.StartIndex);
            Assert.Equal(149 - 37, overlap.Read2.EndIndex);
            Assert.Equal(86, overlap.OverlapLength);
            Assert.Equal(37 + 86 + 37, overlap.TotalStitchedLength);

            // ---------------------------------
            // read1 and read2 exactly align
            // R1   ssssssssxxxxxss
            // R2         ssxxxxxsssss
            // ---------------------------------
            overlap = ExecuteOverlapTest(50, "25S25M25S", 50, "5S25M50S");
            Assert.Equal(25, overlap.Read1.StartIndex);
            Assert.Equal(49, overlap.Read1.EndIndex);
            Assert.Equal(5, overlap.Read2.StartIndex);
            Assert.Equal(29, overlap.Read2.EndIndex);
            Assert.Equal(25, overlap.OverlapLength);
            Assert.Equal(100, overlap.TotalStitchedLength);

            // ---------------------------------
            // read1 anchored in a deletion in read1 (real world example)
            // ---------------------------------
            overlap = ExecuteOverlapTest(55602654, "25S20M2D105M", 55602674, "1M1D126M23S");
            Assert.Equal(44, overlap.Read1.StartIndex);
            Assert.Equal(149, overlap.Read1.EndIndex);
            Assert.Equal(0, overlap.Read2.StartIndex);
            Assert.Equal(105, overlap.Read2.EndIndex);
            Assert.Equal(106, overlap.OverlapLength);
            Assert.Equal(194, overlap.TotalStitchedLength);


        }

        private BaseStitcher.OverlapBoundary ExecuteOverlapTest(int read1Position, string read1Cigar, int read2Position, string read2Cigar)
        {
            var read1CigarAlignment = new CigarAlignment(read1Cigar);
            var read1 = TestHelper.CreateRead("chr1",
                string.Join(string.Empty, Enumerable.Repeat("A", (int)read1CigarAlignment.GetReadSpan())), read1Position,
                read1CigarAlignment);

            var read2CigarAlignment = new CigarAlignment(read2Cigar);
            var read2 = TestHelper.CreateRead("chr1",
                string.Join(string.Empty, Enumerable.Repeat("A", (int)read2CigarAlignment.GetReadSpan())), read2Position,
                read2CigarAlignment);

            var stitcher = new BasicStitcher(10);
            return stitcher.GetOverlapBoundary(read1, read2);
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
        public void TryStitch_WithXCTag()
        {
            const string xcTagDiffFromCalculated = "4M2I4M";
            const string expectedCalculatedCigar = "10M";
            var read1 = TestHelper.CreateRead("chr1", "ATCGATCG", 12345,
                new CigarAlignment("8M"), qualityForAll: 30);

            var read2_overlap = TestHelper.CreateRead("chr1", "ATCGTT", 12349,
                new CigarAlignment("6M"), qualityForAll: 30);

            var stitcher = StitcherTestHelpers.GetStitcher(10);

            // -----------------------------------------------
            // XC tag is available, and matching between R1 and R2, and expected length,
            // but is different from the cigar string we would have calculated
            // -----------------------------------------------
            // XC tag should be taken
            read1.StitchedCigar = new CigarAlignment(xcTagDiffFromCalculated);
            read2_overlap.StitchedCigar = new CigarAlignment(xcTagDiffFromCalculated);

            var alignmentSet = new AlignmentSet(read1, read2_overlap);
            stitcher.TryStitch(alignmentSet);

            var mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(xcTagDiffFromCalculated, mergedRead.CigarData.ToString());

            // -----------------------------------------------
            // XC tag is there, and matching between R1 and R2, but not expected length
            // -----------------------------------------------
            // XC tag should be ignored if it is not expected length, and new cigar should be calculated
            read1.StitchedCigar = new CigarAlignment("8M");
            read2_overlap.StitchedCigar = new CigarAlignment("8M");
            alignmentSet = new AlignmentSet(read1, read2_overlap);
            stitcher.TryStitch(alignmentSet);

            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(expectedCalculatedCigar, mergedRead.CigarData.ToString());

            // -----------------------------------------------
            // XC tag is there on one read but not the other
            // -----------------------------------------------
            // XC tag should be ignored, and new cigar should be calculated
            read1.StitchedCigar = null;
            read2_overlap.StitchedCigar = new CigarAlignment("9M1I");
            alignmentSet = new AlignmentSet(read1, read2_overlap);
            stitcher.TryStitch(alignmentSet);

            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(expectedCalculatedCigar, mergedRead.CigarData.ToString());

            read1.StitchedCigar = new CigarAlignment("9M1I");
            read2_overlap.StitchedCigar = null;
            alignmentSet = new AlignmentSet(read1, read2_overlap);
            stitcher.TryStitch(alignmentSet);

            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(expectedCalculatedCigar, mergedRead.CigarData.ToString());

            // -----------------------------------------------
            // XC tag is not there
            // -----------------------------------------------
            // New cigar should be calculated            
            read1.StitchedCigar = null;
            read2_overlap.StitchedCigar = null;
            alignmentSet = new AlignmentSet(read1, read2_overlap);
            stitcher.TryStitch(alignmentSet);

            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(expectedCalculatedCigar, mergedRead.CigarData.ToString());
        }

        [Fact]
        public void TryStitch_CalculateStitchedCigar()
        {
            // -----------------------------------------------
            // Read position maps disagree
            // -----------------------------------------------
            // Should throw out the pair
            var read1 = TestHelper.CreateRead("chr1", "ATCGATCG", 12345,
                new CigarAlignment("2M2D3M1D3M"), qualityForAll: 30); //Within the overlap, we have a deletion so there will be a shifting of positions from that point on

            var read2 = TestHelper.CreateRead("chr1", "ATCGATCG", 12349,
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
            read1 = TestHelper.CreateRead("chr1", "ATCGATCG", 12341,
                new CigarAlignment("1M2I5M"), qualityForAll: 30);

            read2 = TestHelper.CreateRead("chr1", "ATCGATCG", 12342,
                new CigarAlignment("5M1I2M"), qualityForAll: 30);

            stitcher = StitcherTestHelpers.GetStitcher(10);
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);

            Assert.Equal("1M2I5M1I2M", StitcherTestHelpers.GetMergedRead(alignmentSet).CigarData.ToString());
        }

        [Fact]
        public void TryStitch_ConsensusSequence()
        {
            // 1234...   1 - - 2 3 4 5 6 - - 7 8 9 0 //Reference Positions
            // Read1     X X X X X X X X - - - - -
            // Read1     M I I M M M M M - - - - -
            // Read1     A T C G A T C G - - - - -
            // Read2     - - - X X X X X X X X - -
            // Read2     - - - M M M M M I M M - -
            // Read2     - - - A T C G A T C G - -

            var r1qualities = 30;
            var r2qualities = 20;

            var read1 = TestHelper.CreateRead("chr1", "TTTTTTTT", 12341,
                new CigarAlignment("1M2I5M"), qualityForAll: (byte)r1qualities);

            var read2 = TestHelper.CreateRead("chr1", "AAAAAAAA", 12342,
                new CigarAlignment("5M1I2M"), qualityForAll: (byte) r2qualities);

            var stitcher = StitcherTestHelpers.GetStitcher(10);
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
            Assert.Equal("TTT",mergedRead.Sequence.Substring(0,overlapStart));

            //Consensus sequence should have everything from read2 for positions after overlap
            Assert.Equal("AAA",mergedRead.Sequence.Substring(overlapEnd,3));

            //Consensus sequence should have an N where we have two high-quality (both above min) disagreeing bases
            Assert.Equal("NNNNN", mergedRead.Sequence.Substring(overlapStart, 5));

            //Consensus sequence should have 0 quality where we have two high-quality (both above min) disagreeing bases
            Assert.True(mergedRead.Qualities.Take(overlapStart).All(q => q == r1qualities));
            Assert.True(mergedRead.Qualities.Skip(overlapStart).Take(overlapLength).All(q=>q == 0));
            Assert.True(mergedRead.Qualities.Skip(overlapEnd).Take(mergedRead.Sequence.Length - overlapEnd).All(q => q == r2qualities));

            //Consensus sequence should take higher quality base if one or more of the bases is below min quality
            
            //Read 2 trumps whole overlap
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 5, 5, 5, 5 };
            read2.BamAlignment.Qualities = new byte[] { 40, 40, 40, 40, 40, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(read2.Sequence.Substring(0,5),mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal("TTTAAAAAAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(new byte[] { 30, 30, 30, 40, 40, 40, 40, 40, 20, 19, 18 }, mergedRead.Qualities);

            //Read 1 trumps whole overlap
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 40, 40, 40, 40, 40 };
            read2.BamAlignment.Qualities = new byte[] { 5, 5, 5, 5, 5, 20, 19, 18 }; 
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(read1.Sequence.Substring(3, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal("TTTTTTTTAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(new byte[] { 30, 30, 30, 40, 40, 40, 40, 40, 20, 19, 18 }, mergedRead.Qualities);

            //Little bit of each
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 45, 5, 45, 5 };
            read2.BamAlignment.Qualities = new byte[] { 40, 5, 40, 5, 40, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal("TTTATATAAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(new byte[] { 30, 30, 30, 40, 45, 40, 45, 40, 20, 19, 18 }, mergedRead.Qualities);

            //Consensus sequence should take base and assign the higher quality if both bases agree
            var read2_agreeingBases = TestHelper.CreateRead("chr1", "TTTTTTTT", 12342,
                new CigarAlignment("5M1I2M"), new byte[] { 40, 5, 40, 5, 40, 20, 19, 18 });
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 45, 5, 45, 5 };
            alignmentSet = new AlignmentSet(read1, read2_agreeingBases);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal("TTTTTTTTTTT", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(new byte[] { 30, 30, 30, 40, 45, 40, 45, 40, 20, 19, 18 }, mergedRead.Qualities);

            //Bases disagree and both are below minimum quality, read1>read2 : take base/q from read1
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 8, 8, 8, 8, 8 };
            read2.BamAlignment.Qualities = new byte[] { 5, 5, 5, 5, 5, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(read1.Sequence.Substring(3, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal("TTTTTTTTAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(new byte[] { 30, 30, 30, 8, 8, 8, 8, 8, 20, 19, 18 }, mergedRead.Qualities);

            //Bases disagree and both are below minimum quality, read2>read1 : take base/q from read2
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 5, 5, 5, 5 };
            read2.BamAlignment.Qualities = new byte[] { 8, 8, 8, 8, 8, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(read2.Sequence.Substring(0, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal("TTTAAAAAAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(new byte[] { 30, 30, 30, 8, 8, 8, 8, 8, 20, 19, 18 }, mergedRead.Qualities);

            //Bases disagree and both are below minimum quality, read1==read2 : take base/q from read1
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 5, 5, 5, 5 };
            read2.BamAlignment.Qualities = new byte[] { 5, 5, 5, 5, 5, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(read1.Sequence.Substring(3, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal("TTTTTTTTAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(new byte[] { 30, 30, 30, 5, 5, 5, 5, 5, 20, 19, 18 }, mergedRead.Qualities);

        }
    }
}
