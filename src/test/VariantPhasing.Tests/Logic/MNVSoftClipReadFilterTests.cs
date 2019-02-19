using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using TestUtilities;
using VariantPhasing.Interfaces;
using VariantPhasing.Logic;

namespace VariantPhasing.Tests.Logic
{
    public class MNVSoftClipReadFilterTests
    {
        [Fact]
        public void IsReadClippedAtMNVSite_NoSoftclips()
        {
            IMNVSoftClipReadFilter scReadFilter = new MNVSoftClipReadFilter();
            // SNV:
            // pos   |15   20
            // ref   |ACTGAGACTGA
            // alt   |ACTGAAACTGA
            var snv = TestHelper.CreateDummyAllele("chr1", 20, "G", "A", 2000, 50);
            var del = TestHelper.CreateDummyAllele("chr1", 20, "GAC", "G", 2000, 50);
            var ins = TestHelper.CreateDummyAllele("chr1", 20, "G", "GTA", 2000, 50);
            var homopolymerDel = TestHelper.CreateDummyAllele("chr1", 20, "GGGGG", "G", 2000, 50);
            var homopolymerIns = TestHelper.CreateDummyAllele("chr1", 20, "G", "GGGGG", 2000, 50);

            // Read is ref haplotype (11M)
            var refRead = TestHelper.CreateRead("chr1", "ACTGAGACTGA", 15, new CigarAlignment("11M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(refRead, snv));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(refRead, del));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(refRead, ins));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(refRead, homopolymerDel));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(refRead, homopolymerIns));

            // Read contains mismatch, insertion, or deletion (no clipping)
            // Mismatch
            // pos   |15   20
            // ref   |ACTGAGACTGA
            // read  |ACTGAAACTGA
            var mismatchRead = TestHelper.CreateRead("chr1", "ACTGAAACTGA", 15, new CigarAlignment("5M1X5M"));  // 20: G > A
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(mismatchRead, snv));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(mismatchRead, del));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(mismatchRead, ins));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(mismatchRead, homopolymerDel));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(mismatchRead, homopolymerIns));
            // Deletion
            // pos   |15   20
            // ref   |ACTGAGACTGA
            // read  |ACTGAG--TGA
            var delRead = TestHelper.CreateRead("chr1", "ACTGAGTGA", 15, new CigarAlignment("6M2D3M")); // 20: GAC > G
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delRead, snv));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delRead, del));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delRead, ins));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delRead, homopolymerDel));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delRead, homopolymerIns));
            // Insertion
            // pos   |15   20
            // ref   |ACTGAG--ACTGA
            // read  |ACTGAGTGACTGA
            var insRead = TestHelper.CreateRead("chr1", "ACTGAGTGACTGA", 15, new CigarAlignment("6M2I5M")); // 20: G>GTG
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insRead, snv));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insRead, del));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insRead, ins));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insRead, homopolymerDel));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insRead, homopolymerIns));




        }

        [Fact]
        public void IsReadClippedAtMNVSite_SNV()
        {
            IMNVSoftClipReadFilter scReadFilter = new MNVSoftClipReadFilter();
            // SNV:
            // pos   |15   20
            // ref   |ACTGAGACTGA
            // alt   |ACTGAAACTGA
            var snv = TestHelper.CreateDummyAllele("chr1", 20, "G", "A", 2000, 50);

            // Corner cases for SNV

            // startsWith5S
            // pos     |15   20
            // snvRead |ACTGAAACTGA
            // cigar   |SSSSSXMMMMM
            var startsWith5S = TestHelper.CreateRead("chr1", "ACTGAAACTGA", 20, new CigarAlignment("5S1X5M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(startsWith5S, snv));
            // startsWith6S
            // pos     |15   20
            // snvRead |ACTGAAACTGA
            // cigar   |SSSSSSMMMMM
            var startsWith6S = TestHelper.CreateRead("chr1", "ACTGAAACTGA", 21, new CigarAlignment("6S5M"));
            Assert.Equal((true, false), scReadFilter.IsReadClippedAtMNVSite(startsWith6S, snv));
            // startsWith7S
            // pos     |15   20
            // snvRead |ACTGAAACTGA
            // cigar   |SSSSSSSMMMM
            var startsWith7S = TestHelper.CreateRead("chr1", "ACTGAAACTGA", 22, new CigarAlignment("7S4M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(startsWith7S, snv));
            // endsWith5S
            // pos     |15   20
            // snvRead |ACTGAAACTGA
            // cigar   |MMMMMXSSSSS
            var endsWith5S = TestHelper.CreateRead("chr1", "ACTGAAACTGA", 15, new CigarAlignment("5M1X5S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(endsWith5S, snv));
            // endsWith6S
            // pos     |15   20
            // snvRead |ACTGAAACTGA
            // cigar   |MMMMMSSSSSS
            var endsWith6S = TestHelper.CreateRead("chr1", "ACTGAAACTGA", 15, new CigarAlignment("5M6S"));
            Assert.Equal((false, true), scReadFilter.IsReadClippedAtMNVSite(endsWith6S, snv));
            // endsWith7S
            // pos     |15   20
            // snvRead |ACTGAAACTGA
            // cigar   |MMMMSSSSSSS
            var endsWith7S = TestHelper.CreateRead("chr1", "ACTGAAACTGA", 15, new CigarAlignment("4M7S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(endsWith7S, snv));


        }

        [Fact]
        public void IsReadClippedAtMNVSite_Del()
        {
            IMNVSoftClipReadFilter scReadFilter = new MNVSoftClipReadFilter();
            // SNV:
            // pos   |15   20
            // ref   |ACTGAGACTGA
            var del = TestHelper.CreateDummyAllele("chr1", 20, "GAC", "G", 2000, 50);

            // Corner cases for Deletion
            // delStartsWith5S
            // pos     |15   20
            // ref     |ACTGAGACTGA 
            // delRead |ACTGAG--TGA     // Correct alignment
            // delRead |  ACTGAGTGA     // Current alignment
            // cigar   |  SSSSSXMMM
            var delStartsWith5S = TestHelper.CreateRead("chr1", "ACTGAGTGA", 22, new CigarAlignment("5S1X3M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delStartsWith5S, del));
            // delStartsWith6S
            // pos     |15   20
            // ref     |ACTGAGACTGA 
            // delRead |ACTGAG--TGA     // Correct alignment
            // cigar   |  SSSSSSMMM
            var delStartsWith6S = TestHelper.CreateRead("chr1", "ACTGAGTGA", 23, new CigarAlignment("6S3M"));
            Assert.Equal((true, false), scReadFilter.IsReadClippedAtMNVSite(delStartsWith6S, del));
            // delStartsWith7S
            // pos     |15   20
            // ref     |ACTGAGACTGA 
            // delRead |ACTGAG--TGA     // Correct alignment
            // delRead |  ACTGAGTGA     // Current alignment
            // cigar   |  SSSSSSSMM
            var delStartsWith7S = TestHelper.CreateRead("chr1", "ACTGAGTGA", 24, new CigarAlignment("7S2M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delStartsWith7S, del));

            // delEndsWith4S
            // pos     |15   20
            // ref     |ACTGAGACTGA 
            // delRead |ACTGAG--TGA     // Correct alignment
            // delRead |  ACTGAGTGA     // Current alignment
            // cigar   |  MMMMMSSSS
            var delEndsWith4S = TestHelper.CreateRead("chr1", "ACTGAGTGA", 17, new CigarAlignment("5M4S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delEndsWith4S, del));
            // delEndsWith5S
            // pos     |15   20
            // ref     |ACTGAGACTGA 
            // delRead |ACTGAG--TGA     // Correct alignment
            // delRead |  ACTGAGTGA     // Current alignment
            // cigar   |  MMMMSSSSS
            var delEndsWith5S = TestHelper.CreateRead("chr1", "ACTGAGTGA", 17, new CigarAlignment("4M5S"));
            Assert.Equal((false, true), scReadFilter.IsReadClippedAtMNVSite(delEndsWith5S, del));
            // delEndsWith6S
            // pos     |15   20
            // ref     |ACTGAGACTGA 
            // delRead |ACTGAG--TGA     // Correct alignment
            // delRead |  ACTGAGTGA     // Current alignment
            // cigar   |  MMMSSSSSS
            var delEndsWith6S = TestHelper.CreateRead("chr1", "ACTGAGTGA", 17, new CigarAlignment("3M6S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delEndsWith6S, del));


            // Reads that start and end with soft clip
            // delEndsWith5SStartsWith4S (End clipping site matches MNV)
            // pos     |    15   20
            // ref     |ACTGACTGAGACTGA 
            // delRead |CCCCACTGAG--TGA     // Correct alignment
            // delRead |  CCCCACTGAGTGA     // Current alignment
            // cigar   |  SSSSMMMMSSSSS
            var delEndsWith5SStartsWith4S = TestHelper.CreateRead("chr1", "CCCCACTGAGTGA", 17, new CigarAlignment("4S4M5S"));
            Assert.Equal((false, true), scReadFilter.IsReadClippedAtMNVSite(delEndsWith5SStartsWith4S, del));
            // delStartsWith6SEndsWith4S (Start clipping site matches MNV)
            // pos     |15   20
            // ref     |ACTGAGACTGAACTG 
            // delRead |ACTGAG--TGACCCC     // Correct alignment
            // cigar   |  SSSSSSMMMSSSS
            var delStartsWith6SEndsWith4S = TestHelper.CreateRead("chr1", "ACTGAGTGACCCC", 23, new CigarAlignment("6S3M4S"));
            Assert.Equal((true, false), scReadFilter.IsReadClippedAtMNVSite(delStartsWith6SEndsWith4S, del));
            // delEndsWith6SStartsWith4S (neither clipping site matches MNV)
            // pos     |    15   20
            // ref     |ACTGACTGAGACTGA 
            // delRead |CCCCACTGAG--TGA     // Correct alignment
            // delRead |  CCCCACTGAGTGA     // Current alignment
            // cigar   |  SSSSMMMSSSSSS
            var delEndsWith6SStartsWith4S = TestHelper.CreateRead("chr1", "CCCCACTGAGTGA", 17, new CigarAlignment("4S3M6S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(delEndsWith6SStartsWith4S, del));
        }

        [Fact]
        public void IsReadClippedAtMNVSite_Ins()
        {
            IMNVSoftClipReadFilter scReadFilter = new MNVSoftClipReadFilter();
            // SNV:
            // pos   |15   20
            // ref   |ACTGAGACTGA
            var ins = TestHelper.CreateDummyAllele("chr1", 20, "G", "GTA", 2000, 50);

            // Corner cases for insertion
            // insStartsWith7S
            // pos     |15   20
            // ref     |ACTGAG--ACTGA 
            // insRead |ACTGAGTAACTGA   // Correct alignment
            // insRead |ACTGAGTAACTGA   // Current alignment
            // cigar   |SSSSSSSXMMMMM
            var insStartsWith7S = TestHelper.CreateRead("chr1", "ACTGAGTAACTGA", 20, new CigarAlignment("7S1X5M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insStartsWith7S, ins));
            // insStartsWith8S
            // pos     |15   20
            // ref     |ACTGAG--ACTGA 
            // insRead |ACTGAGTAACTGA   // Correct alignment
            // insRead |ACTGAGTAACTGA   // Current alignment
            // cigar   |SSSSSSSSMMMMM
            var insStartsWith8S = TestHelper.CreateRead("chr1", "ACTGAGTAACTGA", 21, new CigarAlignment("8S5M"));
            Assert.Equal((true, false), scReadFilter.IsReadClippedAtMNVSite(insStartsWith8S, ins));
            // insStartsWith9S
            // pos     |15   20
            // ref     |ACTGAG--ACTGA 
            // insRead |ACTGAGTAACTGA   // Correct alignment
            // insRead |ACTGAGTAACTGA   // Current alignment
            // cigar   |SSSSSSSSSMMMM
            var insStartsWith9S = TestHelper.CreateRead("chr1", "ACTGAGTAACTGA", 22, new CigarAlignment("9S4M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insStartsWith9S, ins));
            // insEndsWith6S
            // pos     |15   20
            // ref     |ACTGAG--ACTGA 
            // insRead |ACTGAGTAACTGA   // Correct alignment
            // insRead |ACTGAGTAACTGA   // Current alignment
            // cigar   |MMMMMMXSSSSSS
            var insEndsWith6S = TestHelper.CreateRead("chr1", "ACTGAGTAACTGA", 15, new CigarAlignment("6M1X6S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insEndsWith6S, ins));
            // insEndsWith7S
            // pos     |15   20
            // ref     |ACTGAG--ACTGA 
            // insRead |ACTGAGTAACTGA   // Correct alignment
            // insRead |ACTGAGTAACTGA   // Current alignment
            // cigar   |MMMMMMSSSSSSS
            var insEndsWith7S = TestHelper.CreateRead("chr1", "ACTGAGTAACTGA", 15, new CigarAlignment("6M7S"));
            Assert.Equal((false, true), scReadFilter.IsReadClippedAtMNVSite(insEndsWith7S, ins));
            // insEndsWith8S
            // pos     |15   20
            // ref     |ACTGAG--ACTGA 
            // insRead |ACTGAGTAACTGA   // Correct alignment
            // insRead |ACTGAGTAACTGA   // Current alignment
            // cigar   |MMMMMSSSSSSSS
            var insEndsWith8S = TestHelper.CreateRead("chr1", "ACTGAGTAACTGA", 15, new CigarAlignment("5M8S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insEndsWith8S, ins));


            // Reads that start and end with soft clip
            // insEndsWith7SStartsWith3S (End clipping site matches MNV)
            // pos     |   15   20
            // ref     |ACTACTGAG--ACTGA 
            // insRead |CCCACTGAGTAACTGA   // Correct alignment
            // insRead |CCCACTGAGTAACTGA   // Current alignment
            // cigar   |SSSMMMMMMSSSSSSS
            var insEndsWith7SStartsWith3S = TestHelper.CreateRead("chr1", "CCCACTGAGTAACTGA", 15, new CigarAlignment("3S6M7S"));
            Assert.Equal((false, true), scReadFilter.IsReadClippedAtMNVSite(insEndsWith7SStartsWith3S, ins));
            // insStartsWith8SEndsWith3S (start clipping site matches MNV)
            // pos     |15   20
            // ref     |ACTGAG--ACTGAACT 
            // insRead |ACTGAGTAACTGACCC   // Correct alignment
            // insRead |ACTGAGTAACTGACCC   // Current alignment
            // cigar   |SSSSSSSSMMMMMSSS
            var insStartsWith8SEndsWith3S = TestHelper.CreateRead("chr1", "ACTGAGTAACTGACCC", 21, new CigarAlignment("8S5M3S"));
            Assert.Equal((true, false), scReadFilter.IsReadClippedAtMNVSite(insStartsWith8SEndsWith3S, ins));
            // insStartsWith7SEndsWith3S (neither clipping site matches MNV)
            // pos     |15   20
            // ref     |ACTGAG--ACTGAACT 
            // insRead |ACTGAGTAACTGACCC   // Correct alignment
            // insRead |ACTGAGTAACTGACCC   // Current alignment
            // cigar   |SSSSSSSXMMMMMSSS
            var insStartsWith7SEndsWith3S = TestHelper.CreateRead("chr1", "ACTGAGTAACTGACCC", 20, new CigarAlignment("7S1X5M3S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(insStartsWith7SEndsWith3S, ins));
        }

        [Fact]
        public void IsReadClippedAtMNVSite_HomopolymerDel()
        {
            IMNVSoftClipReadFilter scReadFilter = new MNVSoftClipReadFilter();
            // Homopolymer deletion:
            // pos   |15   20
            // ref   |ACTGAGGGGGACTGA
            // alt   |ACTGAG----ACTGA
            var homopolymerDel = TestHelper.CreateDummyAllele("chr1", 20, "GGGGG", "G", 2000, 50);

            // Corner cases for Deletion in Homopolymer run
            // hdelStartsWith4S
            // pos      |15   20
            // ref      |ACTGAGGGGGACTGA
            // hdelRead |ACTGA----GACTGA     // alignment with visible indel
            // hdelRead |    ACTGAGACTGA     // alignment with clipping
            // cigar    |    SSSSXMMMMMM
            var hdelStartsWith4S = TestHelper.CreateRead("chr1", "ACTGAGACTGA", 23, new CigarAlignment("4S1X6M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(hdelStartsWith4S, homopolymerDel));
            // hdelStartsWith5S
            // pos      |15   20
            // ref      |ACTGAGGGGGACTGA
            // hdelRead |ACTGA----GACTGA     // alignment with visible indel
            // hdelRead |    ACTGAGACTGA     // alignment with clipping
            // cigar    |    SSSSSMMMMMM
            var hdelStartsWith5S = TestHelper.CreateRead("chr1", "ACTGAGACTGA", 24, new CigarAlignment("5S6M"));
            Assert.Equal((true, false), scReadFilter.IsReadClippedAtMNVSite(hdelStartsWith5S, homopolymerDel));
            // hdelStartsWith6S
            // pos      |15   20
            // ref      |ACTGAGGGGGACTGA
            // hdelRead |ACTGA----GACTGA     // alignment with visible indel
            // hdelRead |    ACTGAGACTGA     // alignment with clipping
            // cigar    |    SSSSSSMMMMM
            var hdelStartsWith6S = TestHelper.CreateRead("chr1", "ACTGAGACTGA", 25, new CigarAlignment("6S5M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(hdelStartsWith6S, homopolymerDel));
            // hdelEndssWith4S
            // pos      |15   20
            // ref      |ACTGAGGGGGACTGA
            // hdelRead |ACTGAG----ACTGA     // alignment with visible indel
            // hdelRead |ACTGAGACTGA     // alignment with clipping
            // cigar    |MMMMMMXSSSS
            var hdelEndssWith4S = TestHelper.CreateRead("chr1", "ACTGAGACTGA", 15, new CigarAlignment("6M1X4S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(hdelEndssWith4S, homopolymerDel));
            // hdelEndssWith5S
            // pos      |15   20
            // ref      |ACTGAGGGGGACTGA
            // hdelRead |ACTGAG----ACTGA     // alignment with visible indel
            // hdelRead |ACTGAGACTGA     // alignment with clipping
            // cigar    |MMMMMMSSSSS
            var hdelEndssWith5S = TestHelper.CreateRead("chr1", "ACTGAGACTGA", 15, new CigarAlignment("6M5S"));
            Assert.Equal((false, true), scReadFilter.IsReadClippedAtMNVSite(hdelEndssWith5S, homopolymerDel));
            // hdelEndssWith6S
            // pos      |15   20
            // ref      |ACTGAGGGGGACTGA
            // hdelRead |ACTGAG----ACTGA     // alignment with visible indel
            // hdelRead |ACTGAGACTGA     // alignment with clipping
            // cigar    |MMMMMSSSSSS
            var hdelEndssWith6S = TestHelper.CreateRead("chr1", "ACTGAGACTGA", 15, new CigarAlignment("5M6S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(hdelEndssWith6S, homopolymerDel));

        }
        [Fact]
        public void IsReadClippedAtMNVSite_HomopolymerIns()
        {
            IMNVSoftClipReadFilter scReadFilter = new MNVSoftClipReadFilter();
            // Homopolymer insertion:
            // pos   |15   20
            // ref   |ACTGAG----ACTGA
            // alt   |ACTGAGGGGGACTGA
            var homopolymerIns = TestHelper.CreateDummyAllele("chr1", 20, "G", "GGGGG", 2000, 50);

            // Corner cases for Homopolymer run Insertion
            // hinsStartsWith8S
            // pos      |15       20
            // ref      |ACTGA----GACTGA
            // hinsRead |ACTGAGGGGGACTGA     // alignment with visible indel
            // hinsRead |ACTGAGGGGGACTGA     // alignment with clipping
            // cigar    |SSSSSSSSXMMMMMM
            var hinsStartsWith8S = TestHelper.CreateRead("chr1", "ACTGAGGGGGACTGA", 19, new CigarAlignment("8S1X6M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(hinsStartsWith8S, homopolymerIns));
            // hinsStartsWith9S
            // pos      |15       20
            // ref      |ACTGA----GACTGA
            // hinsRead |ACTGAGGGGGACTGA     // alignment with visible indel
            // hinsRead |ACTGAGGGGGACTGA     // alignment with clipping
            // cigar    |SSSSSSSSSMMMMMM
            var hinsStartsWith9S = TestHelper.CreateRead("chr1", "ACTGAGGGGGACTGA", 20, new CigarAlignment("9S6M"));
            Assert.Equal((true, false), scReadFilter.IsReadClippedAtMNVSite(hinsStartsWith9S, homopolymerIns));
            // hinsStartsWith10S
            // pos      |15       20
            // ref      |ACTGA----GACTGA
            // hinsRead |ACTGAGGGGGACTGA     // alignment with visible indel
            // hinsRead |ACTGAGGGGGACTGA     // alignment with clipping
            // cigar    |SSSSSSSSSSMMMMM
            var hinsStartsWith10S = TestHelper.CreateRead("chr1", "ACTGAGGGGGACTGA", 21, new CigarAlignment("10S5M"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(hinsStartsWith10S, homopolymerIns));
            // hinsEndsWith8S
            // pos      |15   20
            // ref      |ACTGAG----ACTGA
            // hinsRead |ACTGAGGGGGACTGA     // alignment with visible indel
            // hinsRead |ACTGAGGGGGACTGA     // alignment with clipping
            // cigar    |MMMMMMXSSSSSSSS
            var hinsEndsWith8S = TestHelper.CreateRead("chr1", "ACTGAGGGGGACTGA", 15, new CigarAlignment("6M1X8S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(hinsEndsWith8S, homopolymerIns));
            // hinsEndsWith9S
            // pos      |15   20
            // ref      |ACTGAG----ACTGA
            // hinsRead |ACTGAGGGGGACTGA     // alignment with visible indel
            // hinsRead |ACTGAGGGGGACTGA     // alignment with clipping
            // cigar    |MMMMMMSSSSSSSSS
            var hinsEndsWith9S = TestHelper.CreateRead("chr1", "ACTGAGGGGGACTGA", 15, new CigarAlignment("6M9S"));
            Assert.Equal((false, true), scReadFilter.IsReadClippedAtMNVSite(hinsEndsWith9S, homopolymerIns));
            // hinsEndsWith10S
            // pos      |15   20
            // ref      |ACTGAG----ACTGA
            // hinsRead |ACTGAGGGGGACTGA     // alignment with visible indel
            // hinsRead |ACTGAGGGGGACTGA     // alignment with clipping
            // cigar    |MMMMMSSSSSSSSSS
            var hinsEndsWith10S = TestHelper.CreateRead("chr1", "ACTGAGGGGGACTGA", 15, new CigarAlignment("5M10S"));
            Assert.Equal((false, false), scReadFilter.IsReadClippedAtMNVSite(hinsEndsWith10S, homopolymerIns));

        }
    }

}