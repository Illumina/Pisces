using System;
using Alignment.Domain.Sequencing;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.Models;
using Gemini.Types;
using Gemini.Utility;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using ReadRealignmentLogic.Models;
using Xunit;

namespace Gemini.Tests
{
    public class HelperTests
    {
        [Fact]
        public void GetMdCountsWithSubstitutions()
        {
            var mdString = "2A5T2A4A12T5T57";
            var readSeq =
                "GCTGGGGTGGGCGGGGCGGGAGCCGGCCCNCAGCGGCGGGAGGGGTCCCCGCGGGGACACACACAAACCCAGGCTTTAGCCCAGGGGCTGGGG";
            MdCounts mdCounts;
            mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, readSeq, 0, 0);
            Assert.Equal(2, mdCounts.SubC);

            readSeq = "GCGGCCCCGGXXGGXTCCAGCCGCXCCAGGTCCATGATGTACTTGGCCATGAGCGAGTGCCGGTCTGCCNGGCAGGCGGCCACGCGGCGCAGG";
            readSeq = "GCGGCCCCGGGGGGGTCCAGCCGCGCCAGGTCCATGATGTACTTGGCCATGAGCGAGTGCCGGTCTGCCNGGCAGGCGGCCACGCGGCGCAGG";
            mdString = "10C0T2A9T44T23";
            mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, readSeq, 0);
            Assert.Equal(0, mdCounts.SubT);
            Assert.Equal(0, mdCounts.SubA);
            Assert.Equal(0, mdCounts.SubC);
            Assert.Equal(4, mdCounts.SubG);
            Assert.Equal(1, mdCounts.SubN);

