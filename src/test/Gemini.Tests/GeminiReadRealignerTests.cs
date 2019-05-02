using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Gemini.FromHygea;
using Gemini.Types;
using Gemini.Utility;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;
using Xunit;

namespace Gemini.Tests
{
    public class GeminiReadRealignerTests
    {

        [Fact]
        public void Insertion_Scenarios()
        {
            // COORD:    1234567890      1234567890
            // WT:       ACGTACGTAC------GTACGTACGTACGTACGTACGTACGTACGT
            // MUTANT:   ACGTACGTACTATATAGTACGTACGTACGTACGTACGTACGTACGT
            var chrReference = string.Join(string.Empty, Enumerable.Repeat("ACGT", 10));

            var indel = new CandidateIndel(new CandidateAllele("chr", 10, "C", "CTATATA", AlleleCategory.Insertion));

            //// --------------
            //// read fully spanning insertion
            //// --------------
            //// read anchored on left
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 5, // zero based
            //        CigarData = new CigarAlignment("5S5M5I5M"),  // soft clip shouldn't affect anchor
            //        Bases = "ACGTACGTACTATATAATAC"
            //    }),
            //    true, 1, 1, 1, "10M6I4M");
            //// ...with remasking, should not see softclip if it all matches
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 5, // zero based
            //        CigarData = new CigarAlignment("5S5M5I5M"),  // soft clip shouldn't affect anchor
            //        Bases = "ACGTACGTACTATATAATAC"
            //    }),
            //    true, 1, 1, 1, "10M6I4M", true);
            //// ...with remasking, should not see the softclip there even if doesn't match (we remask on Ns Only)
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 5, // zero based
            //        CigarData = new CigarAlignment("5S5M5I5M"),  // soft clip shouldn't affect anchor
            //        Bases = "TACGTACGTATATATAATAC"
            //    }),
            //    true, 1, 1, 11, "10M6I4M", true);
            //// ...with remasking, should not see the softclip there even if doesn't match (we remask on Ns Only)
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 5, // zero based
            //        CigarData = new CigarAlignment("5S5M5I5M"),  // soft clip shouldn't affect anchor
            //        Bases = "NNCGTACGTATATATAATAC"
            //    }),
            //    true, 3, 1, 9, "2S8M6I4M", true);

            //// Ns should be untouched
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 5, // zero based
            //        CigarData = new CigarAlignment("5S5M5I5M3S"),  // soft clip shouldn't affect anchor
            //        Bases = "NNCGTACGTATATATAATACNNN"
            //    }),
            //    true, 3, 1, 9, "2S8M6I4M3S", true);

            //// Ns should be untouched but other softclips broken into
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 5, // zero based
            //        CigarData = new CigarAlignment("5S5M5I5M3S"),  // soft clip shouldn't affect anchor
            //        Bases = "NNCGTACGTATATATAATACTNN"
            //    }),
            //    true, 3, 1, 10, "2S8M6I5M2S", true);

            //// try again but with different cigar
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 0, // zero based
            //        CigarData = new CigarAlignment("1M18I5D1M"),
            //        Bases = "ACGTACGTAATATATAATAC"
            //    }),
            //    true, 1, 1, 2, "10M6I4M");

            //// with mismatch in inserted sequence -> if above mismatch threshold, should not align
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 0, // zero based
            //        CigarData = new CigarAlignment("1M18I5D1M"),
            //        Bases = "ACGTACGTACCATCTAGTAC"
            //    }),
            //    false, 0, 0, 0, null);

            //// with mismatch in inserted sequence -> if insertion is shorter than threshold, should not align
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 0, // zero based
            //        CigarData = new CigarAlignment("1M18I5D1M"),
            //        Bases = "ACGTACGTACCATATAGTAC"
            //    }),
            //    false, 0, 0, 0, null, minSizeInsertionToAllowMismatch: 10);

            //// with mismatch in inserted sequence -> if above length threshold and below mismatch threshold, should align
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 0, // zero based
            //        CigarData = new CigarAlignment("1M18I5D1M"),
            //        Bases = "ACGTACGTACCATATAGTAC"
            //    }),
            //    true, 1, 1, 0, "10M6I4M");

            //// read anchored on right
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 0, // zero based
            //        CigarData = new CigarAlignment("20M2S"),  // soft clip shouldn't affect anchor
            //        Bases = "GTACTATATAGTACGTACGTAC"
            //    }),
            //    true, 7, 1, 0, "4M6I12M");

            //// try again but with different cigar.  
            //// doesnt really matter since we clear the original, so long as anchor point is correct
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 20, // zero based
            //        CigarData = new CigarAlignment("1M20I1M"),
            //        Bases = "GTACTATATAGTACGTACGTAC"
            //    }),
            //    true, 7, 1, 0, "4M6I12M");

            //// with mismatch in inserted sequence -> if above mismatch threshold, should not align
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 0, // zero based
            //        CigarData = new CigarAlignment("20M2S"),  // soft clip shouldn't affect anchor
            //        Bases = "GTACAATCTAGTACGTACGTAC"
            //    }),
            //    false, 0, 0, 0, null);
            //// with mismatch in inserted sequence -> if insertion is shorter than threshold, should not align
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 0, // zero based
            //        CigarData = new CigarAlignment("20M2S"),  // soft clip shouldn't affect anchor
            //        Bases = "GTACAATATAGTACGTACGTAC"
            //    }),
            //    false, 0, 0, 0, null, minSizeInsertionToAllowMismatch: 10);
            //// with mismatch in inserted sequence -> if above length threshold and below mismatch threshold, should align
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 0, // zero based
            //        CigarData = new CigarAlignment("20M2S"),  // soft clip shouldn't affect anchor
            //        Bases = "GTACTATATAGTACGTACGTAC"
            //    }),
            //    true, 7, 1, 0, "4M6I12M");

            //// --------------
            //// read partially spanning insertion
            //// --------------
            //// read anchored on left
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 5, // zero based
            //        CigarData = new CigarAlignment("5S5M4D3M"),  // soft clip shouldn't affect anchor
            //        Bases = "ACGTACGTACTAT"
            //    }),
            //    true, 1, 1, 0, "10M3I");
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 5, // zero based
            //        CigarData = new CigarAlignment("5S5M4D3M"),  // soft clip shouldn't affect anchor
            //        Bases = "ACGTACGTACTAT"
            //    }),
            //    true, 1, 1, 0, "10M3I", true);
            //// ...with remasking, if not Ns, should see as mismatches
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //    {
            //        Position = 5, // zero based
            //        CigarData = new CigarAlignment("5S5M4D3M"),
            //        Bases = "TACGTACGTATAT"
            //    }),
            //    true, 1, 1, 10, "10M3I", true);
            //// ...with remasking, should still see softclip there (if Ns)
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("5S5M4D3M"),
                    Bases = "NNNNNACGTATAT"
                }),
                true, 6, 1, 5, "5S5M3I", true);
            // read anchored on right
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 17, // zero based
                    CigarData = new CigarAlignment("1M9I1M1S"),  // soft clip shouldn't affect anchor
                    Bases = "TAGTACGTACGT"
                }),
                true, 11, 1, 0, "2I10M");

            // --------------
            // read partially spanning insertion, tested with maskPartialInsertion and/or minimumUnanchoredInsertionLength
            // --------------
            // should expect softclipping of partial insertions if maskPartialInsertion
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("5S5M4D5M"),  // soft clip shouldn't affect anchor
                    Bases = "ACGTACGTACTATAT"
                }),
                true, 1, 1, 0, "10M5I", true);
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("5S5M4D5M"),  // soft clip shouldn't affect anchor
                    Bases = "ACGTACGTACTATAT"
                }),
                true, 1, 0, 0, "10M5S", true, maskPartialInsertion: true, numIncorporatedIndels:1);

            // TODO revisit min unanchored insertion length feature.
            //// should expect softclipping of partial insertions if maskPartialInsertion is false and minimumUnanchoredInsertionLength > insertion length
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //{
            //    Position = 5, // zero based
            //    CigarData = new CigarAlignment("5S5M4D5M"),  // soft clip shouldn't affect anchor
            //    Bases = "ACGTACGTACTATAT"
            //}),
            //true, 1, 0, 0, "10M5S", true, minUnanchoredInsertionLength: 7);

            // should not expect softclipping of partial insertions if maskPartialInsertion is false and minimumUnanchoredInsertionLength <= insertion length
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("5S5M4D5M"),  // soft clip shouldn't affect anchor
                    Bases = "ACGTACGTACTATAT"
                }),
                true, 1, 1, 0, "10M5I", true, minUnanchoredInsertionLength: 6);
            // complete but un-anchored insertions are allowed even if maskPartialInsertion
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M4D6M"),  // soft clip shouldn't affect anchor
                Bases = "ACGTACGTACTATATA"
            }), true, 1, 1, 0, "10M6I", true);

            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("5S5M4D6M"),  // soft clip shouldn't affect anchor
                    Bases = "ACGTACGTACTATATA"
                }),
                true, 1, 1, 0, "10M6I", true, maskPartialInsertion: true);

            // TODO revisit min unanchored insertion length feature.
            //// complete but un-anchored insertions are not allowed if minimumUnanchoredInsertionLength > insertion length (maskPartialInsertion is false)
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //{
            //    Position = 5, // zero based
            //    CigarData = new CigarAlignment("5S5M4D6M"),  // soft clip shouldn't affect anchor
            //    Bases = "ACGTACGTACTATATA"
            //}), true, 1, 0, 0, "10M6S", true, minUnanchoredInsertionLength: 7);
            //// complete but un-anchored insertions are not allowed if minimumUnanchoredInsertionLength > insertion length (maskPartialInsertion is true)
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //{
            //    Position = 5, // zero based
            //    CigarData = new CigarAlignment("5S5M4D6M"),  // soft clip shouldn't affect anchor
            //    Bases = "ACGTACGTACTATATA"
            //}), true, 1, 0, 0, "10M6S", true, maskPartialInsertion: true, minUnanchoredInsertionLength: 7);
            //// complete but un-anchored insertions are allowed if minimumUnanchoredInsertionLength <= insertion length
            //TestRead(chrReference, indel, new Read("chr", new BamAlignment
            //{
            //    Position = 5, // zero based
            //    CigarData = new CigarAlignment("5S5M4D6M"),  // soft clip shouldn't affect anchor
            //    Bases = "ACGTACGTACTATATA"
            //}), true, 1, 1, 0, "10M6I", true, minUnanchoredInsertionLength: 6);

            // anchored insertions are not affected by maskPartialInsertion
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M4D7M"),  // soft clip shouldn't affect anchor
                Bases = "ACGTACGTACTATATAG"
            }), true, 1, 1, 0, "10M6I1M", true);

            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("5S5M4D7M"),  // soft clip shouldn't affect anchor
                    Bases = "ACGTACGTACTATATAG"
                }),
                true, 1, 1, 0, "10M6I1M", true, maskPartialInsertion: true);
            // anchored insertions are not affected by maskPartialInsertion or minimumUnanchoredInsertionLength
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M4D7M"),  // soft clip shouldn't affect anchor
                Bases = "ACGTACGTACTATATAG"
            }), true, 1, 1, 0, "10M6I1M", true, minUnanchoredInsertionLength: 7);

            // ...with remasking and maskPartialInsertion, should see N softclip merged with partial insertion softclip
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("5M4D3M5S"),
                    Bases = "CGTACTATNNNNN"
                }),
                true, 6, 0, 0, "5M8S", true, maskPartialInsertion: true, numIncorporatedIndels:1);

            // --------------
            // positive edge cases - insertion just in range (1 base into)
            // --------------
            // left anchor - right edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 1, // zero based
                    CigarData = new CigarAlignment("10M"),
                    Bases = "CGTACGTACT"
                }),
                true, 2, 1, 0, "9M1I");

            // right anchor - left edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 17, // zero based
                    CigarData = new CigarAlignment("1M8I1M"),
                    Bases = "AGTACGTACG"
                }),
                true, 11, 1, 0, "1I9M");

            // --------------
            // negative edge cases - insertion out of range 
            // --------------
            // left anchor - left edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 15, // zero based
                    CigarData = new CigarAlignment("10M"),
                    Bases = "GTACGTACGT"
                }),
                false, 0, 0, 0, null);

            // left anchor - right edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 0, // zero based
                    CigarData = new CigarAlignment("1M20D9M"),
                    Bases = "ACGTACGTAC"
                }),
                false, 0, 0, 0, null);

            // right anchor - left edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 20, // zero based
                    CigarData = new CigarAlignment("5I5M"),
                    Bases = "GTACGTACGT"
                }),
                false, 0, 0, 0, null);

            // right anchor - right edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("5S5M"),
                    Bases = "ACGTACGTAC"
                }),
                false, 0, 0, 0, null);



            var repeatInsertion = Map(new CandidateIndel(new CandidateAllele("chr", 3, "C", "CAAA", AlleleCategory.Insertion)));
            repeatInsertion.IsDuplication = true;

            // Insertion is same as reference (marked as duplication). Read shows what could be one or the other. Don't realign it to insertion.
            TestRead("ATCAAAG", new List<HashableIndel>(){}, new Read("chr", new BamAlignment
                {
                    Position = 0, // zero based
                    CigarData = new CigarAlignment("6M"),
                    Bases = "ATCAAA"
                }),
                false, 0, 0, 0, null, 0, pairSpecific: false);

            // If same insertion is seen in mate (pairSpecific = true), do realign it.
            TestRead("ATCAAAG", new List<HashableIndel>() { }, new Read("chr", new BamAlignment
                {
                    Position = 0, // zero based
                    CigarData = new CigarAlignment("6M"),
                    Bases = "ATCAAA"
                }),
                false, 0, 0, 0, null, 0, pairSpecific: true);

            repeatInsertion.IsDuplication = false;
            // If same insertion is not explicitly marked as duplication, do realign it.
            TestRead("ATCAAAG", new List<HashableIndel>() { }, new Read("chr", new BamAlignment
                {
                    Position = 0, // zero based
                    CigarData = new CigarAlignment("6M"),
                    Bases = "ATCAAA"
                }),
                false, 0, 0, 0, null, 0, pairSpecific: false);

        }

        [Fact]
        public void RealExample_NStretch()
        {
            // --------------
            // shouldn't allow indel in N-stretch 
            // --------------

            var refTruePosition = 29677218 - 200;
            var chrReference =
                new string('X', 200) + "AAGAAGTTCGAAGTCGCTGCAGCCTAAAACATAGAAAGTCACTTCTTC";

            var insertionInNStretch = new CandidateIndel(new CandidateAllele("chr17", 29677186 - refTruePosition, "C", "CA",
                AlleleCategory.Insertion));
            var alignment = new Read("chr17", new BamAlignment
            {
                Position = 29677218 - refTruePosition, // zero based
                CigarData = new CigarAlignment("53S48M"),
                Bases =
                    "NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNGAAGAAGTTCGAAGTCGCTGCAGCCTAAAACATAGAAAGTCACTTCTTC"
            });

            TestRead(chrReference, insertionInNStretch, alignment, false, 0, 0, 0, null); // Otherwise was getting 20M1I80M

            var deletionInNStretch = new CandidateIndel(new CandidateAllele("chr17", 29677186 - refTruePosition, "CA", "C", AlleleCategory.Deletion));
            TestRead(chrReference, deletionInNStretch, alignment, false, 0, 0, 0, null);
            deletionInNStretch.ReferencePosition = 29677218 - 3 - refTruePosition; // Right at the end of the N stretch
            //TestRead(chrReference, deletionInNStretch, alignment, false, 0, 0, 0, null);
            var simpleSimRead = new Read("chr17", new BamAlignment()
            {
                Position = 5, // 0 based
                CigarData = new CigarAlignment("3S3M3S"),
                Bases = "NNNFGHNNN"
            });

            var deletionInPrefixNs = new CandidateIndel(new CandidateAllele("chr17", 3, "CD", "C", AlleleCategory.Deletion)); // 1 based
            var deletionInSuffixNs = new CandidateIndel(new CandidateAllele("chr17", 9, "HI", "H", AlleleCategory.Deletion)); // 1 based
            //123 4 5678 9 0
            //012 3 4567 8 9
            //ABC D EFGH I J
            //ABC - EFGH I J
            //ABC D EFGH - J
            //    nnnFGHnnn
            TestRead("ABCDEFGHIJKLM", deletionInPrefixNs, simpleSimRead, false, 0, 0, 0, null);
            TestRead("ABCDEFGHIJKLM", deletionInSuffixNs, simpleSimRead, false, 0, 0, 0, null);

            var insertionInPrefixNs = new CandidateIndel(new CandidateAllele("chr17", 3, "C", "CX", AlleleCategory.Insertion));
            var insertionInSuffixNs = new CandidateIndel(new CandidateAllele("chr17", 10, "I", "IX", AlleleCategory.Insertion));
            //123 4567890
            //012 3456789
            //ABC DEFGHIJ
            //ABCXDEFGHIJ
            //12345678901
            //01234567890
            //ABCDEFGHIXJ
            //  nnnFGHnnn
            TestRead("ABCDEFGHIJKLM", insertionInPrefixNs, simpleSimRead, false, 0, 0, 0, null);
            TestRead("ABCDEFGHIJKLM", insertionInSuffixNs, simpleSimRead, false, 0, 0, 0, null);
        }

        [Fact]
        public void InsertionCases_R2Simulation()
        {
            // real world example from R2 cosmic simulation (COSM847)

            var refTruePosition = 28608201;
            var chrReference =
                "GGCACATTCCATTCTTACCAAACTCTAAATTTTCTCTTGGAAACTCCCATTTGAGATCATATTCATATTCTCTGAAATCAACGTAGAAGTACTCATTATCTGAGGAGCCGGTCACCTGTACCATCTGTAGCTGGCTTTCATACCTAAATTGCTTCAGAGATGAAATGATGAGTCAGTTAGGAATAGGCAGTTCTGCAGATAGAGGAAAGAATAATGAATTTTTACCTTTGCTTTTACCTTTTTGTACTTGTGACAAATTAGCAGGGTTAAAACGACAATGAAGAGGAGACAAACACCAAT";

            var indel = new CandidateIndel(new CandidateAllele("chr13", 28608238 - refTruePosition + 1, "T", "TGGAAACTCCCATTTGAGATCATATTCATAAAGGCTC",
                AlleleCategory.Insertion));

            TestRead(chrReference, indel, new Read("chr13", new BamAlignment
                {
                    Position = 28608247 - refTruePosition, // zero based
                    CigarData = new CigarAlignment("10M36I29M"),
                    Bases = "CCATTTGAGATCATATTCATAAAGGCTCGGAAACTCCCATTTGAGATCATATTCATATTCTCTGAAATCAACGTA"
                }),
                true, 39, 1, 0, "28I47M", numIncorporatedIndels:1);

            TestRead(chrReference, indel, new Read("chr13", new BamAlignment
                {
                    Position = 28608247 - refTruePosition, // zero based
                    CigarData = new CigarAlignment("10M36I29M"),
                    Bases = "CCATTTGAGATCATATTCATAAAGGCTCGGAAACTCCCATTTGAGATCATATTCATATTCTCTGAAATCAACGTA"
                }),
                true, 39, 0, 0, "28S47M", maskPartialInsertion: true, numIncorporatedIndels:1);

            TestRead(chrReference, indel, new Read("chr13", new BamAlignment
                {
                    Position = 28608240 - refTruePosition, // zero based
                    CigarData = new CigarAlignment("17M36I22M"),
                    Bases = "GAAACTCCCATTTGAGATCATATTCATAAAGGCTCGGAAACTCCCATTTGAGATCATATTCATATTCTCTGAAAT"
                }),
                true, 39, 1, 0, "35I40M", numIncorporatedIndels:1);

            TestRead(chrReference, indel, new Read("chr13", new BamAlignment
                {
                    Position = 28608240 - refTruePosition, // zero based
                    CigarData = new CigarAlignment("17M36I22M"),
                    Bases = "GAAACTCCCATTTGAGATCATATTCATAAAGGCTCGGAAACTCCCATTTGAGATCATATTCATATTCTCTGAAAT"
                }),
                true, 39, 0, 0, "35S40M", maskPartialInsertion: true, numIncorporatedIndels:1);

        }

        [Fact]
        public void Deletion_Scenarios()
        {
            // Del @ 0, CTATATA -> C
            // COORD:    1234567890123456789012345 67890
            // WT:       ACGTACGTACTATATGTACGTACGT ACGTACGTACGTACGTACGT
            // MUTANT:   ACGTACGTAC-----GTACGTACGT ACGTACGTACGTACGTACGT
            var chrReference = "ACGTACGTACTATATGTACGTACGTACGTACGTACGTACGTACGT";

            var indel = new CandidateIndel(new CandidateAllele("chr", 10, "CTATAT", "C", AlleleCategory.Deletion));

            // --------------
            // read fully spanning deletion
            // --------------
            // read anchored on left, 0 mismatch
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("15M"),  // soft clip shouldn't affect anchor
                    Bases = "CGTACGTACGTACGT"
                }),
                true, 6, 1, 0, "5M5D10M");

            // read anchored on left, 0 mismatch
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 6, // zero based
                    CigarData = new CigarAlignment("1S14M"),  // soft clip shouldn't affect anchor
                    Bases = "CGTACGTACGTACGT"
                }),
                true, 6, 1, 0, "5M5D10M");

            // read anchored on left, 1 mismatch at edge of deletion
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 6, // zero based
                    CigarData = new CigarAlignment("1S14M"),  // soft clip shouldn't affect anchor
                    Bases = "CGTAAGTACGTACGT"
                }),
                true, 6, 1, 1, "5M5D10M");

            // try again but with different cigar
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 9, // zero based
                    CigarData = new CigarAlignment("2S2I10M1S"),  // soft clip shouldn't affect anchor
                    Bases = "CGTAATTACGTACGT"
                }),
                true, 6, 1, 2, "5M5D10M");

            // read anchored on right
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 22, // zero based
                    CigarData = new CigarAlignment("12S1M2S"),  // soft clip shouldn't affect anchor
                    Bases = "CGTACGTACGTACGT"
                }),
                true, 6, 1, 0, "5M5D10M");

            // --------------
            // positive edge cases - deletion just barely in range
            // --------------
            // left anchor - left edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 9, // zero based
                    CigarData = new CigarAlignment("11M"),
                    Bases = "CGTACGTACGT"
                }),
                true, 10, 1, 0, "1M5D10M");

            // left anchor - right edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 4, // zero based
                    CigarData = new CigarAlignment("11M"),
                    Bases = "ACGTACGTACG"
                }),
                true, 5, 1, 0, "6M5D5M");

            // right anchor - left edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 23, // zero based
                    CigarData = new CigarAlignment("1M9I1M"),
                    Bases = "CGTACGTACGT"
                }),
                true, 10, 1, 0, "1M5D10M");

            // right anchor - right edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 18, // zero based
                    CigarData = new CigarAlignment("1M9I1M"),
                    Bases = "ACGTACGTACG"
                }),
                true, 5, 1, 0, "6M5D5M");

            // --------------
            // negative edge cases - deletion out of range 
            // --------------
            // left anchor - left edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 15, // zero based
                    CigarData = new CigarAlignment("10M"),
                    Bases = "GTACGTACGT"
                }),
                false, 0, 0, 0, null);

            // left anchor - right edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 0, // zero based
                    CigarData = new CigarAlignment("1M20D9M"),
                    Bases = "ACGTACGTAC"
                }),
                false, 0, 0, 0, null);

            // right anchor - left edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 20, // zero based
                    CigarData = new CigarAlignment("5I5M"),
                    Bases = "GTACGTACGT"
                }),
                false, 0, 0, 0, null);

            // right anchor - right edge
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
                {
                    Position = 5, // zero based
                    CigarData = new CigarAlignment("5S5M"),
                    Bases = "ACGTACGTAC"
                }),
                false, 0, 0, 0, null);
        }

        [Fact]
        public void TwoIndel_InsPlusDel_Scenarios()
        {
            // Del @ 10, CTATAT -> C
            // Ins @ 21, T-> TCCCCC
            // Ins @ 18, A -> AGG

            // COORD:     123456789012345678901-----234567890123456789012345
            // WT:        ACGTACGTACTATATGTACGT-----ACGTACGTACGTACGTACGTACGT
            // MUTANT1:   ACGTACGTAC-----GTACGTCCCCCACGTACGTACGTACGTACGTACGT

            // COORD:     123456789012345678--901234567890123456789012345
            // WT:        ACGTACGTACTATATGTA--CGTACGTACGTACGTACGTACGTACGT
            // MUTANT2:   ACGTACGTAC-----GTAGGCGTACGTACGTACGTACGTACGTACGT

            var chrReference = "ACGTACGTACTATATGTACGTACGTACGTACGTACGTACGTACGT";

            var deletion = Map(new CandidateIndel(new CandidateAllele("chr", 10, "CTATAT", "C", AlleleCategory.Deletion)));
            var insertion = Map(new CandidateIndel(new CandidateAllele("chr", 21, "T", "TCCACC", AlleleCategory.Insertion)));
            var insertion2 = Map(new CandidateIndel(new CandidateAllele("chr", 18, "A", "AGG", AlleleCategory.Insertion)));
            var indelsGroup1 = PairIndels(deletion, insertion);
            var indelsGroup2 = PairIndels(deletion, insertion2);
            var indels = indelsGroup1.Concat(indelsGroup2).ToList();

            //var indels = new List<CandidateIndel>() { insertion, deletion, insertion2 };
            //var indelCandidateGroups = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(deletion.ToString(), insertion.ToString(), null), new Tuple<string, string, string>(deletion.ToString(), insertion2.ToString(), null) };
            //var indelCandidateGroups2 = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(deletion.ToString(), insertion.ToString(), null) };

            var tests = new List<RealignmentTest>();

            // --------------
            // Mutant 1
            // --------------
            // anchor on left, one mismatch at edge of insertion
            tests.Add(new RealignmentTest()
            {
                Position = 8,
                Cigar = "18M",
                Sequence = "TACGTACGTCCACCTCGT",
                ShouldAlign = true,
                NewPosition = 8,
                NewCigar = "3M5D6M5I4M",
                NumIndels = 2,
                NumMismatches = 1
            });

            //// anchor on right
            tests.Add(new RealignmentTest()
            {
                Position = 11,
                Cigar = "3S15M",
                Sequence = "TACGTACGTCCACCTCGT",
                ShouldAlign = true,
                NewPosition = 8,
                NewCigar = "3M5D6M5I4M",
                NumIndels = 2,
                NumMismatches = 1
            });

            //// unanchored insertion on right
            tests.Add(new RealignmentTest()
            {
                Position = 8,
                Cigar = "14M",
                Sequence = "TACGTACGTCCACC",
                ShouldAlign = true,
                NewPosition = 8,
                NewCigar = "3M5D6M5I",
                NumIndels = 2,
                NumMismatches = 0
            });


            // --------------
            // Mutant 2
            // --------------
            tests.Add(new RealignmentTest()
            {
                Position = 4,
                Cigar = "7M5D6M",
                Sequence = "TACCTACGTAGGC",
                ShouldAlign = true,
                NewPosition = 4,
                NewCigar = "7M5D3M2I1M",
                NumIndels = 2,
                NumMismatches = 1
            });

            tests.Add(new RealignmentTest()
            {
                Position = 4,
                Cigar = "13M",
                Sequence = "TACCTACGTAGGC",
                ShouldAlign = true,
                NewPosition = 4,
                NewCigar = "7M5D3M2I1M",
                NumIndels = 2,
                NumMismatches = 1
            });

            //// --------------
            //// Mutant 1 - insertion only
            //// --------------
            tests.Add(new RealignmentTest()
            {
                Position = 13,
                Cigar = "12M",
                Sequence = "TATGTACGTCCC",
                ShouldAlign = true,
                NewPosition = 13,
                NewCigar = "9M3I",
                NumIndels = 1,
                NumMismatches = 0
            });

            // from right
            tests.Add(new RealignmentTest()
            {
                Position = 18,
                Cigar = "21M",
                Sequence = "CCCCACGTACGTACGTACGTA",
                ShouldAlign = true,
                NewPosition = 22,
                NewCigar = "4I17M",
                NumIndels = 1,
                NumMismatches = 0
            });

            ExecuteTests(tests, indels, chrReference);

            // test MaskPartialInsertion
            var testsMaskPartialInsertion = new List<RealignmentTest>();
            testsMaskPartialInsertion.Add(new RealignmentTest()
            {
                Position = 8,
                Cigar = "13M",
                Sequence = "TACGTACGTCCAC",
                ShouldAlign = true,
                NewPosition = 8,
                NewCigar = "3M5D6M4S",
                NumIndels = 1,
                NumMismatches = 0,
                NumIncorporatedIndels = 2
            });
            ExecuteTests(testsMaskPartialInsertion, indels, chrReference, maskPartialInsertion: true);

            // TODO revisit min unanchored insertion length feature.
            // test minimumUnanchoredInsertionLength
            //var testsMinUnanchoredInsertion = new List<RealignmentTest>();
            //testsMinUnanchoredInsertion.Add(new RealignmentTest()
            //{
            //    Position = 8,
            //    Cigar = "14M",
            //    Sequence = "TACGTACGTCCCCC",
            //    ShouldAlign = true,
            //    NewPosition = 8,
            //    NewCigar = "3M5D6M5S",
            //    NumIndels = 1,
            //    NumMismatches = 0
            //});
            //ExecuteTests(testsMinUnanchoredInsertion, indels, chrReference, minUnanchoredInsertionLength: 6);

            // when two indels don't coexist
            var deletion2 = Map(new CandidateIndel(new CandidateAllele("chr", 10, "CTATAT", "C", AlleleCategory.Deletion)));
            var insertionAgain = Map(new CandidateIndel(new CandidateAllele("chr", 21, "T", "TCCACC", AlleleCategory.Insertion)));
            var insertionNotCoexist = Map(new CandidateIndel(new CandidateAllele("chr", 18, "A", "AGG", AlleleCategory.Insertion)));
            var noneCoexist = new List<HashableIndel>() {deletion2, insertionAgain, insertionNotCoexist};
            var twoCoexistOneNot = indelsGroup1.Concat(new List<HashableIndel>() { insertionNotCoexist }).ToList();


            var testNotCoexist = new List<RealignmentTest>();
            testNotCoexist.Add(new RealignmentTest()
            {
                Position = 4,
                Cigar = "13M",
                Sequence = "TACCTACGTAGGC",
                ShouldAlign = true,
                NewPosition = 4,
                NewCigar = "7M5D6M",
                NumIndels = 1,
                NumMismatches = 3
            });
            ExecuteTests(testNotCoexist, noneCoexist, chrReference, pairSpecific:false); // no combination of >1 indel seen
            ExecuteTests(testNotCoexist, twoCoexistOneNot, chrReference, pairSpecific: false); //wrong combination of >1 indel

            //7M5D3M2I1M
            var testNotCoexistButPairSpecific = new List<RealignmentTest>();
            testNotCoexistButPairSpecific.Add(new RealignmentTest()
            {
                Position = 4,
                Cigar = "13M",
                Sequence = "TACCTACGTAGGC",
                ShouldAlign = true,
                NewPosition = 4,
                NewCigar = "7M5D6M", // Now have to explicitly coexist
                NumIndels = 1, // Now have to explicitly coexist
                NumMismatches = 3 // Now have to explicitly coexist
                //NewCigar = "7M5D3M2I1M",
                //NumIndels = 2,
                //NumMismatches = 1
            });
            ExecuteTests(testNotCoexistButPairSpecific, noneCoexist, chrReference, pairSpecific: true); // no combination of >1 indel seen
            ExecuteTests(testNotCoexistButPairSpecific, twoCoexistOneNot, chrReference, pairSpecific: true);  //wrong combination of >1 indel

            // original order of indels shouldnt really matter
            indels.Reverse();
            ExecuteTests(tests, indels, chrReference);
            ExecuteTests(testsMaskPartialInsertion, indels, chrReference, maskPartialInsertion: true);
            // TODO revisit min unanchored insertion length feature.
            //ExecuteTests(testsMinUnanchoredInsertion, indels, chrReference, minUnanchoredInsertionLength: 6);
            ExecuteTests(testNotCoexist, noneCoexist, chrReference, pairSpecific: false); // no combination of >1 indel seen
            ExecuteTests(testNotCoexist, twoCoexistOneNot, chrReference, pairSpecific: false); //wrong combination of >1 indel
            ExecuteTests(testNotCoexistButPairSpecific, noneCoexist, chrReference, pairSpecific: true); // no combination of >1 indel seen
            ExecuteTests(testNotCoexistButPairSpecific, twoCoexistOneNot, chrReference, pairSpecific: true);  //wrong combination of >1 indel
            // TODO different... go back to orig test and see what the intention was
            //ExecuteTests(testNotCoexist, indels, chrReference, candidateGroups: indelCandidateGroups2);

            // Don't allow introduction of repeat indels

        }



        [Fact]
        public void DontIntroduceDelInRepeatSuffix_Scenarios()
        {
            // TODO 
            // Del @ 10, CTATAT -> C
            // Ins @ 21, T-> TCCCCC
            // Ins @ 18, A -> AGG

            // COORD:     123456789012345678901-----234567890123456789012345
            // WT:        ACGTACGTACTATATGTACGT-----ACGTACGTACGTACGTACGTACGT
            // MUTANT1:   ACGTACGTAC-----GTACGTCCCCCACGTACGTACGTACGTACGTACGT

            // COORD:     123456789012345678--901234567890123456789012345
            // WT:        ACGTACGTACTATATGTA--CGTACGTACGTACGTACGTACGTACGT
            // MUTANT2:   ACGTACGTAC-----GTAGGCGTACGTACGTACGTACGTACGTACGT

            var chrReference = "ACGTACGTATAAAAAGGGGTCXXXXXXXXX";

            var refPrefix = "ACGTACGTA";
            var d0 = Map(new CandidateIndel(new CandidateAllele("chr", 10, "TA", "T", AlleleCategory.Deletion)), refPrefix: refPrefix, refSuffix: "AAAAGGGGTCX", numBeforeUnique: 4);
            var d1 = Map(new CandidateIndel(new CandidateAllele("chr", 10, "TAAA", "T", AlleleCategory.Deletion)), refPrefix: refPrefix, refSuffix: "AAGGGGTCXX", numBeforeUnique:2);
            var d2 = Map(new CandidateIndel(new CandidateAllele("chr", 10, "TAAAAA", "T", AlleleCategory.Deletion)), refPrefix: refPrefix, refSuffix: "GGGGTCXXXX", numBeforeUnique:0);
            var d0a = Map(new CandidateIndel(new CandidateAllele("chr", 10, "TAA", "T", AlleleCategory.Deletion)), refPrefix: refPrefix, refSuffix: "AAAGGGGTCX", numBeforeUnique: 3);
            var i1 = Map(new CandidateIndel(new CandidateAllele("chr", 10, "T", "TA", AlleleCategory.Insertion)), refPrefix: refPrefix, refSuffix: "AAAAAGGGGTC", numBeforeUnique:5);
            var i2 = Map(new CandidateIndel(new CandidateAllele("chr", 10, "T", "TCCCC", AlleleCategory.Insertion)),
                refPrefix: refPrefix, refSuffix: "AAAAAGGGGTC", numBeforeUnique:5);
            var indels = new List<HashableIndel>(){d0,d0a,d1,d2,i1,i2};

            // REF:     xxxTAAAAAGGGGTC
            // I1:      xxxTAAAAAAGGGGTC      (T>TA)
            // I2:      xxxTCCCCAAAAAGGGGTC   (T>TCCCC)
            // D0:      xxxTAAAAGGGGTC         (TA>T)
            // D1:      xxxTAAAGGGGTC         (TAA>T)
            // D2:      xxxTGGGGTC           (TAAAA>T)
            // ALT1:    xxxTAA      NO - Could be ref, I1, D0, or D1, no way to know - don't introduce - but this isn't a long enough rpt suffix, need to prevent it another way
            // ALT2:    xxxTAAAA      NO - Could be ref, I1, D0, or D1, no way to know - don't introduce 
            // ALT3:    xxxTAAAAA    NO - Could be I1 or ref
            // ALT4:    xxxTAAAAAA   ? - Could be I1 or maybe something larger we don't know about
            // ALT5:    xxxTAAGGGG    YES - Looks like D1, trust even though it's a read end repeat
            // ALT6:    xxxTCCCC    YES - Probably I2, trust even though it's a read-end repeat
            // ALT7:    xxxTGGGG     YES - Probably D2, trust even though it's a read-end repeat

            // TODO ALT1 is not covered by rpt length suffix, need to account for it another way
            var alt1 = new List<RealignmentTest>();
            //alt1.Add(new RealignmentTest()
            //{
            //    Position = 1,
            //    Cigar = "12M",
            //    Sequence = "ACGTACGTATAA",
            //    ShouldAlign = false,
            //    NewPosition = 1,
            //    NewCigar = "12M",
            //    NumIndels = 0,
            //    NumMismatches = 0,
            //});
            //ExecuteTests(alt1, indels, chrReference, maskPartialInsertion: false);

            // ALT2
            var test = new List<RealignmentTest>();
            test.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "14M",
                Sequence = "ACGTACGTATAAAA",
                ShouldAlign = false,
                NewPosition = 1,
                NewCigar = "14M",
                NumIndels = 0,
                NumMismatches = 0,
            });
            ExecuteTests(test, indels, chrReference, maskPartialInsertion: false, pairSpecific: false);

            // Allowing pair-specific non-spanning repeat indels
            test = new List<RealignmentTest>();
            test.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "14M",
                Sequence = "ACGTACGTATAAAA",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "10M1D4M",
                NumIndels = 1,
                NumMismatches = 0,
                ShouldBeSketchy = true
            });
            ExecuteTests(test, new List<HashableIndel>() { d0}, chrReference, maskPartialInsertion: false, pairSpecific: true);


            // ALT3
            test = new List<RealignmentTest>();
            test.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "15M",
                Sequence = "ACGTACGTATAAAAA",
                ShouldAlign = false,
                NewPosition = 1,
                NewCigar = "15M",
                NumIndels = 0,
                NumMismatches = 0,
            });
            ExecuteTests(test, indels, chrReference, maskPartialInsertion: false, pairSpecific: false);
            test = new List<RealignmentTest>();
            test.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "15M",
                Sequence = "ACGTACGTATAAAAA",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "10M1I4M",
                NumIndels = 1,
                NumMismatches = 0,
                ShouldBeSketchy = true
            });
            ExecuteTests(test, new List<HashableIndel>() { i1 }, chrReference, maskPartialInsertion: false, pairSpecific: true);
        

            // ALT4
            test = new List<RealignmentTest>();
            test.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "16M",
                Sequence = "ACGTACGTATAAAAAA",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "10M1I5M",
                NumIndels = 1,
                NumMismatches = 0,
            });
            ExecuteTests(test, indels, chrReference, maskPartialInsertion: false);
            test = new List<RealignmentTest>();
            test.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "16M",
                Sequence = "ACGTACGTATAAAAAA",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "10M1I5M",
                NumIndels = 1,
                NumMismatches = 0,
            });
            ExecuteTests(test, indels, chrReference, maskPartialInsertion: false, pairSpecific: false);
            // TODO what if there are two possible insertions: T>TA and T>TAA? If we turn partial insertion masking on does this become T>TA or does it get masked as a partial insertion?

            // ALT5:    xxxTAAGGGG    YES - Looks like D1, trust even though it's a read end repeat
            test = new List<RealignmentTest>();
            test.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "16M",
                Sequence = "ACGTACGTATAAGGGG",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "10M3D6M",
                NumIndels = 1,
                NumMismatches = 0,
            });
            ExecuteTests(test, indels, chrReference, maskPartialInsertion: false);

            // ALT6:    xxxTCCCC    YES - Probably I2, trust even though it's a read-end repeat
            test = new List<RealignmentTest>();
            test.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "14M",
                Sequence = "ACGTACGTATCCCC",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "10M4I",
                NumIndels = 1,
                NumMismatches = 0,
            });
            ExecuteTests(test, new List<HashableIndel>() {d0, d1, i2 }, chrReference, maskPartialInsertion: false);

            // ALT7:    xxxTGGGG     YES - Probably D2, trust even though it's a read-end repeat
            test = new List<RealignmentTest>();
            test.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "14M",
                Sequence = "ACGTACGTATGGGG",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "10M5D4M",
                NumIndels = 1,
                NumMismatches = 0,
            });
            ExecuteTests(test, indels, chrReference, maskPartialInsertion: false);

            //TODO - trickier repeat scenarios
            // REF: xxxGAAAGGGCT
            // D3:  xxxGGGGGCT          (GAAA>G)



            //var testsMaskPartialInsertion = new List<RealignmentTest>();
            //testsMaskPartialInsertion.Add(new RealignmentTest()
            //{
            //    Position = 8,
            //    Cigar = "13M",
            //    Sequence = "TACGTACGTCCCC",
            //    ShouldAlign = true,
            //    NewPosition = 8,
            //    NewCigar = "3M5D6M4S",
            //    NumIndels = 1,
            //    NumMismatches = 0
            //});
            //ExecuteTests(testsMaskPartialInsertion, indels, chrReference, maskPartialInsertion: true);

        }

        [Fact]
        public void TwoIndel_InsPlusDel_SamePosition_Scenarios()
        {
            // real world case from R2 FDA FDA-07-var2_S2.bam
            var chrReference = "GTCGCTATCAAGGAATTAAGAGAAGCAACATCTCCGAAAGCCAACAAGGAAATCCTCGATGTGAGTTTCTGCTTTGCTGTGTGGGGGTCCATGGCTCT";

            var insertion = Map(new CandidateIndel(new CandidateAllele("chr", 12, "G", "GTTGCT", AlleleCategory.Insertion)));
            var deletion = Map(new CandidateIndel(new CandidateAllele("chr", 12, "GGAATTAAGAGAAGCAACATC", "G", AlleleCategory.Deletion)));
            //var indelPair = new List<CandidateIndel> { insertion, deletion }.OrderBy(g => g.ReferencePosition).ThenBy(t => t.ReferenceAllele).Select(x => x.ToString()).ToList();
            //var indelCandidateGroups = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(indelPair[0], indelPair[1], null) };
            var indels = PairIndels(insertion, deletion);

            var tests = new List<RealignmentTest>();

            tests.Add(new RealignmentTest()
            {
                Position = 25,
                Cigar = "7M2I66M",
                Sequence = "TCAAGTTGCTTCCGAAAGCCAACAAGGAAATCCTCGATGTGAGTTTCTGCTTTGCTGTGTGGGGGTCCATGGCTC",
                ShouldAlign = true,
                NewPosition = 8,
                NewCigar = "5M5I20D65M",
                NumIndels = 2,
                NumMismatches = 0
            });

            ExecuteTests(tests, indels, chrReference);
        }

        [Fact]
        public void TwoIndel_InsPlusDel_SamePositionCancelOut_Scenarios()
        {
            var chrReference = "ATCGATGCTAX";

            var insertion = Map(new CandidateIndel(new CandidateAllele("chr", 5, "A", "AT", AlleleCategory.Insertion)));
            var deletion = Map(new CandidateIndel(new CandidateAllele("chr", 5, "AT", "A", AlleleCategory.Deletion)));
            //var indels = PairIndels(insertion, deletion);
            // This test was previously showing that we could prevent these two interacting indels from coexisting, but we no longer check for that because we just make sure they've never been seen together in a read. There's no way you would have seen coexisting indels together in a read (unless something went horribly wrong??) so I'm not going to pair them here.
            var indels = new List<HashableIndel>() {insertion, deletion};
            var tests = new List<RealignmentTest>();

            // Best realignment result should be the not-so-good introduction of just one of the above indels. It should not be the falsely good introduction of the two cancelling each other out.
            // This realignment would then be compared to the original alignment, which didn't have any mismatches, and be discarded.
            tests.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "10M",
                Sequence = "ATCGATGCTA",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "5M1I4M",
                NumIndels = 1,
                NumMismatches = 4
            });


            // Independently each should be successfully incorporated if appropriate
            tests.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "10M",
                Sequence = "ATCGATTGCT",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "5M1I4M",
                NumIndels = 1,
                NumMismatches = 0
            });

            tests.Add(new RealignmentTest()
            {
                Position = 1,
                Cigar = "10M",
                Sequence = "ATCGAGCTAX",
                ShouldAlign = true,
                NewPosition = 1,
                NewCigar = "5M1D5M",
                NumIndels = 1,
                NumMismatches = 0
            });


            ExecuteTests(tests, indels, chrReference);
        }

        [Fact]
        public void TwoIndel_InsPlusIns_Scenarios()
        {
            // COORD:     1234567890123456----7-----890123456789012345678901
            // WT:        ACGTACGTACTATATG----T-----ACGTACGTACGTACGTACGTACGT
            // MUTANT1:   ACGTACGTACTATATGAAAATCCCCCACGTACGTACGTACGTACGTACGT
            var chrReference = "ACGTACGTACTATATGTACGTACGTACGTACGTACGTACGT";

            var insertion = Map(new CandidateIndel(new CandidateAllele("chr", 16, "G", "GAAAA", AlleleCategory.Insertion)));
            var insertion2 = Map(new CandidateIndel(new CandidateAllele("chr", 17, "T", "TCCCCC", AlleleCategory.Insertion)));
            var indels = PairIndels(insertion, insertion2);

            var tests = new List<RealignmentTest>();

            // both from left
            tests.Add(new RealignmentTest()
            {
                Position = 12,
                Cigar = "20M",
                Sequence = "ATATGAAAATCCCCCACGTA",
                ShouldAlign = true,
                NewPosition = 12,
                NewCigar = "5M4I1M5I5M",
                NumIndels = 2,
                NumMismatches = 0
            });

            // both from right
            tests.Add(new RealignmentTest()
            {
                Position = 10,
                Cigar = "20M",
                Sequence = "AATCCCCCACGTACGTACGT",
                ShouldAlign = true,
                NewPosition = 17,
                NewCigar = "2I1M5I12M",
                NumIndels = 2,
                NumMismatches = 0
            });

            //// one from left only
            tests.Add(new RealignmentTest()
            {
                Position = 8,
                Cigar = "10M",
                Sequence = "TACTATATGA",
                ShouldAlign = true,
                NewPosition = 8,
                NewCigar = "9M1I",
                NumIndels = 1,
                NumMismatches = 0
            });

            // unanchored insertion at the right end
            tests.Add(new RealignmentTest()
            {
                Position = 12,
                Cigar = "15M",
                Sequence = "ATATGAAAATCCCCC",
                ShouldAlign = true,
                NewPosition = 12,
                NewCigar = "5M4I1M5I",
                NumIndels = 2,
                NumMismatches = 0
            });
            // unanchored insertion at the left end
            tests.Add(new RealignmentTest()
            {
                Position = 8,
                Cigar = "22M",
                Sequence = "AAAATCCCCCACGTACGTACGT",
                ShouldAlign = true,
                NewPosition = 17,
                NewCigar = "4I1M5I12M",
                NumIndels = 2,
                NumMismatches = 0
            });

            ExecuteTests(tests, indels, chrReference);

            // test MaskPartialInsertion
            var testsMaskPartialInsertion = new List<RealignmentTest>();
            // partial insertion at the right end
            testsMaskPartialInsertion.Add(new RealignmentTest()
            {
                Position = 12,
                Cigar = "13M",
                Sequence = "ATATGAAAATCCC",
                ShouldAlign = true,
                NewPosition = 12,
                NewCigar = "5M4I1M3S",
                NumIndels = 1,
                NumMismatches = 0,
                NumIncorporatedIndels = 2
            });
            // partial insertion at the left end
            testsMaskPartialInsertion.Add(new RealignmentTest()
            {
                Position = 10,
                Cigar = "20M",
                Sequence = "AATCCCCCACGTACGTACGT",
                ShouldAlign = true,
                NewPosition = 17,
                NewCigar = "2S1M5I12M",
                NumIndels = 1,
                NumMismatches = 0,
                NumIncorporatedIndels = 2

            });
            ExecuteTests(testsMaskPartialInsertion, indels, chrReference, maskPartialInsertion: true);

            // test minimumUnanchoredInsertionLength
            var testsMinUnanchoredInsertion = new List<RealignmentTest>();
            // unanchored insertion at the right end, >= minUnanchoredInsertionLength
            testsMinUnanchoredInsertion.Add(new RealignmentTest()
            {
                Position = 12,
                Cigar = "15M",
                Sequence = "ATATGAAAATCCCCC",
                ShouldAlign = true,
                NewPosition = 12,
                NewCigar = "5M4I1M5I",
                NumIndels = 2,
                NumMismatches = 0
            });

            // TODO revisit min unanchored insertion length feature.
            //// unanchored insertion at the left end, < minUnanchoredInsertionLength
            //testsMinUnanchoredInsertion.Add(new RealignmentTest()
            //{
            //    Position = 8,
            //    Cigar = "22M",
            //    Sequence = "AAAATCCCCCACGTACGTACGT",
            //    ShouldAlign = true,
            //    NewPosition = 17,
            //    NewCigar = "4S1M5I12M",
            //    NumIndels = 1,
            //    NumMismatches = 0
            //});
            //ExecuteTests(testsMinUnanchoredInsertion, indels, chrReference, minUnanchoredInsertionLength: 5);

            // original order of indels shouldnt really matter
            indels.Reverse();
            ExecuteTests(tests, indels, chrReference);
            ExecuteTests(testsMaskPartialInsertion, indels, chrReference, maskPartialInsertion: true);
            ExecuteTests(testsMinUnanchoredInsertion, indels, chrReference, minUnanchoredInsertionLength: 5);

        }

        private List<HashableIndel> PairIndels(HashableIndel indel1, HashableIndel indel2)
        {

            var indel1New = Helper.CopyHashable(indel1, Helper.HashableToString(indel2));
            var indel2New = Helper.CopyHashable(indel2, Helper.HashableToString(indel1));

            return new List<HashableIndel>() {indel1New, indel2New};
        }

        private static HashableIndel CopyHashable(HashableIndel indel1, string otherIndel = null)
        {
            var indel1New = new HashableIndel()
            {
                AllowMismatchingInsertions = indel1.AllowMismatchingInsertions,
                AlternateAllele = indel1.AlternateAllele,
                Chromosome = indel1.Chromosome,
                InMulti = otherIndel != null,
                IsDuplication = indel1.IsDuplication,
                IsRepeat = indel1.IsRepeat,
                IsUntrustworthyInRepeatRegion = indel1.IsUntrustworthyInRepeatRegion,
                Length = indel1.Length,
                NumBasesInReferenceSuffixBeforeUnique = indel1.NumBasesInReferenceSuffixBeforeUnique,
                ReferencePosition = indel1.ReferencePosition,
                StringRepresentation = indel1.StringRepresentation,
                Score = indel1.Score,
                Type = indel1.Type,
                RefPrefix = indel1.RefPrefix,
                RefSuffix = indel1.RefSuffix,
                OtherIndel = otherIndel,
                ReferenceAllele = indel1.ReferenceAllele,
                RepeatUnit = indel1.RepeatUnit
            };
            return indel1New;
        }

        [Fact]
        public void TwoIndel_DelPlusDel_Scenarios()
        {
            // COORD:     12345678901234567890123456789012345678901
            // WT     :   ACGTACGTACTATATGAAAATCCCCCACGTACGTACGTACG
            // MUTANT1:   ACGTACGTACTATATG----T-----ACGTACGTACGTACG
            var chrReference = "ACGTACGTACTATATGAAAATCCCCCACGTACGTACGTACG";

            var deletion = Map(new CandidateIndel(new CandidateAllele("chr", 16, "GAAAA", "G", AlleleCategory.Deletion)));
            var deletion2 = Map(new CandidateIndel(new CandidateAllele("chr", 21, "TCCCCC", "T", AlleleCategory.Deletion)));
            //deletion.OtherIndel = Helper.HashableToString(deletion2);
            //deletion.InMulti = true;
            //deletion2.OtherIndel = Helper.HashableToString(deletion);
            //deletion2.InMulti = true;

            //var indels = new List<HashableIndel>() {deletion, deletion2};
            var indels = PairIndels(deletion, deletion2);

            var tests = new List<RealignmentTest>();

            // both from left
            tests.Add(new RealignmentTest()
            {
                Position = 12,
                Cigar = "10M",
                Sequence = "ATATGAACGT",
                ShouldAlign = true,
                NewPosition = 12,
                NewCigar = "5M4D1M5D4M",
                NumIndels = 2,
                NumMismatches = 1
            });

            // both from right
            tests.Add(new RealignmentTest()
            {
                Position = 21,
                Cigar = "10M",
                Sequence = "ATATGAACGT",
                ShouldAlign = true,
                NewPosition = 12,
                NewCigar = "5M4D1M5D4M",
                NumIndels = 2,
                NumMismatches = 1
            });

            // one from left only
            tests.Add(new RealignmentTest()
            {
                Position = 12,
                Cigar = "6M",
                Sequence = "ATATGT",
                ShouldAlign = true,
                NewPosition = 12,
                NewCigar = "5M4D1M",
                NumIndels = 1,
                NumMismatches = 0
            });

            ExecuteTests(tests, indels, chrReference);

            // original order of indels shouldnt really matter
            indels.Reverse();
            ExecuteTests(tests, indels, chrReference);
        }

        private void TestRead(string refSequence, CandidateIndel indel, Read read, bool shouldAlign, int position,
            int numIndels, int numMismatches, string cigar, bool remaskSoftclip = false, HashSet<Tuple<string, string, string>> candidateGroups = null, bool maskPartialInsertion = false, int minUnanchoredInsertionLength = 0, int minSizeInsertionToAllowMismatch = 5, bool pairSpecific = true, int numIncorporatedIndels = 0)
        {
            
            var read2 = new Read(read.Chromosome, new BamAlignment
            {
                Position = read.BamAlignment.Position, // zero based
                CigarData = new CigarAlignment(read.CigarData.ToString()),  // soft clip shouldn't affect anchor
                Bases = read.BamAlignment.Bases
            });

            //TestRead(refSequence, new List<CandidateIndel>() { indel }, read, shouldAlign, position, numIndels,
            //    numMismatches, cigar, remaskSoftclip, candidateGroups, maskPartialInsertion, minUnanchoredInsertionLength, minSizeInsertionToAllowMismatch: minSizeInsertionToAllowMismatch);

            TestRead(refSequence, new List<CandidateIndel>() { indel }, read2, shouldAlign, position, numIndels,
                numMismatches, cigar, numIncorporatedIndels > 0 ? numIncorporatedIndels : numIndels, remaskSoftclip, candidateGroups, maskPartialInsertion, minUnanchoredInsertionLength, minSizeInsertionToAllowMismatch: minSizeInsertionToAllowMismatch, offset: 10, pairSpecific: pairSpecific);

        }

        private void TestRead(string refSequence, List<HashableIndel> hashables, Read read, bool shouldAlign, int position, int numIndels, int numMismatches,
            string cigar, int numIncorporatedIndels, bool remaskSoftclip = false, HashSet<Tuple<string, string, string>> candidateGroups = null, bool maskPartialInsertion = false, int minUnanchoredInsertionLength = 0, int minSizeInsertionToAllowMismatch = 5, int offset = 0, bool pairSpecific = true, bool shouldBeSketchy = false)
        {
            read.BamAlignment.Position = read.BamAlignment.Position + offset;
            read.BamAlignment.Qualities = new byte[read.Sequence.Length];
            for (int i = 0; i < read.BamAlignment.Qualities.Length; i++)
            {
                read.BamAlignment.Qualities[i] = 30;
            }

            Dictionary<HashableIndel, GenomeSnippet> sequences = new Dictionary<HashableIndel, GenomeSnippet>();
            foreach (var hashableIndel in hashables)
            {
                sequences.Add(hashableIndel, new GenomeSnippet()
                {
                    Chromosome = hashableIndel.Chromosome,
                    StartPosition = 0 + offset,
                    Sequence = refSequence
                });
            }

            var result = new GeminiReadRealigner(new BasicAlignmentComparer(), remaskSoftclip,
                maskPartialInsertion, minInsertionSizeToAllowMismatchingBases: minSizeInsertionToAllowMismatch).Realign(read, hashables, sequences, pairSpecific);

            if (shouldAlign)
            {
                VerifyResult(result, position + offset, numIndels, numMismatches, cigar, numIncorporatedIndels, shouldBeSketchy);
            }
            else
            {
                Assert.Null(result);
            }
        }

        private void TestRead(string refSequence, List<CandidateIndel> indels, Read read, bool shouldAlign, int position, int numIndels, int numMismatches,
            string cigar, int numIncorporatedIndels, bool remaskSoftclip = false, HashSet<Tuple<string, string, string>> candidateGroups = null, bool maskPartialInsertion = false, int minUnanchoredInsertionLength = 0, int minSizeInsertionToAllowMismatch = 5, int offset = 0, bool pairSpecific = true)
        {
            var hashables = indels.Select(x => Map(x, offset)).ToList();

            TestRead(refSequence, hashables, read, shouldAlign, position, numIndels,
                numMismatches, cigar, numIncorporatedIndels, remaskSoftclip, candidateGroups, maskPartialInsertion, minUnanchoredInsertionLength, minSizeInsertionToAllowMismatch: minSizeInsertionToAllowMismatch, offset: offset, pairSpecific: pairSpecific);

        }

        private void ExecuteTests(List<RealignmentTest> tests, List<HashableIndel> indels, string chrReference, HashSet<Tuple<string, string, string>> candidateGroups = null, bool maskPartialInsertion = false, int minUnanchoredInsertionLength = 0, bool pairSpecific = true)
        {
            foreach (var test in tests)
            {
                TestRead(chrReference, indels, new Read("chr", new BamAlignment
                    {
                        Position = test.Position - 1, // 0-based
                        CigarData = new CigarAlignment(test.Cigar),
                        Bases = test.Sequence
                    }),
                    test.ShouldAlign, test.NewPosition, test.NumIndels, test.NumMismatches, test.NewCigar, maskPartialInsertion: maskPartialInsertion, minUnanchoredInsertionLength: minUnanchoredInsertionLength, pairSpecific: pairSpecific, numIncorporatedIndels: test.NumIncorporatedIndels > 0 ? test.NumIncorporatedIndels : test.NumIndels, shouldBeSketchy: test.ShouldBeSketchy);
            }
        }

        private void ExecuteTests(List<RealignmentTest> tests, List<CandidateIndel> indels, string chrReference, HashSet<Tuple<string, string, string>> candidateGroups = null, bool maskPartialInsertion = false, int minUnanchoredInsertionLength = 0)
        {
            foreach (var test in tests)
            {
                TestRead(chrReference, indels, new Read("chr", new BamAlignment
                    {
                        Position = test.Position,
                        CigarData = new CigarAlignment(test.Cigar),
                        Bases = test.Sequence
                    }),
                    test.ShouldAlign, test.NewPosition, test.NumIndels, test.NumMismatches, test.NewCigar, test.NumIncorporatedIndels, candidateGroups: candidateGroups, maskPartialInsertion: maskPartialInsertion, minUnanchoredInsertionLength: minUnanchoredInsertionLength);
            }
        }

        private void VerifyResult(RealignmentResult result, int position, int numIndels, int numMismatches, string cigar, int numIncorporatedIndels, bool shouldBeSketchy = false)
        {
            Assert.NotNull(result);
            Assert.Equal(cigar, result.Cigar.ToString());
            Assert.Equal(position, result.Position);
            Assert.Equal(numIndels, result.NumIndels);
            Assert.Equal(numIncorporatedIndels, result.AcceptedIndels.Count);
            Assert.Equal(numMismatches, result.NumMismatches);
            Assert.Equal(shouldBeSketchy, result.IsSketchy);
        }

        private class RealignmentTest
        {
            public int Position;
            public string Cigar;
            public string Sequence;

            public bool ShouldAlign;
            public int NewPosition;
            public string NewCigar;
            public int NumIndels;
            public int NumMismatches;
            public int NumIncorporatedIndels;
            public bool ShouldBeSketchy;
        }

        [Fact]
        public void Compare()
        {
            var readRealigner = new GeminiReadRealigner(new BasicAlignmentComparer());

            var deletion = Map(new CandidateIndel(new CandidateAllele("chr1", 10, "AC", "A", AlleleCategory.Deletion)));
            var deletion2 = Map(new CandidateIndel(new CandidateAllele("chr1", 11, "AC", "A", AlleleCategory.Deletion)));
            var insertion = Map(new CandidateIndel(new CandidateAllele("chr1", 10, "A", "AC", AlleleCategory.Insertion)));
            var insertion2 = Map(new CandidateIndel(new CandidateAllele("chr1", 11, "A", "AC", AlleleCategory.Insertion)));

            var all = new List<HashableIndel> { insertion2, deletion2, deletion, insertion };
            all.Sort(readRealigner.CompareSimple);

            Assert.Equal(insertion, all[0]);
            Assert.Equal(deletion, all[1]);
            Assert.Equal(insertion2, all[2]);
            Assert.Equal(deletion2, all[3]);
        }

        private HashableIndel Map(CandidateIndel indel, int offset = 0, string refPrefix = null, string refSuffix = null, int numBeforeUnique = 0, string multiIndel = null)
        {
            return new HashableIndel()
            {
                AlternateAllele = indel.AlternateAllele,
                Chromosome = indel.Chromosome,
                ReferenceAllele = indel.ReferenceAllele,
                ReferencePosition = indel.ReferencePosition + offset,
                Length = indel.Length,
                Type = indel.Type,
                RefPrefix = refPrefix,
                RefSuffix = refSuffix,
                NumBasesInReferenceSuffixBeforeUnique = numBeforeUnique,
                InMulti = multiIndel != null,
                OtherIndel = multiIndel,
            };
        }
        [Fact]
        public void CanCoexist()
        {
            var operations = new GeminiReadRealigner(new BasicAlignmentComparer());

            var deletion = Map(new CandidateIndel(new CandidateAllele("chr1", 10, "ACG", "A", AlleleCategory.Deletion)));
            var deletion_same = Map(new CandidateIndel(new CandidateAllele("chr1", 10, "ACTT", "A", AlleleCategory.Deletion)));
            var deletion_overlap = Map(new CandidateIndel(new CandidateAllele("chr1", 11, "CTT", "C", AlleleCategory.Deletion)));
            var deletion_overlap2 = Map(new CandidateIndel(new CandidateAllele("chr1", 7, "CTTAA", "C", AlleleCategory.Deletion)));
            var deletion_nonoverlap = Map(new CandidateIndel(new CandidateAllele("chr1", 7, "CTTA", "C", AlleleCategory.Deletion)));
            var insertion = Map(new CandidateIndel(new CandidateAllele("chr1", 10, "A", "AC", AlleleCategory.Insertion)));
            var insertion_same = Map(new CandidateIndel(new CandidateAllele("chr1", 10, "A", "AG", AlleleCategory.Insertion)));
            var insertion2 = Map(new CandidateIndel(new CandidateAllele("chr1", 11, "A", "AC", AlleleCategory.Insertion)));
            var insertion_nonoverlap = Map(new CandidateIndel(new CandidateAllele("chr1", 12, "A", "AC", AlleleCategory.Insertion)));

            // same position
            Assert.False(operations.CanCoexist(deletion, deletion_same));
            Assert.False(operations.CanCoexist(insertion, insertion_same));

            Assert.False(operations.CanCoexist(deletion, insertion)); // Previously true, now we're strict and only allow multis. TODO maybe in the future allow stuff that was far away, for if we stitch reads and they becmoe long enough to tie two together that we previously couldn't have seen together

            // overlapping deletions
            Assert.False(operations.CanCoexist(deletion, deletion_overlap));
            Assert.False(operations.CanCoexist(deletion, deletion_overlap2));
            Assert.False(operations.CanCoexist(deletion, deletion_nonoverlap));// Previously true, now we're strict and only allow multis. TODO maybe in the future allow stuff that was far away, for if we stitch reads and they becmoe long enough to tie two together that we previously couldn't have seen 

            // insertion inside of deletion
            Assert.False(operations.CanCoexist(insertion2, deletion));
            Assert.False(operations.CanCoexist(deletion, insertion2));
            Assert.False(operations.CanCoexist(deletion, insertion_nonoverlap)); // Previously true, now we're strict and only allow multis. TODO maybe in the future allow stuff that was far away, for if we stitch reads and they becmoe long enough to tie two together that we previously couldn't have seen 
        }
    }
}