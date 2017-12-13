using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Alignment.Domain.Sequencing;
using StitchingLogic.Models;
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
        public void GenerateConsensus_MatchSectionFullyOverlaps()
        {
            //       SSSSSMMMMMSS
            // SSSMMMIIIIIMMMM
            //       BCDEFGHIJKLM
            // abcdefxxxxxghij
            // abcDEFxxxxxGHIJKLM
            //TestStitching("3S3M5I4M", "abcdefxxxxxghij", "5S5M2S", "BCDEFGHIJKLM", 3, "3S3M5I5M2S", "3R12S3F", "aBCDEFxxxxxGHIJKLM");
            //TestStitching("5S5M2S", "BCDEFGHIJKLM", "3S3M5I4M", "abcdefxxxxxghij", -3, "3S3M5I5M2S", "11R3S3F", "abcdefxxxxxGHIJKLM");

            var r2 = "ACATTGGAAAGCATTCAGCACCTTCAGCTTATAGCAGAAAAACACAAGGGAGAGGGTAAGTTAGTAAATGATCTTCTTTTTTTCTGTATTAATAAAAGTNN";
            var r1 = "GAAAGCATTCAGCACCTTCAGCTTATAGCAGAAAAACACAAGGGAGAGGGTAAGTTAGTAAATGATCTTCTTTTTTTCTGTATTAATAAAAGTAATTTGCA";
            TestStitching("5S96M", r1, "3S3M5I88M2S", r2, -3, "3S3M5I96M", "11R88S8F", "ACATTGGAAAGCATTCAGCACCTTCAGCTTATAGCAGAAAAACACAAGGGAGAGGGTAAGTTAGTAAATGATCTTCTTTTTTTCTGTATTAATAAAAGTAATTTGCA");

            //Simplified case --fully overlapping, exact same stitched regions, slightly different sequence
            TestStitching("5S2M1I2M5S", "FFFFFfffffFFFFF", "5S2M1I2M5S", "RRRRRrrrrrRRRRR", 0, "5S2M1I2M5S", "5R5S5F", "RRRRRfffffFFFFF");
            // Real case -- fully overlapping, exact same stitched regions, slightly different sequence
            TestStitching("30S43M3I45M30S", "GACTGTGTCTCAGCAGTTATAATAAAATTGTCACTGTACTAAAAATACCATTAATAATAAAGTGTGGAAAGACATGATATTCAGCTCTCTCCAAAAGTAGCTATTTGGTTATTTATTGTTAGCTCATACGTTTTATGTATTTATAAAGCTC",
                "30S43M3I45M30S", "GACTGTGTCTCAGCAGTTATAATAAAATTGTCACTGTACTAAAAATACCATTAATAATAAAGTGTGGAAAGACATGATATTCAGCTCTCTCCAAAAGTAGCTATTTGGTTATTTAAAGTTAGCTCATACGTTTTATGTATTTATAAAGCTC",
                0, "30S43M3I45M30S", "30R91S30F",
                "GACTGTGTCTCAGCAGTTATAATAAAATTGTCACTGTACTAAAAATACCATTAATAATAAAGTGTGGAAAGACATGATATTCAGCTCTCTCCAAAAGTAGCTATTTGGTTATTTATTGTTAGCTCATACGTTTTATGTATTTATAAAGCTC");

            // Match section fully overlapping/exact, R1 PreSC starts before R2 PreSC, Posts same
            // SSMMSS
            // ABCDEF
            //  bcdef
            // AbCDEF
            TestStitching("2S2M2S", "ABCDEF", "1S2M2S", "bcdef", 0, "2S2M2S", "1F1R2S2F", "AbCDEF");

            // Match section fully overlapping/exact, R2 PreSC starts before R1 PreSC, Posts same
            // SSMMSS
            //  BCDEF
            // abcdef
            // abCDEF
            TestStitching("1S2M2S", "BCDEF", "2S2M2S", "abcdef", 0, "2S2M2S", "2R2S2F", "abCDEF");

            // Match section fully overlapping/exact, R1 PostSC ends before R2 PostSC, Pres same
            // SSMMSS
            // ABCDE
            // abcdef
            // abCDEf
            TestStitching("2S2M1S", "ABCDE", "2S2M2S", "abcdef", 0, "2S2M2S", "2R2S1F1R", "abCDEf");

            // Match section fully overlapping/exact, R2 PostSC ends before R1 PostSC, Pres same
            // SSMMSS
            // ABCDEF
            // abcde
            // abCDEF
            TestStitching("2S2M2S", "ABCDEF", "2S2M1S", "abcde", 0, "2S2M2S", "2R2S2F", "abCDEF");

            // Match section fully overlapping/exact, R1 PreSC starts before R2 PreSC, R1 PostSC ends before R2 PostSC
            // SSMMSS
            // ABCDE
            //  bcdef
            // AbCDEf
            TestStitching("2S2M1S", "ABCDE", "1S2M2S", "bcdef", 0, "2S2M2S", "1F1R2S1F1R", "AbCDEf");

            // Match section fully overlapping/exact, R1 PreSC starts before R2 PreSC, R2 PostSC ends before R1 PostSC
            // SSMMSS
            // ABCDEF
            //  bcde
            // AbCDEF
            TestStitching("2S2M2S", "ABCDEF", "1S2M1S", "bcde", 0, "2S2M2S", "1F1R2S2F", "AbCDEF");

            // Match section fully overlapping/exact, R2 PreSC starts before R1 PreSC, R2 PostSC ends before R1 PostSC
            // SSMMSS
            //  BCDEF
            // abcde
            // abCDEF
            TestStitching("1S2M2S", "BCDEF", "2S2M1S", "abcde", 0, "2S2M2S", "2R2S2F", "abCDEF");

            // SSMMSS
            //  BCDE
            // abcdef
            // abCDEf
            TestStitching("1S2M1S", "BCDE", "2S2M2S", "abcdef", 0, "2S2M2S", "2R2S1F1R", "abCDEf");

            // SSMMSS
            //  BCDE
            // abcde
            // abCDE
            TestStitching("1S2M1S", "BCDE", "2S2M1S", "abcde", 0, "2S2M1S", "2R2S1F", "abCDE");

            // SSMMSS
            //   CDEF
            // abcdef
            // abCDEF
            TestStitching("2M2S", "CDEF", "2S2M2S", "abcdef", 0, "2S2M2S", "2R2S2F", "abCDEF");

            // SSMMSS
            //   CDEF
            // abcde
            // abCDEF
            TestStitching("2M2S", "CDEF", "2S2M1S", "abcde", 0, "2S2M2S", "2R2S2F", "abCDEF");

            //   CDEF
            // abcd
            // abCDEF
            TestStitching("2M2S", "CDEF", "2S2M", "abcd", 0, "2S2M2S", "2R2S2F", "abCDEF");

            // SSMMSS
            //   CDE
            // abcdef
            // abCDEf
            TestStitching("2M1S", "CDE", "2S2M2S", "abcdef", 0, "2S2M2S", "2R2S1F1R", "abCDEf");

            // SSMMSS
            // ABCD
            // abcdef
            // abCDef
            TestStitching("2S2M", "ABCD", "2S2M2S", "abcdef", 0, "2S2M2S", "2R2S2R", "abCDef");

            // SSMMSS
            // ABCD
            //   cdef
            // ABCDef
            TestStitching("2S2M", "ABCD", "2M2S", "cdef", 0, "2S2M2S", "2F2S2R", "ABCDef");

            // SSMMSS
            // ABCD
            //  bcdef
            // AbCDef
            TestStitching("2S2M", "ABCD", "1S2M2S", "bcdef", 0, "2S2M2S", "1F1R2S2R", "AbCDef");


        }

        [Fact]
        public void GenerateConsensus_MatchSectionGap()
        {
            ///////////////////////////////////////
            // Cigars match in overlaps
            ///////////////////////////////////////

            // SSMMSS
            // ABCD
            //    def
            // ABCDef
            TestStitching("2S2M", "ABCD", "1M2S", "def", 1, "2S2M2S", "3F1S2R", "ABCDef", false);
            TestStitching("2S2M", "ABCD", "1M2S", "def", 1, "2S2M2S", "3F1S2R", "ABCDef", true);

            // SSMMSS
            // ABC
            //   cdef
            // ABCdef
            TestStitching("2S1M", "ABC", "2M2S", "cdef", 0, "2S2M2S", "2F1S3R", "ABCdef", false);
            TestStitching("2S1M", "ABC", "2M2S", "cdef", 0, "2S2M2S", "2F1S3R", "ABCdef", true);

            // SSMMSS
            // ABCDEF
            //    def
            // ABCDEF
            TestStitching("2S2M2S", "ABCDEF", "1M2S", "def", 1, "2S2M2S", "3F1S2F", "ABCDEF", false);
            TestStitching("2S2M2S", "ABCDEF", "1M2S", "def", 1, "2S2M2S", "3F1S2F", "ABCDEF", true);

            // SSMMSS
            //    DEF
            // abcdef
            // abcDEF
            TestStitching("1M2S", "DEF", "2S2M2S", "abcdef", -1, "2S2M2S", "3R1S2F", "abcDEF", false);
            TestStitching("1M2S", "DEF", "2S2M2S", "abcdef", -1, "2S2M2S", "3R1S2F", "abcDEF", true);

            // SSMMSS
            //    DEF
            // abcde
            // abcDEF
            TestStitching("1M2S", "DEF", "2S2M1S", "abcde", -1, "2S2M2S", "3R1S2F", "abcDEF", false);
            TestStitching("1M2S", "DEF", "2S2M1S", "abcde", -1, "2S2M2S", "3R1S2F", "abcDEF", true);

            // SSMMSS
            // ABCDE    
            //    def  
            // ABCDEf
            TestStitching("2S2M1S", "ABCDE", "1M2S", "def", 1, "2S2M2S", "3F1S1F1R", "ABCDEf", false);
            TestStitching("2S2M1S", "ABCDE", "1M2S", "def", 1, "2S2M2S", "3F1S1F1R", "ABCDEf", true);

            ///////////////////////////////////////
            // S overlapping M
            ///////////////////////////////////////

            // SSMM
            //  SMS
            // ABCD
            //  bcd
            TestStitching("2S2M", "ABCD", "1S1M1S", "bcd", 0, "2S2M", "1F1R1S1F", "AbCD", false);
            TestStitching("2S2M", "ABCD", "1S1M1S", "bcd", 0, "2S2M", "1F1R1S1F", "AbCD", true);
            TestStitching("2S2M", "ABCD", "1S1M1S", "bcd", 0, "2S2M", "1F3S", "ABCD", true, ignoreProbeSoftclips: false);

            // SSMM
            //   MSSS
            // ABCD
            //   cdef
            // FFSFRR - if allowing softclips to contribute and ignoring probe softclips, or not allowing softclips
            // FFSSRR - if allowing softclips to contribute and not ignoring probe softclips
            TestStitching("2S2M", "ABCD", "1M3S", "cdef", 0, "2S2M2S", "2F1S1F2R", "ABCDef", false);
            TestStitching("2S2M", "ABCD", "1M3S", "cdef", 0, "2S2M2S", "2F1S1F2R", "ABCDef", true);
            TestStitching("2S2M", "ABCD", "1M3S", "cdef", 0, "2S2M2S", "2F2S2R", "ABCDef", true, ignoreProbeSoftclips: false);

            //   MSSS
            // SSMM
            //   CDEF
            // abcd
            TestStitching("1M3S", "CDEF", "2S2M", "abcd", 0, "2S2M2S", "2R1S1R2F", "abCdEF", false);
            TestStitching("1M3S", "CDEF", "2S2M", "abcd", 0, "2S2M2S", "2R2S2F", "abCDEF", true);
            TestStitching("1M3S", "CDEF", "2S2M", "abcd", 0, "2S2M2S", "2R2S2F", "abCDEF", true, ignoreProbeSoftclips: false);

            // RSRSSFSF
            //  SSMMMSS
            // SSMMMSS
            //  BCDEFGH
            // abcdefg
            // abcDEFGH
            TestStitching("2S3M2S", "BCDEFGH", "2S3M2S", "abcdefg", -1, "2S4M2S", "1R1S1R2S1F1S1F", "aBcDEFGH", false, ignoreProbeSoftclips: false);
            TestStitching("2S3M2S", "BCDEFGH", "2S3M2S", "abcdefg", -1, "2S4M2S", "3R2S3F", "abcDEFGH", false, ignoreProbeSoftclips: true);
            TestStitching("2S3M2S", "BCDEFGH", "2S3M2S", "abcdefg", -1, "2S4M2S", "3R2S3F", "abcDEFGH", true);
            TestStitching("2S3M2S", "BCDEFGH", "2S3M2S", "abcdefg", -1, "2S4M2S", "1R6S1F", "aBCDEFGH", true, ignoreProbeSoftclips: false);

            // SSMMMSS
            //  SSMMMSS
            // ABCDEFG
            //  bcdefgh
            // AbCDEfGh
            // FRSSSSFR - if using softclips, and giving preference to forward read on softclip suffix overlap
            // FSSSSSSR - if using softclips and not ignoring probe clips
            // FRFSSRFR - if not using softclips, and giving preference to forward read on softclip suffix overlap
            // ABCDEFGh
            TestStitching("2S3M2S", "ABCDEFG", "2S3M2S", "bcdefgh", 1, "2S4M2S", "1F1R1F2S1R1F1R", "AbCDEfGh", false); // TODO why doesn't this work
            TestStitching("2S3M2S", "ABCDEFG", "2S3M2S", "bcdefgh", 1, "2S4M2S", "1F1R4S1F1R", "AbCDEFGh", true);
            TestStitching("2S3M2S", "ABCDEFG", "2S3M2S", "bcdefgh", 1, "2S4M2S", "1F6S1R", "ABCDEFGh", true, ignoreProbeSoftclips: false);
        }

        public void TestStitching(string read1Cigar, string read1Sequence, string read2Cigar, string read2Sequence, int read2PosMinusRead1Pos, string expectedCigar, string expectedDirections, string expectedStitchedSequence, bool useSoftclips = false, bool nifyDisagreements = false, bool ignoreProbeSoftclips = true)
        {
            var read1 = ReadTestHelper.CreateRead("chr1", read1Sequence, 1000, new CigarAlignment(read1Cigar));
            var read2 = ReadTestHelper.CreateRead("chr1", read2Sequence, 1000 + read2PosMinusRead1Pos, new CigarAlignment(read2Cigar));
            read1.BamAlignment.SetIsFirstMate(true);
            read2.BamAlignment.SetIsFirstMate(false);

            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            var stitcher = new BasicStitcher(10, useSoftclippedBases: useSoftclips, nifyDisagreements: nifyDisagreements, ignoreProbeSoftclips: ignoreProbeSoftclips);
            var alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            Assert.Equal(expectedStitchedSequence, alignmentSet.ReadsForProcessing.First().Sequence);
            Assert.Equal(expectedCigar.ToString(), alignmentSet.ReadsForProcessing.First().CigarData.ToString());
            Assert.Equal(expectedDirections.ToString(), alignmentSet.ReadsForProcessing.First().CigarDirections.ToString());

        }

        [Fact]
        public void SequenceInSoftclippedRegions()
        {
            // Aaron's original example - both R1 and R2 start at same position
            //var read1 = ReadTestHelper.CreateRead("chr1",
            //    "CAATGCAGCGAACAATGTTCTGGTGGTTGAATTTGCTGCAGAGCAGAGAGGGATGTAACCAAAATTAACTGAGCTGAGTCTGGGCAAATCTTAAACTGGGAGGAACAGGATACAAAGTTACATTTTCAGCAGCTACAATGTATAAAGGCAT",
            //    125, new CigarAlignment("25S119M7S"));
            //var read2 = ReadTestHelper.CreateRead("chr1",
            //    "CTGGTGGTTGAATTTGCTGCAGAGCAGAGAGGGATGTAACCAAAATTAACTGAGCTGAGTCTGGGCAAATCTTAAACTGGGAGGAACAGGATACAAAGTTACATTTTCAGCAGCTACAATGTATAAAGGCATTCTGTAAACATGGGCAGCA",
            //    125, new CigarAlignment("6S119M26S"));

            var read1 = ReadTestHelper.CreateRead("chr1",
                "FFFFFFFFFFFFFFFFFFFYYYYYYGTTGAATTTGCTGCAGAGCAGAGAGGGATGTAACCAAAATTAACTGAGCTGAGTCTGGGCAAATCTTAAACTGGGAGGAACAGGATACAAAGTTACATTTTCAGCAGCTACAATGTATAFFFFFFF",
                125, new CigarAlignment("25S119M7S"));
            var read2 = ReadTestHelper.CreateRead("chr1",
                "RRRRRRGTTGAATTTGCTGCAGAGCAGAGAGGGATGTAACCAAAATTAACTGAGCTGAGTCTGGGCAAATCTTAAACTGGGAGGAACAGGATACAAAGTTACATTTTCAGCAGCTACAATGTATAXXXXXXXRRRRRRRRRRRRRRRRRRR",
                125, new CigarAlignment("6S119M26S"));

            //XXXXXXXRRRRRRRRRRRRRRRRRRR
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            var expectedStitched =
                "FFFFFFFFFFFFFFFFFFFRRRRRRGTTGAATTTGCTGCAGAGCAGAGAGGGATGTAACCAAAATTAACTGAGCTGAGTCTGGGCAAATCTTAAACTGGGAGGAACAGGATACAAAGTTACATTTTCAGCAGCTACAATGTATAFFFFFFFRRRRRRRRRRRRRRRRRRR";

            var alignmentSet = new AlignmentSet(read1, read2);
            
            //Stitched Cigar: 25S119M26S
            //    Stitched Directions: 19F6R119S7F19R
            //    ignored prefix: 6
            //ignored suffix: 7
            //cigar1 prefix clip: 25
            //cigar2 prefix clip: 6
            //cigar 1 suffix clip: 7
            //cigar 2 suffix clip: 26
            //    is outie: False

            var stitchingInfo = new StitchingInfo()
            {
                StitchedCigar = new CigarAlignment("25S119M26S"),
                StitchedDirections = new CigarDirection("19F6R119S7F19R"),
                IgnoredProbePrefixBases = 6,
                IgnoredProbeSuffixBases = 7
            };

            TestStitching(read1.CigarData.ToString(), read1.Sequence, read2.CigarData.ToString(), read2.Sequence, 0, stitchingInfo.StitchedCigar.ToString(), stitchingInfo.StitchedDirections.ToString(),
                expectedStitched, true);


            // Aaron's second example, R1 starts before R2 but also has overlapping softclips
            // Gap is 4
            read1 = ReadTestHelper.CreateRead("chr1",
                "CAATGCAGCGAACAATGTTCTGGTGGTTGAATTTGCTGCAGAGCAGAGAGGGATGTAACCAAAATTAACTGAACTGAGTCTGGGCAAATCTTAAACTGGGAGGAACAGGATACAAAGTTACATTTTCAGCAGCTACAATGTATAAAGGCA",
                125, new CigarAlignment("25S119M6S"));
            read2 = ReadTestHelper.CreateRead("chr1",
                "NNNTNNTNNAATTTGCTGCAGAGCAGAGAGGGATGTAACCAAAATTAACTGAACTGAGTCTGGGCAAATCTTAAACTGGGAGGAACAGGATACAAAGTTACATTTTCAGCAGCTACAATGTATAAAGGCATTCTGTAAACATGGGCAGCA",
                129, new CigarAlignment("9S115M26S"));

            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);
            expectedStitched =
                "CAATGCAGCGAACAATGTTCNNNTNGTTGAATTTGCTGCAGAGCAGAGAGGGATGTAACCAAAATTAACTGAACTGAGTCTGGGCAAATCTTAAACTGGGAGGAACAGGATACAAAGTTACATTTTCAGCAGCTACAATGTATAAAGGCATTCTGTAAACATGGGCAGCA";

            stitchingInfo = new StitchingInfo()
            {
                StitchedCigar = new CigarAlignment("25S119M26S"),
                StitchedDirections = new CigarDirection("20F5R119S6F20R"),
            };

            TestStitching(read1.CigarData.ToString(), read1.Sequence, read2.CigarData.ToString(), read2.Sequence, 4, stitchingInfo.StitchedCigar.ToString(), stitchingInfo.StitchedDirections.ToString(),
    expectedStitched, true);


            //      7890123456
            // SSSSSMMMMMMMMMMMMSSSSSS
            // GCTTGZTTGAATTTATAAAGGCA
            //   NTNOTNNAATTTATAAAGGCATT
            //   SSSSSSSMMMMMMMMSSSSSSSS


            read1 = ReadTestHelper.CreateRead("chr1", "GCTTGZTTGAATTTATAAAGGCA", 27, new CigarAlignment("5S12M6S"));
            read2 = ReadTestHelper.CreateRead("chr1", "NTNOTNNAATTTATAAAGGCATT", 31, new CigarAlignment("7S8M8S"));
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);
            expectedStitched =
                "GCNTNZTTGAATTTATAAAGGCATT";
            stitchingInfo = new StitchingInfo()
            {
                StitchedCigar = new CigarAlignment("5S12M8S"),
                StitchedDirections = new CigarDirection("2F3R12S6F2R"),
            };

            TestStitching(read1.CigarData.ToString(), read1.Sequence, read2.CigarData.ToString(), read2.Sequence, 4, stitchingInfo.StitchedCigar.ToString(), stitchingInfo.StitchedDirections.ToString(),
    expectedStitched, true);


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
            var r1 = "BCDEFGHIJKLM";
            var r2 = "abcdefxxxxxghij";
            var r1cigar = "5S5M2S";
            var r2cigar = "3S3M5I4M";

            r1 = "GAAAGCATTCAGCACCTTCAGCTTATAGCAGAAAAACACAAGGGAGAGGGTAAGTTAGTAAATGATCTTCTTTTTTTCTGTATTAATAAAAGTAATTTGCA";
            r2 = "ACATTGGAAAGCATTCAGCACCTTCAGCTTATAGCAGAAAAACACAAGGGAGAGGGTAAGTTAGTAAATGATCTTCTTTTTTTCTGTATTAATAAAAGTNN";
            r2cigar = "3S3M5I88M2S";
            r1cigar = "5S96M";

            var r1read = ReadTestHelper.CreateRead("chr1", r1, 10, new CigarAlignment(r1cigar));
            var r2read = ReadTestHelper.CreateRead("chr1", r2, 7, new CigarAlignment(r2cigar));
            StitcherTestHelpers.SetReadDirections(r2read, DirectionType.Reverse);
            var stitcher = new BasicStitcher(10, useSoftclippedBases: false);
            var alignmentSet = new AlignmentSet(r1read, r2read);
            stitcher.TryStitch(alignmentSet);



            // Real example from Kristina's problematic variant #73
            var read1Bases =
                "GAAGCCACACTGACGTGCCTCTCCCTCCCTCCAGGAAGCCTTCCAGGAAGCCTACGTGATGGCCAGCGTGGACAACCCCCACGTGTGCCGCCTGCTGGGCATCTGCCTCACCTCCACCGTGCAGCTCATCACGCAGCTCATGCCCTTCGG";
            var read2Bases =
                "AGGAAGCCTTCCAGGAAGCCTACGTGATGGCCAGCGTGGACAACCCCCACGTGTGCCGCCTGCTGGGCATCTGCCTCACCTCCACCGTGCAGCTCATCACGCAGCTCATGCCCTTCGGCTGCCTCCTGGACTATGTCCGGGAACACAAAG";

            var read1 = ReadTestHelper.CreateRead("chr7", read1Bases, 55248972, new CigarAlignment("20S9M12I109M"));
            var read2 = ReadTestHelper.CreateRead("chr7", read2Bases, 55248981, new CigarAlignment("9S120M21S"));
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            stitcher = new BasicStitcher(10, useSoftclippedBases: false);
            alignmentSet = new AlignmentSet(read1, read2);
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

            // PICS-721 
            read1Bases =
                "CTCCTGCTGCTGGCCGGGCTGTATCGAGGGCAGGCGCTCCACGGCCGGCACCCCCCCCCCCCCCCCCCCCGGGACGACCGGGGCCCCCGGCCCCCGGGCCC";
            read2Bases =
                "CAGAAGCTCTCCCGCTTCCCTCTGGCCCGACAGGTACTGGGCGCATCCCCCACCTCACATGTGACAGCCTGACTCCAGCAGGCAGAACCAAGTCTCCCACT";
            read1 = ReadTestHelper.CreateRead("chr1", read1Bases, 176520228, new CigarAlignment("55M46S"));
            read2 = ReadTestHelper.CreateRead("chr1", read2Bases, 176520300, new CigarAlignment("101M"));
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            // Not using softclips -- these don't even overlap. Big gap in between. Shouldn't stitch.
            stitcher = new BasicStitcher(10, useSoftclippedBases: false);
            alignmentSet = new AlignmentSet(read1, read2);
            Assert.False(stitcher.TryStitch(alignmentSet));

            // Using softclips -- these would overlap if we extended the softclip all the way and used that to stitch, but it shouldn't.
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
        public void TryStitch_NoOverlap()
        {
            // Not kissing and no overlap, or overlap is only softclip + M
            // MSS
            //   MMMS
            TestMerge(1, "1M2S", 3, "3M1S", shouldMerge:false);
    
            // MSS
            //    MMMS
            TestMerge(1, "1M2S", 4, "3M1S", shouldMerge: false);
    
            // MSSS
            //   SMMMS
            TestMerge(1, "1M3S", 3, "1S3M1S", shouldMerge: false);
    
            // For now, we're allowing kissing reads to merge and softclips to contriubte (see PiscesUnitTestScenarios_SCProbeInsertions_SCProbeInsertion Inputs_SoftclippedInsertion-9), but this needs discussion
            // In the future, we might want to change this behavior and enable this test:
            //// MSS
            ////  MMMS
            //TestMerge(1, "1M2S", 2, "3M1S", shouldMerge: false);
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

            //PiscesUnitTestScenarios_SoftClippedDeletions_SoftclippedDeletion Inputs_SoftclippedDeletion-1
            TestMerge(2, "1M2D5M", 5, "2S4M", 2, "1S1M2D5M", "1R7S1F", r1Bases: "ABCDEF", r2Bases: "123456");

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

        [Fact]
        public void TryStitch_ReadsWithLongDeletions()
        {
            // PICS-574
            // NS500747:267:H5G27BGXY:2:11205:9740:16485 147 chr7 148506283 60 20S7M5803D2I72M 
            // = 148512084 5881 NNNNNNNNNNNNNNNNNNNNCACAGACCTCACAGGGTTGATAGTTGTAAACATGGTTAGAGGAGCCGTCTGAGTAAAGATAACATCATGCAGGCCAATGAA !!!!!!!!!!!!!!!!!!!!EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEAAAAA SM:i:97 AS:i:272 RG:Z:1 NM:i:5808 BC:Z:none OC:Z:20S81M
            // NS500747: 267:H5G27BGXY: 2:11205:9740:16485 99 chr7 148512084 60 9M2I71M19S 
            // = 148506283 - 5882 CTCACAGACCTCACAGGGTTGATAGTTGTAAACATGGTTAGAGGAGCCGTCTGAGTAAAGATAACATCATGCAGGCCAATGANNNNNNNNNNNNNNNNNNN AAAAAEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE!!!!!!!!!!!!!!!!!!!SM:i: 76 AS: i: 272 RG: Z: 1 NM: i: 8 BC: Z: none OC:Z: 82M19S

            // Should not throw exception if we want to just ignore it.
            TestMerge(148506283, "20S7M5803D2I72M", 148512084, "9M2I71M19S", shouldMerge: false, isAboveMaxLength: true, ignoreReadsAboveMaxLength: true);
            // Should throw exception unless we specify to ignore reads above max length
            TestMerge(148506283, "20S7M5803D2I72M", 148512084, "9M2I71M19S", shouldMerge: false, isAboveMaxLength: true, ignoreReadsAboveMaxLength: false);
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
            // Try both scenarios: throw exception
            Assert.Throws<Exception>(() => TestMerge(1, maxExpectedReadLength + 1 + "M", maxExpectedReadLength,
                maxExpectedReadLength + 1 + "M",
                1, null, null,
                maxReadLength: maxExpectedReadLength, ignoreReadsAboveMaxLength: false, isAboveMaxLength: true));
            // ...or don't throw exception
            TestMerge(1, maxExpectedReadLength + 1 + "M", maxExpectedReadLength,
                    maxExpectedReadLength + 1 + "M",
                    1, null, null,
                    maxReadLength: maxExpectedReadLength, shouldMerge: false, ignoreReadsAboveMaxLength: true, isAboveMaxLength: true);

            // No overlap -- shouldn't merge anyway
            TestMerge(1, maxExpectedReadLength + "M", maxExpectedReadLength + 1, maxExpectedReadLength + "M",
                1, null, null, shouldMerge: false,
                maxReadLength: maxExpectedReadLength);

            // Do not pass maxReadLength, use default
            if (greaterThanDefault)
            {
                // Try both scenarios: throw exception
                Assert.Throws<Exception>(
                    () => TestMerge(1, maxExpectedReadLength + "M", maxExpectedReadLength,
                        maxExpectedReadLength + "M",
                        1, (2 * maxExpectedReadLength - 1) + "M",
                        (maxExpectedReadLength - 1) + "F" + "1S" + (maxExpectedReadLength - 1) + "R", ignoreReadsAboveMaxLength: false, isAboveMaxLength: true));

                // ...or don't throw exception
                TestMerge(1, maxExpectedReadLength + "M", maxExpectedReadLength,
                        maxExpectedReadLength + "M",
                        1, (2 * maxExpectedReadLength - 1) + "M",
                        (maxExpectedReadLength - 1) + "F" + "1S" + (maxExpectedReadLength - 1) + "R",
                        shouldMerge: false, ignoreReadsAboveMaxLength: true, isAboveMaxLength: true);
            }
            else
            {
                TestMerge(1, maxExpectedReadLength + "M", maxExpectedReadLength, maxExpectedReadLength + "M",
                    1, (2 * maxExpectedReadLength - 1) + "M",
                    (maxExpectedReadLength - 1) + "F" + "1S" + (maxExpectedReadLength - 1) + "R");
            }

        }

        private void TestMerge(int pos1, string cigar1, int pos2, string cigar2, int posStitch = 0, string cigarStitch = "", string stitchDirections = "", bool shouldMerge = true, bool ignoreProbeSoftclips = true, int? maxReadLength = null, bool ignoreReadsAboveMaxLength = false, bool isAboveMaxLength = false, string r1Bases= null, string r2Bases=null)
        {
            r1Bases = r1Bases ?? new string('A', (int)new CigarAlignment(cigar1).GetReadSpan());
            r2Bases = r2Bases ?? new string('A', (int)new CigarAlignment(cigar2).GetReadSpan());
            var read1 = ReadTestHelper.CreateRead("chr1", r1Bases, pos1,
             new CigarAlignment(cigar1));

            var read2 = ReadTestHelper.CreateRead("chr1", r2Bases, pos2,
                new CigarAlignment(cigar2));
            StitcherTestHelpers.SetReadDirections(read2, DirectionType.Reverse);

            BasicStitcher stitcher;

            if (maxReadLength != null)
            {
                stitcher = new BasicStitcher(10, ignoreProbeSoftclips: ignoreProbeSoftclips,
                    maxReadLength: maxReadLength.Value, ignoreReadsAboveMaxLength: ignoreReadsAboveMaxLength);
            }
            else
            {
                // Use the default
                stitcher = new BasicStitcher(10, ignoreProbeSoftclips: ignoreProbeSoftclips, ignoreReadsAboveMaxLength: ignoreReadsAboveMaxLength);
            }

            if (!shouldMerge)
            {
                var alignmentSet = new AlignmentSet(read1, read2);
                    if ( isAboveMaxLength && !ignoreReadsAboveMaxLength)
                    {
                        Assert.Throws<Exception>(() => stitcher.TryStitch(alignmentSet));
                    }
                    else
                    {
                        Assert.False(stitcher.TryStitch(alignmentSet));
                    }

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
            Assert.Throws<Exception>(() => stitcher.TryStitch(alignmentSet));

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
            //If "read1" (orientation-based) is not the true read1 off the sequencer, should take base/q from true read1
            read1.BamAlignment.Qualities = new byte[] { 30, 30, 30, 5, 5, 5, 5, 5 };
            read1.BamAlignment.SetIsFirstMate(false);
            read2.BamAlignment.Qualities = new byte[] { 5, 5, 5, 5, 5, 20, 19, 18 };
            read2.BamAlignment.SetIsFirstMate(true);
            alignmentSet = new AlignmentSet(read1, read2);
            stitcher.TryStitch(alignmentSet);
            mergedRead = StitcherTestHelpers.GetMergedRead(alignmentSet);
            Assert.Equal(nifyDisagreements ? "NNNNN" : read2.Sequence.Substring(3, 5), mergedRead.Sequence.Substring(overlapStart, 5));
            Assert.Equal(nifyDisagreements ? "TTTNNNNNAAA" : "TTTAAAAAAAA", mergedRead.Sequence);
            StitcherTestHelpers.CompareQuality(nifyDisagreements ? new byte[] { 30, 30, 30, 0, 0, 0, 0, 0, 20, 19, 18 } : new byte[] { 30, 30, 30, 5, 5, 5, 5, 5, 20, 19, 18 }, mergedRead.Qualities);

        }


	}
}