            mdString = "6A7C10A8A41";
            readSeq = "CNGGGCGGGCTGGCTGGGGGGTTGGCAGGCTTTGTAGCTGCTGGGGTTGGTGGGGAGGGAGCCGGCCCTCAGCGTCGGGAGGGGTCCCCGCG";
            // Cigar 16S76M
            mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, readSeq, 16);
            Assert.Equal(4, mdCounts.SubT);
            Assert.Equal(0, mdCounts.SubA);
            Assert.Equal(0, mdCounts.SubC);
            Assert.Equal(0, mdCounts.SubG);
            Assert.Equal(0, mdCounts.SubN);

            mdString = "0T0C7C0C3T2A0T3G51";
            // T - xxx
            // C - xxx
            // A - x
            // G - x
            // Runs: 
            //  T0C
            //  C0C
            //  A0T
            mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, new string('A', 74), 0);
            Assert.Equal(1, mdCounts.A);
            Assert.Equal(1, mdCounts.G);
            Assert.Equal(3, mdCounts.T);
            Assert.Equal(3, mdCounts.C);
            Assert.Equal(2, mdCounts.RunLength);
            Assert.Equal(6, mdCounts.NumInRuns);
            Assert.Equal(8, mdCounts.SubA);


            mdString = "10T0C0A50";
            mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, new string('A', 63), 0);
            Assert.Equal(1, mdCounts.A);
            Assert.Equal(0, mdCounts.G);
            Assert.Equal(1, mdCounts.T);
            Assert.Equal(1, mdCounts.C);
            Assert.Equal(3, mdCounts.RunLength);
            Assert.Equal(3, mdCounts.NumInRuns);
            Assert.Equal(3, mdCounts.SubA);

            // Indel-containing reads
            // Real examples from amplicon test data

            // Insertion - read length is 115 so MD string of 114 should tell us this has an insertion
            // NA12877-100ng-E08A-H06
            // NB551015:245:HNWHKBGX3:3:23401:16183:12924
            // 111M1I3M
            readSeq =
                "GGACACAGGGAGGGGAACATCACACACTGGGGCCTGTCGGGGGATGGGGTGATAGGGGAAGGATAGCATTAGGAGAAATACCTAATGTAGATGACAGGTTGATGGGTGCAGACAA";
            mdString = "114";
            Assert.Throws<ArgumentException>(()=>mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, readSeq, 0));

            // Softclipped out deletion - beginning of read
            // NB551015:245:HNWHKBGX3:3:21610:2569:13429
            // 3S145M
            mdString = "0^G114G14A15";
            Assert.Throws<ArgumentException>(() => mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, new string('A', 148), 3));

            // Softclipped out deletion - end of read
            // 144^CAG0
            // NB551015: 245:HNWHKBGX3: 3:13510:16153:17865
            // 144M7S
            mdString = "144^CAG0";
            Assert.Throws<ArgumentException>(() => mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, new string('A', 151), 0, 7));

            // Run-of-the-mill softclips - don't penalize
            // 76M6S
            // 76
            readSeq = "GCCAAGGCAGGTGGACCATGAGGTCAGGAGATTGAGACCATCCTGGCTAACATGGTGAAACCCTGTATCTACTAAACATTGA";
            mdString = "76";
            mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, readSeq, 0, 6);
            Assert.Equal(0, mdCounts.A);
            Assert.Equal(0, mdCounts.T);
            Assert.Equal(0, mdCounts.C);
            Assert.Equal(0, mdCounts.G);
            Assert.Equal(0, mdCounts.RunLength);
            Assert.Equal(0, mdCounts.NumInRuns);
            Assert.Equal(0, mdCounts.SubA);
            Assert.Equal(0, mdCounts.SubT);
            Assert.Equal(0, mdCounts.SubC);
            Assert.Equal(0, mdCounts.SubG);

            // Run-of-the-mill softclips - don't penalize
            // 7S75M
            // 75
            readSeq = "GAAGTGTGGCCAAGGCAGGTGGACCATGAGGTCAGGAGATTGAGACCATCCTGGCTAACATGGTGAAACCCTGTATCTACTA";
            mdString = "75";
            mdCounts = Helper.GetMdCountsWithSubstitutions(mdString, readSeq, 7, 0);
            Assert.Equal(0, mdCounts.A);
            Assert.Equal(0, mdCounts.T);
            Assert.Equal(0, mdCounts.C);
            Assert.Equal(0, mdCounts.G);
            Assert.Equal(0, mdCounts.RunLength);
            Assert.Equal(0, mdCounts.NumInRuns);
            Assert.Equal(0, mdCounts.SubA);
            Assert.Equal(0, mdCounts.SubT);
            Assert.Equal(0, mdCounts.SubC);
            Assert.Equal(0, mdCounts.SubG);

        }

        [Fact]
        public void GetMdCounts()
        {
            var mdString = "0T0C7C0C3T2A0T3G51";
            // T - xxx
            // C - xxx
            // A - x
            // G - x
            var mdCounts = Helper.GetMdCounts(mdString);
            Assert.Equal(1, mdCounts.A);
            Assert.Equal(1, mdCounts.G);
            Assert.Equal(3, mdCounts.T);
            Assert.Equal(3, mdCounts.C);
            Assert.Equal(2, mdCounts.RunLength);
            Assert.Equal(6, mdCounts.NumInRuns);
        }
        [Fact]
        public void SoftclipCigar()
        {
            var result = Helper.SoftclipCigar(new CigarAlignment("4M1I"),
                new[] {MatchType.Mismatch, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match}, 1, 0,
                true, false, 0, 0, false, true);
            Assert.Equal("1S3M1I", result.ToString());

            result = Helper.SoftclipCigar(new CigarAlignment("4M1I"),
                new[] { MatchType.Mismatch, MatchType.Mismatch, MatchType.Match, MatchType.Match, MatchType.Match }, 1, 0,
                true, false, 0, 0, false, true);
            Assert.Equal("1S3M1I", result.ToString());

            result = Helper.SoftclipCigar(new CigarAlignment("4M1I"),
                new[] { MatchType.Match, MatchType.Mismatch, MatchType.Match, MatchType.Match, MatchType.Match }, 1, 0,
                true, false, 0, 0, false, true);
            Assert.Equal("4M1I", result.ToString());

            ///// Originally 2 softclips
            // Innermost is mismatch, need to softclip from there on out
            result = Helper.SoftclipCigar(new CigarAlignment("4M1I"),
                new[] { MatchType.Match, MatchType.Mismatch, MatchType.Match, MatchType.Match, MatchType.Match }, 2, 0,
                true, false, 0, 0, false, true);
            Assert.Equal("2S2M1I", result.ToString());

            // Both are mismatch, need to softclip from there on out
            result = Helper.SoftclipCigar(new CigarAlignment("4M1I"),
                new[] { MatchType.Mismatch, MatchType.Mismatch, MatchType.Match, MatchType.Match, MatchType.Match }, 2, 0,
                true, false, 0, 0, false, true);
            Assert.Equal("2S2M1I", result.ToString());

            result = Helper.SoftclipCigar(new CigarAlignment("10M1I"),
                new[] { MatchType.NMismatch, MatchType.NMismatch, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Unmapped }, 
                5, 0,
                true, false, 5, 0, false, true);
            Assert.Equal("5S5M1I", result.ToString());

            // Even if it's already partially softclipped (eg Ns), properly re-softclip 
            result = Helper.SoftclipCigar(new CigarAlignment("3S7M1I"),
                new[] { MatchType.NMismatch, MatchType.NMismatch, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Unmapped },
                5, 0,
                true, false, 5, 0, false, true);
            Assert.Equal("5S5M1I", result.ToString());

            result = Helper.SoftclipCigar(new CigarAlignment("5S5M1I"),
                new[] { MatchType.NMismatch, MatchType.NMismatch, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Unmapped },
                5, 0,
                true, false, 5, 0, false, true);
            Assert.Equal("5S5M1I", result.ToString());

            //// Super long original softclip - can it be rescued?
            // Single mismatch in softclip - rescue up to mismatch
            result = Helper.SoftclipCigar(new CigarAlignment("15M1I"),
                new[]
                {
                    MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match,
                    MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match,
                    MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match,
                    MatchType.Unmapped
                }, 12, 0,
                true, false, 0, 0, false, true);
            Assert.Equal("15M1I", result.ToString());


        
            // Resoftclip the whole thing because it's deemed too messy.
            result = Helper.SoftclipCigar(new CigarAlignment("15M1I"),
                new[]
                {
                    MatchType.Match, MatchType.Match, MatchType.Mismatch, MatchType.Match, MatchType.Match,
                    MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match,
                    MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match,
                    MatchType.Unmapped
                }, 11, 0,
                true, false, 0, 0, false, true, 12);
            Assert.Equal("11S4M1I", result.ToString());

            // Ratio of mismatches is low enough to only softclip from the mismatch to the end
            result = Helper.SoftclipCigar(new CigarAlignment("15M1I"),
                new[]
                {
                    MatchType.Match, MatchType.Match, MatchType.Mismatch, MatchType.Match, MatchType.Match,
                    MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match,
                    MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match,
                    MatchType.Unmapped
                }, 12, 0,
                true, false, 0, 0, false, true, 12);
            Assert.Equal("3S12M1I", result.ToString());

        }

        [Fact]
        public void ConstructCigar()
        {
            var positionMap = new int[] {1, 2, 3, 4, 5};
            var cigar = Helper.ConstructCigar(positionMap);
            Assert.Equal("5M", cigar.ToString());
            cigar = Helper.ConstructCigar(positionMap, true);
            Assert.Equal("5M", cigar.ToString());

            positionMap = new int[] {1, 2, 4, 5, 6};
            cigar = Helper.ConstructCigar(positionMap);
            Assert.Equal("2M1D3M", cigar.ToString());
            cigar = Helper.ConstructCigar(positionMap, true);
            Assert.Equal("2M1D3M", cigar.ToString());

            positionMap = new int[] {1, 2, -1, 3, 4, 5};
            cigar = Helper.ConstructCigar(positionMap);
            Assert.Equal("2M1I3M", cigar.ToString());
            cigar = Helper.ConstructCigar(positionMap, true);
            Assert.Equal("2M1I3M", cigar.ToString());

            positionMap = new int[] { -1, 2, -1, 3, 4, 5 };
            cigar = Helper.ConstructCigar(positionMap);
            Assert.Equal("1I1M1I3M", cigar.ToString());
            cigar = Helper.ConstructCigar(positionMap, true);
            Assert.Equal("1S1M1I3M", cigar.ToString());

            positionMap = new int[] { 1, 2, -1, 3, 4, -1};
            cigar = Helper.ConstructCigar(positionMap);
            Assert.Equal("2M1I2M1I", cigar.ToString());
            cigar = Helper.ConstructCigar(positionMap, true);
            Assert.Equal("2M1I2M1S", cigar.ToString());

            positionMap = new int[] { -1, -1, -1 };
            cigar = Helper.ConstructCigar(positionMap);
            Assert.Equal("3I", cigar.ToString());
            cigar = Helper.ConstructCigar(positionMap, true);
            Assert.Equal("3S", cigar.ToString());

            positionMap = new int[] { 1, 5 };
            cigar = Helper.ConstructCigar(positionMap);
            Assert.Equal("1M3D1M", cigar.ToString());
            cigar = Helper.ConstructCigar(positionMap, true);
            Assert.Equal("1M3D1M", cigar.ToString());

            positionMap = new int[] { 1, 5, -1 };
            cigar = Helper.ConstructCigar(positionMap);
            Assert.Equal("1M3D1M1I", cigar.ToString());
            cigar = Helper.ConstructCigar(positionMap, true);
            Assert.Equal("1M3D1M1S", cigar.ToString());
        }

        [Fact]
        public void GetMismatchMap()
        {
            var mismatchMap = Helper.GetMismatchMap("ATCGA", new PositionMap(new int[] {1, 2, 3, 4,5}), "ATCGATT", 0);
            Assert.Equal(new[]{MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match, MatchType.Match}, mismatchMap);

            mismatchMap = Helper.GetMismatchMap("ATCGA", new PositionMap(new int[] { -1, -1, 3, 4, 5 }), "ATCGATT", 0);
            Assert.Equal(new[] { MatchType.Unmapped, MatchType.Unmapped, MatchType.Match, MatchType.Match, MatchType.Match }, mismatchMap);

            mismatchMap = Helper.GetMismatchMap("ANCGA", new PositionMap(new int[] { -1, -1, 3, 4, 5 }), "ATCGATT", 0);
            Assert.Equal(new[] { MatchType.Unmapped, MatchType.NMismatch, MatchType.Match, MatchType.Match, MatchType.Match }, mismatchMap);

            mismatchMap = Helper.GetMismatchMap("ANCNN", new PositionMap(new int[] { -1, -1, 3, 4, 5 }), "ATCGATT", 0);
            Assert.Equal(new[] { MatchType.Unmapped, MatchType.NMismatch, MatchType.Match, MatchType.NMismatch, MatchType.NMismatch}, mismatchMap);

        }

        [Fact]
        public void DeletionHasSketchyAnchor()
        {
            var rptADeletion = new HashableIndel()
            {
                AlternateAllele = "T",
                ReferenceAllele = "TAAAA",
                IsRepeat = true,
                RepeatUnit = "A"
            };

            var rptDinucDeletion = new HashableIndel()
            {
                AlternateAllele = "T",
                ReferenceAllele = "TACAC",
                IsRepeat = true,
                RepeatUnit = "AC"
            };

            Assert.True(Helper.DeletionHasSketchyAnchor("ACCCCC", rptADeletion, 0));
            Assert.True(Helper.DeletionHasSketchyAnchor("AACCCCC", rptADeletion, 0));
            Assert.True(Helper.DeletionHasSketchyAnchor("AAAAACCCCC", rptADeletion, 0));
            Assert.True(Helper.DeletionHasSketchyAnchor("AAACCCCC", rptADeletion, 1));
            Assert.True(Helper.DeletionHasSketchyAnchor("CAAAA", rptADeletion, 0));
            Assert.True(Helper.DeletionHasSketchyAnchor("CCCAAAA", rptADeletion, 2));
            //Assert.False(Helper.DeletionHasSketchyAnchor("CCCAAAA", rptADeletion, 0));
            Assert.False(Helper.DeletionHasSketchyAnchor("CCCAAAA", rptADeletion, 1));
            Assert.False(Helper.DeletionHasSketchyAnchor("CTTTT", rptADeletion, 0));
            Assert.False(Helper.DeletionHasSketchyAnchor("TAAAAT", rptADeletion, 0));

            Assert.True(Helper.DeletionHasSketchyAnchor("CCCCTA", rptADeletion, 4));
            Assert.False(Helper.DeletionHasSketchyAnchor("CCCCTAT", rptADeletion, 4));
            Assert.False(Helper.DeletionHasSketchyAnchor("TCCCCC", rptADeletion, 0));
            Assert.True(Helper.DeletionHasSketchyAnchor("TTTACACACAC", rptDinucDeletion, 2));
            Assert.False(Helper.DeletionHasSketchyAnchor("TTTACACACACT", rptDinucDeletion, 2));
            Assert.True(Helper.DeletionHasSketchyAnchor("TTTACACACA", rptDinucDeletion, 2));
            Assert.True(Helper.DeletionHasSketchyAnchor("ACACACAC", rptDinucDeletion, 1));


            var rptTDeletion = new HashableIndel()
            {
                AlternateAllele = "T",
                ReferenceAllele = "TTTTA",
                IsRepeat = true,
                RepeatUnit = "T"
            };
            var rptTriDeletion = new HashableIndel()
            {
                AlternateAllele = "T",
                ReferenceAllele = "TTCATCA",
                IsRepeat = true,
                RepeatUnit = "TCA"
            };



            Assert.True(Helper.DeletionHasSketchyAnchor("TTTGCTATCAATCACAGGTATACAAGTACTTGCCTTTACTCCTGCATGTAGAAGACTCTTATGAGCGAGATAATGCAGAGAAGGCCTTTCATATAAATT", rptTDeletion, 2));


            Assert.True(Helper.DeletionHasSketchyAnchor("CCATTCTGATTTGACTTTTGTGCATCTTTGGCTCGAGTATCTCATATAGATTACTCGTGCTTTTCTTCAGCTTCCTCATCATCAAAATCTTTATCATTTT", rptTriDeletion, 98));
            Assert.False(Helper.DeletionHasSketchyAnchor("CCATTCTGATTTGACTTTTGTGCATCTTTGGCTCGAGTATCTCATATAGATTACTCGTGCTTTTCTTCAGCTTCCTCATCATCAAAATCTTTATCATTTT", rptTriDeletion, 97));
            Assert.False(Helper.DeletionHasSketchyAnchor("CCATTCTGATTTGACTTTTGTGCATCTTTGGCTCGAGTATCTCATATAGATTACTCGTGCTTTTCTTCAGCTTCCTCATCATCAAAATCTTTATCATTTT", rptTriDeletion, 96));
            Assert.False(Helper.DeletionHasSketchyAnchor("CCATTCTGATTTGACTTTTGTGCATCTTTGGCTCGAGTATCTCATATAGATTACTCGTGCTTTTCTTCAGCTTCCTCATCATCAAAATCTTTATCATTTT", rptTriDeletion, 99));


            var rptLongDeletion = new HashableIndel()
            {
                AlternateAllele = "T",
                ReferenceAllele = "TTCAGTCG",
                IsRepeat = true,
                RepeatUnit = "TCAGTCG"
            };
            Assert.False(Helper.DeletionHasSketchyAnchor("CTTTTATTA", rptLongDeletion, 1));
            Assert.False(Helper.DeletionHasSketchyAnchor("CTTTTATTA", rptLongDeletion, 0));
            Assert.False(Helper.DeletionHasSketchyAnchor("GTA", rptLongDeletion, 1));
            Assert.False(Helper.DeletionHasSketchyAnchor("GTA", rptLongDeletion, 0));
            Assert.False(Helper.DeletionHasSketchyAnchor("GTA", rptLongDeletion, 2));

        }

        [Fact]
        public void RepeatDeletionFlankedByRepeats()
        {
            var rptADeletion = new HashableIndel()
            {
                AlternateAllele = "T",
                ReferenceAllele = "TAAAA",
                IsRepeat = true,
                RepeatUnit = "A"
            };

            Assert.False(Helper.RepeatDeletionFlankedByRepeats("TTATA", rptADeletion, 2));
            Assert.True(Helper.RepeatDeletionFlankedByRepeats("CCAAA", rptADeletion, 2));
            Assert.True(Helper.RepeatDeletionFlankedByRepeats("AAAAA", rptADeletion, 2));
            Assert.False(Helper.RepeatDeletionFlankedByRepeats("TTTAA", rptADeletion, 2));

        }

        [Fact]
        public void IsDuplication()
        {
            //Assert.True(Helper.IsDuplication("TTTTAAA", 4, true, "A", "TAA", 3));
            Assert.True(Helper.IsDuplication("TTTTAAA", 4, true, "A", "TAAA"));
            Assert.True(Helper.IsDuplication("TTTTATG", 4, false, null, "TATG"));
        }

        [Fact]
        public void IsInHomopolymerStretch()
        {
            Assert.True(Helper.IsInHomopolymerStretch("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", 21));
            Assert.True(Helper.IsInHomopolymerStretch("AAAAAAAAAAAATAAAAAAAAAAAAACAAAAAAAAAAA", 21));
            Assert.False(Helper.IsInHomopolymerStretch("ATCGATCGATCGATCGATCGATCGATCGATCGATCGATCG", 21));
            Assert.False(Helper.IsInHomopolymerStretch("ATATATATATATATATATATATATATATATATATATATAT", 21));
            Assert.True(Helper.IsInHomopolymerStretch("ATTATTATTATTTATTATTATTATTTATTATTATTATTTATTATTATTATTT", 21));

            // TODO add more challenging cases

        }

        [Fact]
        public void CandidateToString()
        {
            var preIndel = new PreIndel(new CandidateAllele("chr1", 123, "A", "ATC", AlleleCategory.Insertion));
            Assert.Equal("chr1:123 A>ATC",Helper.CandidateToString(preIndel));

            preIndel = new PreIndel(new CandidateAllele("chr1", 456, "ATC", "A", AlleleCategory.Deletion));
            Assert.Equal("chr1:456 ATC>A", Helper.CandidateToString(preIndel));
        }

        [Fact]
        public void CompareSubstring()
        {
            Assert.True(Helper.CompareSubstring("TCG", "ATCG", 1));
            Assert.False(Helper.CompareSubstring("TCG", "ATCG", 0));
            Assert.True(Helper.CompareSubstring("TCG", "TCG", 0));
            Assert.True(Helper.CompareSubstring("TCG", "ATCGCGA", 1));
        }

        [Fact]
        public void MultiIndelContainsIndel()
        {
            var indel1 = new PreIndel(new CandidateAllele("chr1", 100, "A", "ATC", AlleleCategory.Insertion));
            indel1.InMulti = true;
            indel1.OtherIndel = "chr1:105 AT>A";
            var indel2 = new PreIndel(new CandidateAllele("chr1", 105, "AT", "A", AlleleCategory.Deletion));

            var otherMultiIndel = new PreIndel(new CandidateAllele("chr1", 100, "A", "ATC", AlleleCategory.Insertion));
            otherMultiIndel.InMulti = true;
            otherMultiIndel.OtherIndel = "chr1:107 AT>A";

            var multiWithIndel2AsPrimary = new PreIndel(new CandidateAllele("chr1", 105, "AT", "A", AlleleCategory.Deletion));
            multiWithIndel2AsPrimary.InMulti = true;
            multiWithIndel2AsPrimary.OtherIndel = "chr1:100 AT>A";


            Assert.True(Helper.MultiIndelContainsIndel(indel1, indel2));
            Assert.False(Helper.MultiIndelContainsIndel(otherMultiIndel, indel2));
            Assert.True(Helper.MultiIndelContainsIndel(multiWithIndel2AsPrimary, indel2));

            var nonMultiIndel = new PreIndel(new CandidateAllele("chr1", 100, "A", "ATC", AlleleCategory.Insertion));
            Assert.Throws<ArgumentException>(() => { Helper.MultiIndelContainsIndel(nonMultiIndel, indel2); });
        }

        [Fact]
        public void IsValidMap()
        {
            var positionMap = new int[] {0, 1, -1, 2, 3};
            Assert.True(Helper.IsValidMap(positionMap));

            positionMap = new int[] {-1, -1, -1};
            Assert.False(Helper.IsValidMap(positionMap));
        }
    }

    
}