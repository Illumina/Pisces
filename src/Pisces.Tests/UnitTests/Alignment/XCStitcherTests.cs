using System;
using System.Linq;
using Pisces.Interfaces;
using SequencingFiles;
using Pisces.Domain.Models;
using Pisces.Domain.Tests;
using Pisces.IO;
using Xunit;

namespace Pisces.Tests.UnitTests.Alignment
{
    public class XCStitcherTests
    {
        private IAlignmentStitcher GetXCStitcher()
        {
            return StitcherTestHelpers.GetStitcher(10, true);
        }

        [Fact]
        public void TryStitch_NoXC_Stitchable()
        {
            var xcStitcherXcRequired = GetXCStitcher();

            //Reads without XC tags that do overlap should be added separately in XC stitcher when xc is required
            var alignmentSet = StitcherTestHelpers.GetOverlappingReadSet();
            StitcherTestHelpers.TryStitchAndAssertFailed(xcStitcherXcRequired, alignmentSet);
        }

        [Fact]
        public void TryStitch_WithXCTag()
        {
            const string xcTagDiffFromCalculated = "4M2I4M";
            const string expectedCalculatedCigar = "10M";

            var xcRequiredStitcher = GetXCStitcher();

            // -----------------------------------------------
            // XC tag is available, and matching between R1 and R2, and expected length,
            // but is different from the cigar string we would have calculated
            // -----------------------------------------------
           
            // [If require XC]
            // XC tag should be taken
            var alignmentSet = StitcherTestHelpers.GetOverlappingReadSet();
            ((Read)alignmentSet.PartnerRead1).StitchedCigar = new CigarAlignment(xcTagDiffFromCalculated);
            ((Read)alignmentSet.PartnerRead2).StitchedCigar = new CigarAlignment(xcTagDiffFromCalculated);
            xcRequiredStitcher.TryStitch(alignmentSet);

            Assert.Equal(1, alignmentSet.ReadsForProcessing.Count);
            var mergedRead = alignmentSet.ReadsForProcessing.First();
            Assert.Equal(xcTagDiffFromCalculated, ((Read)mergedRead).CigarData.ToString());

            // -----------------------------------------------
            // XC tag is there, and matching between R1 and R2, but not expected length
            // -----------------------------------------------
            // [If not require XC]
            //bounce back to processing separately?
            //alignmentSet = GetOverlappingReadSet();
            //alignmentSet.PartnerRead1.StitchedCigarString = "8M";
            //alignmentSet.PartnerRead2.StitchedCigarString = "8M";
            //stitcher.TryStitch(alignmentSet);
            //Assert.Equal(2, alignmentSet.ReadsForProcessing.Count);
            // [If require XC]
            //???

            // -----------------------------------------------
            // XC tag is there on one read but not the other
            // -----------------------------------------------

            // [If require XC]
            // should bounce back to separate processing
            alignmentSet = StitcherTestHelpers.GetOverlappingReadSet();
            ((Read)alignmentSet.PartnerRead1).StitchedCigar = new CigarAlignment("4M2I4M");
            ((Read)alignmentSet.PartnerRead2).StitchedCigar = null;

            StitcherTestHelpers.TryStitchAndAssertFailed(xcRequiredStitcher, alignmentSet);

            alignmentSet = StitcherTestHelpers.GetOverlappingReadSet();
            ((Read)alignmentSet.PartnerRead1).StitchedCigar = null;
            ((Read)alignmentSet.PartnerRead2).StitchedCigar = new CigarAlignment("4M2I4M");

            StitcherTestHelpers.TryStitchAndAssertFailed(xcRequiredStitcher, alignmentSet);

            // -----------------------------------------------
            // XC tag is not there
            // -----------------------------------------------
         
            // [If require XC]
            // should bounce back to separate processing
            alignmentSet = StitcherTestHelpers.GetOverlappingReadSet();
            ((Read)alignmentSet.PartnerRead1).StitchedCigar = null;
            ((Read)alignmentSet.PartnerRead2).StitchedCigar = null;

            StitcherTestHelpers.TryStitchAndAssertFailed(xcRequiredStitcher, alignmentSet);

            // -----------------------------------------------
            // XC tag does not match between read1 and read2
            // -----------------------------------------------

            // [If require XC]
            // should bounce back to separate processing
            alignmentSet = StitcherTestHelpers.GetOverlappingReadSet();
            ((Read)alignmentSet.PartnerRead1).StitchedCigar = new CigarAlignment("4M2I4M");
            ((Read)alignmentSet.PartnerRead2).StitchedCigar = new CigarAlignment("9M1I");

            StitcherTestHelpers.TryStitchAndAssertFailed(xcRequiredStitcher, alignmentSet);
        }

