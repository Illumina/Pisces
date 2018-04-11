using System;
using System.Collections.Generic;
using System.Linq;
using RealignIndels.Logic;
using RealignIndels.Logic.TargetCalling;
using RealignIndels.Models;
using RealignIndels.Utlity;
using Alignment.Domain.Sequencing;
using Hygea.Logic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;


namespace RealignIndels.Tests.UnitTests
{
    public class ReadRealignerTests
    {
        [Fact]
        public void Insertion_Scenarios()
        {   
            // COORD:    1234567890      1234567890
            // WT:       ACGTACGTAC------GTACGTACGTACGTACGTACGTACGTACGT
            // MUTANT:   ACGTACGTACTATATAGTACGTACGTACGTACGTACGTACGTACGT
            var chrReference = string.Join(string.Empty, Enumerable.Repeat("ACGT", 10));

            var indel = new CandidateIndel(new CandidateAllele("chr", 10, "C", "CTATATA", AlleleCategory.Insertion));

            // --------------
            // read fully spanning insertion
            // --------------
            // read anchored on left
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M5I5M"),  // soft clip shouldn't affect anchor
                Bases = "ACGTACGTACTATATAATAC"
            }),
            true, 1, 1, 1, "10M6I4M");
            // ...with remasking, should not see softclip if it all matches
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M5I5M"),  // soft clip shouldn't affect anchor
                Bases = "ACGTACGTACTATATAATAC"
            }),
            true, 1, 1, 1, "10M6I4M", true);
            // ...with remasking, should not see the softclip there even if doesn't match (we remask on Ns Only)
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M5I5M"),  // soft clip shouldn't affect anchor
                Bases = "TACGTACGTATATATAATAC"
            }),
            true, 1, 1, 11, "10M6I4M", true);
            // ...with remasking, should not see the softclip there even if doesn't match (we remask on Ns Only)
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M5I5M"),  // soft clip shouldn't affect anchor
                Bases = "NNCGTACGTATATATAATAC"
            }),
            true, 3, 1, 9, "2S8M6I4M", true);

            // Ns should be untouched
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M5I5M3S"),  // soft clip shouldn't affect anchor
                Bases = "NNCGTACGTATATATAATACNNN"
            }),
            true, 3, 1, 9, "2S8M6I4M3S", true);

            // Ns should be untouched but other softclips broken into
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M5I5M3S"),  // soft clip shouldn't affect anchor
                Bases = "NNCGTACGTATATATAATACTNN"
            }),
            true, 3, 1, 10, "2S8M6I5M2S", true);

            // try again but with different cigar
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 0, // zero based
                CigarData = new CigarAlignment("1M18I5D1M"),
                Bases = "ACGTACGTAATATATAATAC"
            }),
            true, 1, 1, 2, "10M6I4M");

            // with mismatch in inserted sequence -> should not align
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 0, // zero based
                CigarData = new CigarAlignment("1M18I5D1M"),
                Bases = "ACGTACGTACCATATAGTAC"
            }),
            false, 0, 0, 0, null);

            // read anchored on right
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 0, // zero based
                CigarData = new CigarAlignment("20M2S"),  // soft clip shouldn't affect anchor
                Bases = "GTACTATATAGTACGTACGTAC"
            }),
            true, 7, 1, 0, "4M6I12M");

            // with mismatch in inserted sequence -> should not align
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 0, // zero based
                CigarData = new CigarAlignment("20M2S"),  // soft clip shouldn't affect anchor
                Bases = "GTACAATATAGTACGTACGTAC"
            }),
            false, 0, 0, 0, null);
            
            // try again but with different cigar.  
            // doesnt really matter since we clear the original, so long as anchor point is correct
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 20, // zero based
                CigarData = new CigarAlignment("1M20I1M"),
                Bases = "GTACTATATAGTACGTACGTAC"
            }),
            true, 7, 1, 0, "4M6I12M");

            // --------------
            // read partially spanning insertion
            // --------------
            // read anchored on left
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M4D3M"),  // soft clip shouldn't affect anchor
                Bases = "ACGTACGTACTAT"
            }),
            true, 1, 1, 0, "10M3I");
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M4D3M"),  // soft clip shouldn't affect anchor
                Bases = "ACGTACGTACTAT"
            }),
            true, 1, 1, 0, "10M3I", true); 
            // ...with remasking, if not Ns, should see as mismatches
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5S5M4D3M"),
                Bases = "TACGTACGTATAT"
            }),
            true, 1, 1, 10, "10M3I", true);           
            // ...with remasking, should still see softclip there (if Ns)
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
            // read partially spanning insertion, tested with maskPartialInsertion 
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
            true, 1, 0, 0, "10M5S", true, maskPartialInsertion: true);
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
            // ...with remasking and maskPartialInsertion, should see N softclip merged with partial insertion softclip
            TestRead(chrReference, indel, new Read("chr", new BamAlignment
            {
                Position = 5, // zero based
                CigarData = new CigarAlignment("5M4D3M5S"),
                Bases = "CGTACTATNNNNN"
            }),
            true, 6, 0, 0, "5M8S", true, maskPartialInsertion: true);

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
            true, 39, 1, 0, "28I47M");

            TestRead(chrReference, indel, new Read("chr13", new BamAlignment
            {
                Position = 28608247 - refTruePosition, // zero based
                CigarData = new CigarAlignment("10M36I29M"),
                Bases = "CCATTTGAGATCATATTCATAAAGGCTCGGAAACTCCCATTTGAGATCATATTCATATTCTCTGAAATCAACGTA"
            }),
            true, 39, 0, 0, "28S47M", maskPartialInsertion: true);

            TestRead(chrReference, indel, new Read("chr13", new BamAlignment
            {
                Position = 28608240 - refTruePosition, // zero based
                CigarData = new CigarAlignment("17M36I22M"),
                Bases = "GAAACTCCCATTTGAGATCATATTCATAAAGGCTCGGAAACTCCCATTTGAGATCATATTCATATTCTCTGAAAT"
            }),
            true, 39, 1, 0, "35I40M");

            TestRead(chrReference, indel, new Read("chr13", new BamAlignment
            {
                Position = 28608240 - refTruePosition, // zero based
                CigarData = new CigarAlignment("17M36I22M"),
                Bases = "GAAACTCCCATTTGAGATCATATTCATAAAGGCTCGGAAACTCCCATTTGAGATCATATTCATATTCTCTGAAAT"
            }), 
            true, 39, 0, 0, "35S40M", maskPartialInsertion: true);

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

            var deletion = new CandidateIndel(new CandidateAllele("chr", 10, "CTATAT", "C", AlleleCategory.Deletion));
            var insertion = new CandidateIndel(new CandidateAllele("chr", 21, "T", "TCCCCC", AlleleCategory.Insertion));
            var insertion2 = new CandidateIndel(new CandidateAllele("chr", 18, "A", "AGG", AlleleCategory.Insertion));
            var indels = new List<CandidateIndel>() {insertion, deletion, insertion2};
            var indelCandidateGroups = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(deletion.ToString(), insertion.ToString(), null), new Tuple<string, string, string>(deletion.ToString(), insertion2.ToString(), null)};
            var indelCandidateGroups2 = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(deletion.ToString(), insertion.ToString(), null) };

            var tests = new List<RealignmentTest>();

            // --------------
            // Mutant 1
            // --------------
            // anchor on left, one mismatch at edge of insertion
            tests.Add(new RealignmentTest()
            {
                Position = 8,
                Cigar = "18M",
                Sequence = "TACGTACGTCCCCCTCGT",
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
                Sequence = "TACGTACGTCCCCCTCGT",
                ShouldAlign = true,
                NewPosition = 8,
                NewCigar = "3M5D6M5I4M",
                NumIndels = 2,
                NumMismatches = 1
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

            ExecuteTests(tests, indels, chrReference, candidateGroups: indelCandidateGroups);

            // test MaskPartialInsertion
            var testsMaskPartialInsertion = new List<RealignmentTest>();
            testsMaskPartialInsertion.Add(new RealignmentTest()
            {
                Position = 8,
                Cigar = "13M",
                Sequence = "TACGTACGTCCCC",
                ShouldAlign = true,
                NewPosition = 8,
                NewCigar = "3M5D6M4S",
                NumIndels = 1,
                NumMismatches = 0
            });
            ExecuteTests(testsMaskPartialInsertion, indels, chrReference, candidateGroups: indelCandidateGroups, maskPartialInsertion: true);

            // when two indels don't coexist
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
            ExecuteTests(testNotCoexist, indels, chrReference); // no combination of >1 indel seen
            ExecuteTests(testNotCoexist, indels, chrReference, candidateGroups: indelCandidateGroups2); //wrong combination of >1 indel

            // original order of indels shouldnt really matter
            indels.Reverse();
            ExecuteTests(tests, indels, chrReference, candidateGroups: indelCandidateGroups);
            ExecuteTests(testsMaskPartialInsertion, indels, chrReference, candidateGroups: indelCandidateGroups, maskPartialInsertion: true);
            ExecuteTests(testNotCoexist, indels, chrReference);
            ExecuteTests(testNotCoexist, indels, chrReference, candidateGroups: indelCandidateGroups2);

        }

        [Fact]
        public void TwoIndel_InsPlusDel_SamePosition_Scenarios()
        {
            // real world case from R2 FDA FDA-07-var2_S2.bam
            var chrReference = "GTCGCTATCAAGGAATTAAGAGAAGCAACATCTCCGAAAGCCAACAAGGAAATCCTCGATGTGAGTTTCTGCTTTGCTGTGTGGGGGTCCATGGCTCT";
            
            var insertion = new CandidateIndel(new CandidateAllele("chr", 12, "G", "GTTGCT", AlleleCategory.Insertion));
            var deletion = new CandidateIndel(new CandidateAllele("chr", 12, "GGAATTAAGAGAAGCAACATC", "G", AlleleCategory.Deletion));
            var indelPair = new List<CandidateIndel> { insertion, deletion }.OrderBy(g => g.ReferencePosition).ThenBy(t => t.ReferenceAllele).Select(x => x.ToString()).ToList();
            var indelCandidateGroups = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(indelPair[0], indelPair[1], null) };


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

            ExecuteTests(tests, new List<CandidateIndel> { deletion, insertion}, chrReference, candidateGroups: indelCandidateGroups);          
        }

        [Fact]
        public void TwoIndel_InsPlusDel_SamePositionCancelOut_Scenarios()
        {
            var chrReference = "ATCGATGCTAX";

            var insertion = new CandidateIndel(new CandidateAllele("chr", 5, "A", "AT", AlleleCategory.Insertion));
            var deletion = new CandidateIndel(new CandidateAllele("chr", 5, "AT", "A", AlleleCategory.Deletion));
            var indelCandidateGroups = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(insertion.ToString(), deletion.ToString(), null) };

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


            ExecuteTests(tests, new List<CandidateIndel> { deletion, insertion }, chrReference, candidateGroups: indelCandidateGroups);
        }

        [Fact]
        public void TwoIndel_InsPlusIns_Scenarios()
        {
            // COORD:     1234567890123456----7-----890123456789012345678901
            // WT:        ACGTACGTACTATATG----T-----ACGTACGTACGTACGTACGTACGT
            // MUTANT1:   ACGTACGTACTATATGAAAATCCCCCACGTACGTACGTACGTACGTACGT
            var chrReference = "ACGTACGTACTATATGTACGTACGTACGTACGTACGTACGT";

            var insertion = new CandidateIndel(new CandidateAllele("chr", 16, "G", "GAAAA", AlleleCategory.Insertion));
            var insertion2 = new CandidateIndel(new CandidateAllele("chr", 17, "T", "TCCCCC", AlleleCategory.Insertion));
            var indels = new List<CandidateIndel>() { insertion, insertion2 };
            var indelCandidateGroups = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(insertion.ToString(), insertion2.ToString(), null) };

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

            ExecuteTests(tests, indels, chrReference, candidateGroups: indelCandidateGroups);

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
                NumMismatches = 0
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
                NumMismatches = 0
            });
            ExecuteTests(testsMaskPartialInsertion, indels, chrReference, candidateGroups: indelCandidateGroups, maskPartialInsertion: true);

            // original order of indels shouldnt really matter
            indels.Reverse();
            ExecuteTests(tests, indels, chrReference, candidateGroups: indelCandidateGroups);
            ExecuteTests(testsMaskPartialInsertion, indels, chrReference, candidateGroups: indelCandidateGroups, maskPartialInsertion: true);

        }

        [Fact]
        public void TwoIndel_DelPlusDel_Scenarios()
        {
            // COORD:     12345678901234567890123456789012345678901
            // WT     :   ACGTACGTACTATATGAAAATCCCCCACGTACGTACGTACG
            // MUTANT1:   ACGTACGTACTATATG----T-----ACGTACGTACGTACG
            var chrReference = "ACGTACGTACTATATGAAAATCCCCCACGTACGTACGTACG";

            var deletion = new CandidateIndel(new CandidateAllele("chr", 16, "GAAAA", "G", AlleleCategory.Deletion));
            var deletion2 = new CandidateIndel(new CandidateAllele("chr", 21, "TCCCCC", "T", AlleleCategory.Deletion));
            var indels = new List<CandidateIndel>() { deletion2, deletion };
            var indelCandidateGroups = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(deletion.ToString(), deletion2.ToString(), null) };

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

            ExecuteTests(tests, indels, chrReference, candidateGroups: indelCandidateGroups);

            // original order of indels shouldnt really matter
            indels.Reverse();
            ExecuteTests(tests, indels, chrReference, candidateGroups: indelCandidateGroups);
        }

        [Fact]
        public void ThreeIndels()
        {
            // COORD:     123456789012345---67890123456789012345678901
            // WT     :   ACGTACGTACTATAT---GAAAATCCCCCACGTACGTACGTACG
            // MUTANT1:   ACGTACGTACTATATGGGG----T-----ACGTACGTACGTACG
            var chrReference = "ACGTACGTACTATATGAAAATCCCCCACGTACGTACGTACG";

            var insertion = new CandidateIndel(new CandidateAllele("chr", 15, "T", "TGGG", AlleleCategory.Insertion));
            var insertion2 = new CandidateIndel(new CandidateAllele("chr", 15, "T", "TACT", AlleleCategory.Insertion)); // random insertion at same position
            var deletion = new CandidateIndel(new CandidateAllele("chr", 16, "GAAAA", "G", AlleleCategory.Deletion));
            var deletion2 = new CandidateIndel(new CandidateAllele("chr", 21, "TCCCCC", "T", AlleleCategory.Deletion));
            var indels = new List<CandidateIndel>() { deletion2, insertion2, deletion, insertion };
            var indelCandidateGroups = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(insertion.ToString(), deletion.ToString(), deletion2.ToString()), new Tuple<string, string, string>(insertion2.ToString(), deletion.ToString(), deletion2.ToString()) };
            var indelCandidateGroups2 = new HashSet<Tuple<string, string, string>>() { new Tuple<string, string, string>(insertion.ToString(), deletion.ToString(), null) };


            var tests = new List<RealignmentTest>();

            // all from left
            tests.Add(new RealignmentTest()
            {
                Position = 13,
                Cigar = "10M",
                Sequence = "AATGGGGTAC",
                ShouldAlign = true,
                NewPosition = 13,
                NewCigar = "3M3I1M4D1M5D2M",
                NumIndels = 3,
                NumMismatches = 1
            });

            // all from right
            tests.Add(new RealignmentTest()
            {
                Position = 19,
                Cigar = "10M",
                Sequence = "TATGGGGTAC",
                ShouldAlign = true,
                NewPosition = 13,
                NewCigar = "3M3I1M4D1M5D2M",
                NumIndels = 3,
                NumMismatches = 0
            });

            ExecuteTests(tests, indels, chrReference, candidateGroups: indelCandidateGroups);

            // read has three indels, but no combination of >1 indel has been seen -> only realign with one indel
            var testNotCoexist = new List<RealignmentTest>();
            testNotCoexist.Add(new RealignmentTest()
            {
                Position = 13,
                Cigar = "10M",
                Sequence = "AATGGGGTAC",
                ShouldAlign = true,
                NewPosition = 13,
                NewCigar = "3M3I4M",
                NumIndels = 1,
                NumMismatches = 3
            });
            ExecuteTests(testNotCoexist, indels, chrReference);

            // read has three indels, but only the first two indels haven been seen coexisting -> realign with the first two indels
            var testFirstPair = new List<RealignmentTest>();
            testFirstPair.Add(new RealignmentTest()
            {
                Position = 13,
                Cigar = "10M",
                Sequence = "AATGGGGTAC",
                ShouldAlign = true,
                NewPosition = 13,
                NewCigar = "3M3I1M4D3M",
                NumIndels = 2,
                NumMismatches = 2
            });
            ExecuteTests(testFirstPair, indels, chrReference, candidateGroups: indelCandidateGroups2);

            // there are 2 of the 3 grouped indels in the read, but they are the first and third of the group -> does not count as coexisting
            var testWrongPair = new List<RealignmentTest>();
            testWrongPair.Add(new RealignmentTest()
            {
                Position = 16,
                Cigar = "6S8M",
                Sequence = "TATGGGGAAAATAC",
                ShouldAlign = true,
                NewPosition = 13,
                NewCigar = "3M3I8M",
                NumIndels = 1,
                NumMismatches = 1
            });
            ExecuteTests(testWrongPair, indels, chrReference, candidateGroups: indelCandidateGroups);

            // original order of indels shouldnt really matter
            indels.Reverse();
            ExecuteTests(tests, indels, chrReference, candidateGroups: indelCandidateGroups);
            ExecuteTests(testNotCoexist, indels, chrReference);
            ExecuteTests(testFirstPair, indels, chrReference, candidateGroups: indelCandidateGroups2);
            ExecuteTests(testWrongPair, indels, chrReference, candidateGroups: indelCandidateGroups);

        }

        private void TestRead(string refSequence, CandidateIndel indel, Read read, bool shouldAlign, int position,
            int numIndels, int numMismatches, string cigar,  bool remaskSoftclip = false, HashSet<Tuple<string, string, string>> candidateGroups = null, bool maskPartialInsertion = false)
        {
            TestRead(refSequence, new List<CandidateIndel>() { indel }, read, shouldAlign, position, numIndels, numMismatches, cigar,  remaskSoftclip, candidateGroups, maskPartialInsertion);
        }

        private void TestRead(string refSequence, List<CandidateIndel> indels, Read read, bool shouldAlign, int position, int numIndels, int numMismatches,
            string cigar,  bool remaskSoftclip = false, HashSet<Tuple<string, string, string>> candidateGroups = null, bool maskPartialInsertion = false)
        {
            var result = new ReadRealigner(new BasicAlignmentComparer(), true, remaskSoftclip, maskPartialInsertion).Realign(read, indels, refSequence, new IndelRanker(), indelCandidateGroups:candidateGroups);
            
            if (shouldAlign)
            {
                VerifyResult(result, position, numIndels, numMismatches, cigar);
            }
            else {
                Assert.Equal(null, result);
            }
        }

        private void ExecuteTests(List<RealignmentTest> tests, List<CandidateIndel> indels, string chrReference, HashSet<Tuple<string, string, string>> candidateGroups = null, bool maskPartialInsertion = false)
        {
            foreach (var test in tests)
            {
                TestRead(chrReference, indels, new Read("chr", new BamAlignment
                {
                    Position = test.Position - 1, 
                    CigarData = new CigarAlignment(test.Cigar),
                    Bases = test.Sequence
                }),
            test.ShouldAlign, test.NewPosition, test.NumIndels, test.NumMismatches, test.NewCigar, candidateGroups: candidateGroups, maskPartialInsertion : maskPartialInsertion);
            }
        }

        private void VerifyResult(RealignmentResult result, int position, int numIndels, int numMismatches, string cigar)
        {
            Assert.NotNull(result);
            Assert.Equal(position, result.Position);
            Assert.Equal(numIndels, result.NumIndels);
            Assert.Equal(numMismatches, result.NumMismatches);
            Assert.Equal(cigar, result.Cigar.ToString());
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
        }

        [Fact]
        public void Compare()
        {
            var readRealigner = new ReadRealigner(new BasicAlignmentComparer());

            var deletion = new CandidateIndel(new CandidateAllele("chr1", 10, "AC", "A", AlleleCategory.Deletion));
            var deletion2 = new CandidateIndel(new CandidateAllele("chr1", 11, "AC", "A", AlleleCategory.Deletion));
            var insertion = new CandidateIndel(new CandidateAllele("chr1", 10, "A", "AC", AlleleCategory.Insertion));
            var insertion2 = new CandidateIndel(new CandidateAllele("chr1", 11, "A", "AC", AlleleCategory.Insertion));

            var all = new List<CandidateIndel> {insertion2, deletion2, deletion, insertion};
            all.Sort(readRealigner.Compare);

            Assert.Equal(insertion, all[0]);
            Assert.Equal(deletion, all[1]);
            Assert.Equal(insertion2, all[2]);
            Assert.Equal(deletion2, all[3]);
        }

        [Fact]
        public void CanCoexist()
        {
            var operations = new ReadRealigner(new BasicAlignmentComparer());

            var deletion = new CandidateIndel(new CandidateAllele("chr1", 10, "ACG", "A", AlleleCategory.Deletion));
            var deletion_same = new CandidateIndel(new CandidateAllele("chr1", 10, "ACTT", "A", AlleleCategory.Deletion));
            var deletion_overlap = new CandidateIndel(new CandidateAllele("chr1", 11, "CTT", "C", AlleleCategory.Deletion));
            var deletion_overlap2 = new CandidateIndel(new CandidateAllele("chr1", 7, "CTTAA", "C", AlleleCategory.Deletion));
            var deletion_nonoverlap = new CandidateIndel(new CandidateAllele("chr1", 7, "CTTA", "C", AlleleCategory.Deletion));
            var insertion = new CandidateIndel(new CandidateAllele("chr1", 10, "A", "AC", AlleleCategory.Insertion));
            var insertion_same = new CandidateIndel(new CandidateAllele("chr1", 10, "A", "AG", AlleleCategory.Insertion));
            var insertion2 = new CandidateIndel(new CandidateAllele("chr1", 11, "A", "AC", AlleleCategory.Insertion));
            var insertion_nonoverlap = new CandidateIndel(new CandidateAllele("chr1", 12, "A", "AC", AlleleCategory.Insertion));

            // same position
            Assert.False(operations.CanCoexist(deletion, deletion_same));
            Assert.False(operations.CanCoexist(insertion, insertion_same));

            Assert.True(operations.CanCoexist(deletion, insertion));

            // overlapping deletions
            Assert.False(operations.CanCoexist(deletion, deletion_overlap));
            Assert.False(operations.CanCoexist(deletion, deletion_overlap2));
            Assert.True(operations.CanCoexist(deletion, deletion_nonoverlap));

            // insertion inside of deletion
            Assert.False(operations.CanCoexist(insertion2, deletion));
            Assert.False(operations.CanCoexist(deletion, insertion2));
            Assert.True(operations.CanCoexist(deletion, insertion_nonoverlap));
        }
    }
}
