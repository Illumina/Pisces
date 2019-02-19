using System;	
using System.Collections.Generic;	
using System.Text;	
using Xunit;	
using VariantPhasing.Interfaces;	
using Moq;	
using Pisces.Domain.Models;	
using Pisces.Domain.Models.Alleles;	
using TestUtilities;	
using Alignment.Domain.Sequencing;	
using VariantPhasing.Logic;	
	
namespace VariantPhasing.Tests.Logic
{	
    public class MNVClippedReadComparatorTests
    {	
        [Fact]	
        public void DoesClippedReadSupportMNV_Happy()
        {	
            var mockSCReadFilterTrueSuffix = new Mock<IMNVSoftClipReadFilter>();	
            mockSCReadFilterTrueSuffix.Setup(x => x.IsReadClippedAtMNVSite(It.IsAny<Read>(), It.IsAny<CalledAllele>())).Returns((false, true));	
            IMNVClippedReadComparator clippedReadComparatorTrueSuffix = new MNVClippedReadComparator(mockSCReadFilterTrueSuffix.Object);	
	
            var mockSCReadFilterTruePrefix = new Mock<IMNVSoftClipReadFilter>();	
            mockSCReadFilterTruePrefix.Setup(x => x.IsReadClippedAtMNVSite(It.IsAny<Read>(), It.IsAny<CalledAllele>())).Returns((true, false));	
            IMNVClippedReadComparator clippedReadComparatorTruePrefix = new MNVClippedReadComparator(mockSCReadFilterTruePrefix.Object);	
	
            // MNV: chr1:20 CTGTTC>TGTTA	
            // Pos    |15   20	
            // Ref    |ACGTACTGTTCCAC	
            // Alt    |ACGTA-TGTTACAC	
            var mnv1 = TestHelper.CreateDummyAllele("chr1", 20, "CTGTTC", "TGTTA", 2000, 50);	
	
	
            // Read originates from MNV (5M8S)	
            // Pos    |15   20	
            // Algmnt |ACGTA-TGTTACAC	
            // Read   |ACGTATGTTACAC	
            // Cigar  |MMMMMSSSSSSSS	
            var read1 = TestHelper.CreateRead("chr1", "ACGTATGTTACAC", 15, new CigarAlignment("5M8S"));	
            Assert.Equal(true, clippedReadComparatorTrueSuffix.DoesClippedReadSupportMNV(read1, mnv1));	
            Assert.Equal(false, clippedReadComparatorTruePrefix.DoesClippedReadSupportMNV(read1, mnv1));	
	
            // Read with similar cigar score ^^, but clearly shifted (this will be caught by MNVSoftClipReadFilter)	
            // DoesClippedReadSupportMNV only cares about matching the sequence in the clipped portion to alt allele	
            // Pos    |15   20	
            // Algmnt |  ACGTA-TGTTACAC	
            // Read   |  ACGTATGTTACAC	
            // Cigar  |  MMMMMSSSSSSSS	
            read1 = TestHelper.CreateRead("chr1", "ACGTATGTTACAC", 17, new CigarAlignment("5M8S"));	
            Assert.Equal(true, clippedReadComparatorTrueSuffix.DoesClippedReadSupportMNV(read1, mnv1));	
            Assert.Equal(false, clippedReadComparatorTruePrefix.DoesClippedReadSupportMNV(read1, mnv1));	
	
            // Read with similar cigar score ^^ and correct alignment (no shift), but	
            // Clipped portion of read doesn't match alternate allele	
            // Pos    |15   20	
            // Algmnt |ACGTA-TGTTACAC	
            // Read   |ACGTATGATACAC	
            // Cigar  |MMMMMSSSSSSSS	
            read1 = TestHelper.CreateRead("chr1", "ACGTATGATACAC", 15, new CigarAlignment("5M8S"));	
            Assert.Equal(false, clippedReadComparatorTrueSuffix.DoesClippedReadSupportMNV(read1, mnv1));	
            Assert.Equal(false, clippedReadComparatorTruePrefix.DoesClippedReadSupportMNV(read1, mnv1));	
	
            // Clipped portion of read only covers part of alternate allele	
            // Pos    |15   20	
            // Algmnt |ACGTA-TGT	
            // Read   |ACGTATGT	
            // Cigar  |MMMMMSSS	
            read1 = TestHelper.CreateRead("chr1", "ACGTATGT", 15, new CigarAlignment("5M3S"));	
            Assert.Equal(false, clippedReadComparatorTrueSuffix.DoesClippedReadSupportMNV(read1, mnv1));	
            Assert.Equal(false, clippedReadComparatorTruePrefix.DoesClippedReadSupportMNV(read1, mnv1));	
	
            // Clipped portion of read exactly covers alternate allele	
            // Pos    |15   20	
            // Algmnt |ACGTA-TGTTA	
            // Read   |ACGTATGTTA	
            // Cigar  |MMMMMSSSSS	
            read1 = TestHelper.CreateRead("chr1", "ACGTATGTTA", 15, new CigarAlignment("5M5S"));	
            Assert.Equal(true, clippedReadComparatorTrueSuffix.DoesClippedReadSupportMNV(read1, mnv1));	
            Assert.Equal(false, clippedReadComparatorTruePrefix.DoesClippedReadSupportMNV(read1, mnv1));	
	
	
            // Example of matching read with clipping in start	
            // Pos    |15   20	
            // Algmnt |ACGTA-TGTTACAC	
            // Read   |ACGTACTGTTACAC	
            // Cigar  |SSSSSSSSSSSMMM	
            read1 = TestHelper.CreateRead("chr1", "ACGTACTGTTACAC", 15, new CigarAlignment("11S3M"));	
            Assert.Equal(true, clippedReadComparatorTruePrefix.DoesClippedReadSupportMNV(read1, mnv1));	
            Assert.Equal(false, clippedReadComparatorTrueSuffix.DoesClippedReadSupportMNV(read1, mnv1));	
	
            // Read originates from MNV and clipped in both beginning and end (4S5M8S)	
            // Pos    |    15   20	
            // Algmnt |ACTGACGTA-TGTTACAC	
            // Read   |CCCCACGTATGTTACAC	
            // Cigar  |SSSSMMMMMSSSSSSSS	
            read1 = TestHelper.CreateRead("chr1", "CCCCACGTATGTTACAC", 15, new CigarAlignment("4S5M8S"));	
            Assert.Equal(false, clippedReadComparatorTruePrefix.DoesClippedReadSupportMNV(read1, mnv1));	
            Assert.Equal(true, clippedReadComparatorTrueSuffix.DoesClippedReadSupportMNV(read1, mnv1));	
	
            // Example of matching read with clipping in start (and non matching clip at the end)	
            // Pos    |15   20	
            // Algmnt |ACGTA-TGTTACACACTGA	
            // Read   |ACGTACTGTTACACCCCCC	
            // Cigar  |SSSSSSSSSSSMMMSSSSS	
            read1 = TestHelper.CreateRead("chr1", "ACGTACTGTTACACCCCCC", 15, new CigarAlignment("11S3M5S"));	
            Assert.Equal(true, clippedReadComparatorTruePrefix.DoesClippedReadSupportMNV(read1, mnv1));	
            Assert.Equal(false, clippedReadComparatorTrueSuffix.DoesClippedReadSupportMNV(read1, mnv1));	
	
	
            // If SCReadFilter returns false,false, DoesClippedReadSupportMNV returns false	
            var mockSCReadFilterBothFalse = new Mock<IMNVSoftClipReadFilter>();	
            mockSCReadFilterBothFalse.Setup(x => x.IsReadClippedAtMNVSite(It.IsAny<Read>(), It.IsAny<CalledAllele>())).Returns((false,false));	
            IMNVClippedReadComparator clippedReadComparatorBothFalse = new MNVClippedReadComparator(mockSCReadFilterBothFalse.Object);	
            // True examples from above:	
	
            // Example of matching read with clipping at the end	
            // Pos    |15   20	
            // Algmnt |ACGTA-TGTTACAC	
            // Read   |ACGTACTGTTACAC	
            // Cigar  |SSSSSSSSSSSMMM	
            read1 = TestHelper.CreateRead("chr1", "ACGTACTGTTACAC", 15, new CigarAlignment("11S3M"));	
            Assert.Equal(false, clippedReadComparatorBothFalse.DoesClippedReadSupportMNV(read1, mnv1));	
	
            // Read originates from MNV (5M8S)	
            // Pos    |15   20	
            // Algmnt |ACGTA-TGTTACAC	
            // Read   |ACGTATGTTACAC	
            // Cigar  |MMMMMSSSSSSSS	
            read1 = TestHelper.CreateRead("chr1", "ACGTATGTTACAC", 15, new CigarAlignment("5M8S"));	
            Assert.Equal(false, clippedReadComparatorBothFalse.DoesClippedReadSupportMNV(read1, mnv1));	
        }	
    }	
}