        [Fact]
        public void TryStitch_NoXC_Unstitchable()
        {

            var read1 = DomainTestHelper.CreateRead("chr1", "ATCGATCG", 12345, new CigarAlignment("8M"), qualityForAll:30);

            var read2_noOverlap = DomainTestHelper.CreateRead("chr1", "A", 2384, new CigarAlignment("1M"), qualityForAll: 30);

            var read2_overlap = DomainTestHelper.CreateRead("chr1", "ATCGTT", 12349, new CigarAlignment("6M"), qualityForAll: 30);

            var read2_diffChrom = DomainTestHelper.CreateRead("chr2", "ATCGTT", 12349, new CigarAlignment("6M"), qualityForAll: 30);

            var read2_nonOverlap_border = DomainTestHelper.CreateRead("chr1", "AT", 12343, new CigarAlignment("2M"), qualityForAll: 30);

            var stitcher = StitcherTestHelpers.GetStitcher(10,true);

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
            StitcherTestHelpers.TryStitchAndAssertAddedSeparately(stitcher, alignmentSet);

            // -----------------------------------------------
            // No overlap, reads are directly neighboring
            // -----------------------------------------------
            // Shouldn't stitch
            alignmentSet = new AlignmentSet(read1, read2_nonOverlap_border);
            StitcherTestHelpers.TryStitchAndAssertAddedSeparately(stitcher, alignmentSet);

            // -----------------------------------------------
            // No overlap, reads on diff chromosomes
            // -----------------------------------------------
            // Should throw exception
            alignmentSet = new AlignmentSet(read1, read2_diffChrom);
            var ex = Assert.Throws<ArgumentException>(() => stitcher.TryStitch(alignmentSet));
            Assert.Contains("Partner reads are from different chromosomes", ex.Message, StringComparison.InvariantCultureIgnoreCase); // This is brittle but since a variety of exceptions can happen in this process want to make sure it's this specific one
            


        }

        [Fact]
        public void TryStitch_CalculateStitchedCigar()
        {
            // -----------------------------------------------
            // Read position maps disagree
            // -----------------------------------------------

            var read1 = DomainTestHelper.CreateRead("chr1", "ATCGATCG", 12345, new CigarAlignment("2M2D3M1D3M"), qualityForAll: 30);

            var read2 = DomainTestHelper.CreateRead("chr1", "ATCGATCG", 12349, new CigarAlignment("8M"), qualityForAll: 30);

            // [If require XC] 
            // We never calculate stitched cigar anyway. Bounce out to processing separately.
            var stitcher = GetXCStitcher();
            var alignmentSet = new AlignmentSet(read1, read2);
            StitcherTestHelpers.TryStitchAndAssertFailed(stitcher,alignmentSet);

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

            // [If require XC] 
            // No stitched cigar, and separate reads
            read1 = DomainTestHelper.CreateRead("chr1", "ATCGATCG", 12341, new CigarAlignment("1M2I5M"), qualityForAll: 30);

            read2 = DomainTestHelper.CreateRead("chr1", "ATCGATCG", 12342, new CigarAlignment("5M1I2M"), qualityForAll: 30);

            stitcher = GetXCStitcher();
            alignmentSet = new AlignmentSet(read1, read2);
            StitcherTestHelpers.TryStitchAndAssertFailed(stitcher, alignmentSet);
        }

        [Fact]
        [Trait("ReqID","SDS-32")]
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

            var read1 = DomainTestHelper.CreateRead("chr1", "TTTTTTTT", 12341, new CigarAlignment("1M2I5M"), qualityForAll: (byte)r1qualities);
            read1.StitchedCigar = new CigarAlignment("1M2I5M1I2M");

            var read2 = DomainTestHelper.CreateRead("chr1", "AAAAAAAA", 12342, new CigarAlignment("5M1I2M"), qualityForAll: (byte)r2qualities);
            read2.StitchedCigar = new CigarAlignment("1M2I5M1I2M");

            var stitcher = GetXCStitcher();
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
            Assert.Equal("NNNNN", mergedRead.Sequence.Substring(overlapStart, 5));

            //Consensus sequence should have 0 quality where we have two high-quality (both above min) disagreeing bases
            Assert.True(mergedRead.Qualities.Take(overlapStart).All(q => q == r1qualities));
            Assert.True(mergedRead.Qualities.Skip(overlapStart).Take(overlapLength).All(q => q == 0));
            Assert.True(mergedRead.Qualities.Skip(overlapEnd).Take(mergedRead.Sequence.Length - overlapEnd).All(q => q == r2qualities));

            //Consensus sequence should take :
            // base and quality from read2 if read1 base is below min quality
            // base and quality from read1 if read2 base is below min quality (even if read2 base is actually greater than read1)
            // N and 0 if read1 and read2 bases are both above min quality and they disagree

            //Read 2 trumps whole overlap
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 5, 5, 5, 5 };
            read2.BamAlignment.Qualities = new byte[] { 40, 40, 40, 40, 40, 20, 19, 18 };
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(read2.Sequence.Substring(0, 5), mergedRead.Sequence.Substring(overlapStart, 5));
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
            var read2_agreeingBases = DomainTestHelper.CreateRead("chr1", "TTTTTTTT", 12342, 
                new CigarAlignment("5M1I2M"), new byte[] { 40, 5, 40, 5, 40, 20, 19, 18 });
            read2_agreeingBases.StitchedCigar = new CigarAlignment("1M2I5M1I2M");

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
