using System;
using System.Collections.Generic;
using Pisces.Domain.Options;
using Alignment.Domain.Sequencing;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using VariantPhasing.Types;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class VeadFinderTests
    {
        [Fact]
        public void FindVariantMNVResults()
        {
            var read = new BamAlignment();
            read.Bases = "AA" + "ACGTACGT" + "GGGG";
            //vcf coords  12-345678910-11,12,13,14
            read.CigarData = new CigarAlignment("2S8M4S");
            read.Position = 3 - 1;
            read.Qualities = new byte[read.Bases.Length];

            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 4;
            vs1.VcfReferenceAllele = "TA";
            vs1.VcfAlternateAllele = "CG"; //read should match ALT for this test

            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 10;
            vs2.VcfReferenceAllele = "TTT";
            vs2.VcfAlternateAllele = "T";

            var vsFromVcf = new List<VariantSite>() { vs1, vs2 };

            //given a variant site, is it in the read?

            ExecuteTest(read, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 4);
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "TA");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "CG");

                Assert.Equal(matchedVariants[1].VcfReferencePosition, 10);  //a deletion not supported by the reads
                Assert.Equal(matchedVariants[1].VcfReferenceAllele, "T");  //to we just return T>T, a reference call at this loci.
                Assert.Equal(matchedVariants[1].VcfAlternateAllele, "T");

            });
        }

        [Fact]
        /// <summary>
        /// in this test, all the bases fail quality
        /// </summary>
        public void FindCompetingNResults()
        {
            var read = new BamAlignment();
            read.Bases = "AA" + "ACGTACGT" + "GGGG";
            //vcf coords  12-345678910-11,12,13,14
            read.CigarData = new CigarAlignment("2S8M4S");
            read.Position = 3 - 1;
            read.Qualities = new byte[read.Bases.Length];

            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 4;
            vs1.VcfReferenceAllele = "TA";
            vs1.VcfAlternateAllele = "CG"; //read should match ALT for this test

            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 4;
            vs2.VcfReferenceAllele = "TARR";
            vs2.VcfAlternateAllele = "CGTA";

            var vs3 = new VariantSite();
            vs3.VcfReferencePosition = 4;
            vs3.VcfReferenceAllele = "T";
            vs3.VcfAlternateAllele = "T";

            var vs4 = new VariantSite();
            vs4.VcfReferencePosition = 4;
            vs4.VcfReferenceAllele = "TA";
            vs4.VcfAlternateAllele = "T";

            var vs5 = new VariantSite();
            vs5.VcfReferencePosition = 4;
            vs5.VcfReferenceAllele = "T";
            vs5.VcfAlternateAllele = "TAAA";

            var vsFromVcf = new List<VariantSite>() { vs1, vs2, vs3, vs4, vs5 };
            vsFromVcf.Sort();

            //given a variant site, is it in the read? none of these bases should pass

            //in this test "foundVariants" are the ones we found in the input vcf, and "matched varaints" 
            //are the ones that we found in the the read.
            //IE, we have 4 passing varaints in the input vcf. We want to know which of them are supported by a given read.
            //Those 4 input variants represent 4 separate queries to the read. Each query has an answer (a variant site vs)
            //that might be reference, indeterminate, or the variant the algorithm was looking for.
            //N means the query was indeterminate. 


            ExecuteTest(read, 10, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 4);
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "N");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "N");

                Assert.Equal(matchedVariants[1].VcfReferencePosition, 4);  //a MNV not supported by the reads
                Assert.Equal(matchedVariants[1].VcfReferenceAllele, "N");
                Assert.Equal(matchedVariants[1].VcfAlternateAllele, "N");

                Assert.Equal(matchedVariants[2].VcfReferencePosition, 4);  //a reference not supported by the reads
                Assert.Equal(matchedVariants[2].VcfReferenceAllele, "N");
                Assert.Equal(matchedVariants[2].VcfAlternateAllele, "N");

                //this, below, would fail prior to PICS-837 bugfix

                Assert.Equal(matchedVariants[3].VcfReferencePosition, 4);  //an insertion not supported by the reads
                Assert.Equal(matchedVariants[3].VcfReferenceAllele, "N");
                Assert.Equal(matchedVariants[3].VcfAlternateAllele, "N");

                Assert.Equal(matchedVariants[4].VcfReferencePosition, 4);  //a deletion not supported by the reads
                Assert.Equal(matchedVariants[4].VcfReferenceAllele, "N");
                Assert.Equal(matchedVariants[4].VcfAlternateAllele, "N");

            });
        }


        [Fact]
        /// <summary>
        /// in this test, the read fits two MNVs at the same position, and we need to find both.
        /// </summary>
        public void FindCompetingDisAgreeingMNVResults()
        {
            var read = new BamAlignment();
            read.Bases = "AA" + "ACGTACGT" + "GGGG";
            //vcf coords  12-345678910-11,12,13,14
            read.CigarData = new CigarAlignment("2S8M4S");
            read.Position = 3 - 1;
            read.Qualities = new byte[read.Bases.Length];

            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 4;
            vs1.VcfReferenceAllele = "TA";
            vs1.VcfAlternateAllele = "CG";

            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 4;
            vs2.VcfReferenceAllele = "TARR";
            vs2.VcfAlternateAllele = "CCTA";//doesnt match

            var vsFromVcf = new List<VariantSite>() { vs1, vs2 };
            vsFromVcf.Sort();

            //given a variant site, is it in the read?

            ExecuteTest(read, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 4);
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "TA");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "CG");

                Assert.Equal(matchedVariants[1].VcfReferencePosition, 4);  //this variant not supported by the reads
                Assert.Equal(matchedVariants[1].VcfReferenceAllele, "X");  //to we just return X>X, a reference call at this loci.
                Assert.Equal(matchedVariants[1].VcfAlternateAllele, "X");

            });
        }




        [Fact]
        /// <summary>
        /// in this test, the read fits two MNVs at the same position, and we need to find both.
        /// </summary>
        public void FindColocatedAgreeingMNVResults()
        {
            var read = new BamAlignment();
            read.Bases = "AA" + "ACGTACGT" + "GGGG";
            //vcf coords  12-345678910-11,12,13,14
            read.CigarData = new CigarAlignment("2S8M4S");
            read.Position = 3 - 1;
            read.Qualities = new byte[read.Bases.Length];

            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 4;
            vs1.VcfReferenceAllele = "TA";
            vs1.VcfAlternateAllele = "CG";

            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 4;
            vs2.VcfReferenceAllele = "TARR";
            vs2.VcfAlternateAllele = "CGTA";//read should match ALT for this test

            var vsFromVcf = new List<VariantSite>() { vs1, vs2 };
            vsFromVcf.Sort();

            //given a variant site, is it in the read?

            ExecuteTest(read, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 4);  //this should find both
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "TA");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "CG");

                Assert.Equal(matchedVariants[1].VcfReferencePosition, 4);
                Assert.Equal(matchedVariants[1].VcfReferenceAllele, "TARR");
                Assert.Equal(matchedVariants[1].VcfAlternateAllele, "CGTA");

            });
        }


        [Fact]
        /// <summary>
        /// in this test, the read fits two MNVs at slightly different spots, and we need to find both.
        /// </summary>
        public void FindAgreeingOverlappingMNVResults()
        {
            var read = new BamAlignment();
            read.Bases = "AA" + "ACGTACGT" + "GGGG";
            //vcf coords  12-345678910-11,12,13,14
            read.CigarData = new CigarAlignment("2S8M4S");
            read.Position = 3 - 1;
            read.Qualities = new byte[read.Bases.Length];

            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 4;
            vs1.VcfReferenceAllele = "TAAC";
            vs1.VcfAlternateAllele = "CGTA";

            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 6;
            vs2.VcfReferenceAllele = "ACCC";
            vs2.VcfAlternateAllele = "TACG";//read should match ALT for this test

            var vsFromVcf = new List<VariantSite>() { vs1, vs2 };
            vsFromVcf.Sort();

            //given a variant site, is it in the read?

            ExecuteTest(read, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 4);
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "TAAC");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "CGTA");

                Assert.Equal(matchedVariants[1].VcfReferencePosition, 6);  //this variant not supported by the reads
                Assert.Equal(matchedVariants[1].VcfReferenceAllele, "ACCC");  //to we just return X>X, a reference call at this loci.
                Assert.Equal(matchedVariants[1].VcfAlternateAllele, "TACG");

            });
        }


        [Fact]
        /// <summary>
        /// in this test, the read fits two MNVs, and we need to find both.
        /// </summary>
        public void FindMultipleMNVResults()
        {
            var read = new BamAlignment();
            read.Bases = "AA" + "ACGTACGT" + "GGGG";
            //vcf coords  12-345678910-11,12,13,14
            read.CigarData = new CigarAlignment("2S8M4S");
            read.Position = 3 - 1;
            read.Qualities = new byte[read.Bases.Length];

            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 4;
            vs1.VcfReferenceAllele = "TA";
            vs1.VcfAlternateAllele = "CG"; //read should match ALT for this test

            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 7;
            vs2.VcfReferenceAllele = "GG";
            vs2.VcfAlternateAllele = "AC";

            var vsFromVcf = new List<VariantSite>() { vs1, vs2 };
            vsFromVcf.Sort();

            //given a variant site, is it in the read?

            ExecuteTest(read, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 4);
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "TA");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "CG");

                Assert.Equal(matchedVariants[1].VcfReferencePosition, 7);  //a deletion not supported by the reads
                Assert.Equal(matchedVariants[1].VcfReferenceAllele, "GG");  //to we just return T>T, a reference call at this loci.
                Assert.Equal(matchedVariants[1].VcfAlternateAllele, "AC");

            });
        }



        [Fact]
        public void ProcessWithDeletionsReadTest()
        {
            //variants with deletions, from vcf
            //5	chr4	1389296	TCACA	T	5-0
            //5	chr4	1389304	A	G	5-1
            //5	chr4	1389353	C	T	5-2

            //example reads
            //NS500522:22:HCL55BGXX:3:12604:11604:9688_fwd	1389291	5M4D65M6S	CTGCTCACGTGCCGATGTGGAGTGCCCGCCTGCTCACACCAGCCCATGTGTAGTGCCCGCCTGCTCACACCAGGCC
            //NS500522:22:HCL55BGXX:2:22111:2006:10696_fwd	1389304	27S49M	AGTGCAGTGGGCTGCTCTTCACAGAGGTGCCGATGTGGAGTGCCCGCCTGCTCACACGTGCCCATGTGGAGTGCCT
            //NS500522:22:HCL55BGXX:2:12204:16794:7701_fwd	1389305	12S28M36S	GCCTGCTCACGGGCCGATGTGGGGTGCCCGCCTGCTCACAGTACCCGCCGGGGGGGGGCGGCCTGCGCTCTCCAGG
            //NS500522:22:HCL55BGXX:3:11506:19160:15945_fwd	1389309	32S44M	GCTGGAGTCGGCGCCTGCTGACAGAGGTGCCAATGTGGAGGGCCCGCCTGCTCACACGTGCCCATGTGGAGTGCCT
            //NS500522:22:HCL55BGXX:1:21301:10073:20227_rev	1389311	47M2I26M	GTGTAGTGCCAGCCTGCTCACACGTGACCATGTGTTGTGCCTGCCTGCTCTCACACGTGCCCATGTGGAGTGCCC
            //NS500522:22:HCL55BGXX:2:11108:11614:15003_rev	1389311	47M2I26M	GTGTAGTGCCCGCCTGCTCTCACGTGCCCATGTGGTGTGCCCGCCTGCTCTCACACGTGCCCATGTGGAGTGCCC

            //findings
            //NS500522:22:HCL55BGXX:3:12604:11604:9688_fwd: T>T A>G C>C 
            //NS500522:22:HCL55BGXX:2:22111:2006:10696_fwd: N>N A>A C>T 
            //NS500522:22:HCL55BGXX:3:11506:19160:15945_fwd: N>N N>N C>T 
            //NS500522:22:HCL55BGXX:1:21301:10073:20227_rev: N>N N>N C>C 


            //actual alignment

            //vcf coord          292  v1@296    v2@304           313         323         333            343         v3@353 
            //r1                 C TGCTD DDDCA CG   TGC    CGATG TGGAG TGCCC GCCTG CTCAC A   CCAG CCCAT GTGTA GTGCC CGCCT G       CT CACACCAGGCC
            //r2 AGTGC AGTGG GCTGC TCTTC ACAGA GG(S)TGC    CGATG TGGAG TGCCC GCCTG CTCAC A   CGTG CCCAT GTGGA GTGCC T
            //r3                    GCCT GCTCA CGG(S)GC    CGATG TGGGG TGCCC GCCTG CTCAC A(S)GTAC CCGCC GGGGG GGGGC GGCCT G       CG CTCTCCAGG
            //r4 GCTGG AGTCG GCGCC TGCTG ACAGA GGT   GC CA(S)ATG TGGAG GGCCC GCCTG CTCAC A   CGTG CCCAT GTGGA GTGCC T
            //r5                                               G TGTAG TGCCA GCCTG CTCAC A   CGTG ACCAT GTGTT GTGCC TGCCT G (I)CT CT CACACGTGCCCATGTGGAGTGCCC
            //r6                                               G TGTAG TGCCC GCCTG CTCTC A   CGTG CCCAT GTGGT GTGCC CGCCT G (I)CT CT CACACGTGCCCATGTGGAGTGCCC

            var r1 = new BamAlignment();
            r1.Bases = "CTGCTCACGTGCCGATGTGGAGTGCCCGCCTGCTCACACCAGCCCATGTGTAGTGCCCGCCTGCTCACACCAGGCC";
            r1.Qualities = new byte[r1.Bases.Length];
            r1.CigarData = new CigarAlignment("5M4D65M6S");
            r1.Position = 1389291;

            var r2 = new BamAlignment();
            r2.Bases = "AGTGCAGTGGGCTGCTCTTCACAGAGGTGCCGATGTGGAGTGCCCGCCTGCTCACACGTGCCCATGTGGAGTGCCT";
            r2.Qualities = new byte[r2.Bases.Length];
            r2.CigarData = new CigarAlignment("27S49M");
            r2.Position = 1389304;

            var r3 = new BamAlignment();
            r3.Bases = "GCCTGCTCACGGGCCGATGTGGGGTGCCCGCCTGCTCACAGTACCCGCCGGGGGGGGGCGGCCTGCGCTCTCCAGG";
            r3.Qualities = new byte[r3.Bases.Length];
            r3.CigarData = new CigarAlignment("12S28M36S");
            r3.Position = 1389305;

            var r4 = new BamAlignment();
            r4.Bases = "GCTGGAGTCGGCGCCTGCTGACAGAGGTGCCAATGTGGAGGGCCCGCCTGCTCACACGTGCCCATGTGGAGTGCCT";
            r4.Qualities = new byte[r4.Bases.Length];
            r4.CigarData = new CigarAlignment("32S44M");
            r4.Position = 1389309;

            var r5 = new BamAlignment();
            r5.Bases = "GTGTAGTGCCAGCCTGCTCACACGTGACCATGTGTTGTGCCTGCCTGCTCTCACACGTGCCCATGTGGAGTGCCC";
            r5.Qualities = new byte[r5.Bases.Length];
            r5.CigarData = new CigarAlignment("47M2I26M");
            r5.Position = 1389311;

            var r6 = new BamAlignment();
            r6.Bases = "GTGTAGTGCCCGCCTGCTCTCACGTGCCCATGTGGTGTGCCCGCCTGCTCTCACACGTGCCCATGTGGAGTGCCC";
            r6.Qualities = new byte[r6.Bases.Length];
            r6.CigarData = new CigarAlignment("47M2I26M");
            r6.Position = 1389311;

            var reads = new List<BamAlignment>() { r1, r2, r3, r4, r5, r6 };

            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 1389296;
            vs1.VcfReferenceAllele = "TCACA";
            vs1.VcfAlternateAllele = "T";

            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 1389304;
            vs2.VcfReferenceAllele = "A";
            vs2.VcfAlternateAllele = "G";

            var vs3 = new VariantSite();
            vs3.VcfReferencePosition = 1389353;
            vs3.VcfReferenceAllele = "C";
            vs3.VcfAlternateAllele = "T";

            var vsFromVcf = new List<VariantSite>() { vs1, vs2, vs3 };

            //test r1

            ExecuteTest(r1, 0, vsFromVcf, (fv) =>
            {
                Assert.Equal(fv[SubsequenceType.MatchOrMismatchSequence].Count, 2);
                Assert.Equal(fv[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(fv[SubsequenceType.DeletionSequence].Count, 1);
            },
            (mv) =>
            {
                Assert.Equal(mv[0].VcfReferencePosition, 1389296);
                Assert.Equal(mv[0].VcfReferenceAllele, "TCACA");
                Assert.Equal(mv[0].VcfAlternateAllele, "T");

                Assert.Equal(mv[1].VcfReferencePosition, 1389304);
                Assert.Equal(mv[1].VcfReferenceAllele, "A");
                Assert.Equal(mv[1].VcfAlternateAllele, "G");

                Assert.Equal(mv[2].VcfReferencePosition, 1389353);
                Assert.Equal(mv[2].VcfReferenceAllele, "C");
                Assert.Equal(mv[2].VcfAlternateAllele, "C");
            });


            //test r1 with base call filter

            ExecuteTest(r1, 10, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 2);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 1);
            },
                (matchedVariants) =>
                {
                    Assert.Equal(matchedVariants[0].VcfReferencePosition, 1389296);
                    Assert.Equal(matchedVariants[0].VcfReferenceAllele, "N");
                    Assert.Equal(matchedVariants[0].VcfAlternateAllele, "N");

                    Assert.Equal(matchedVariants[1].VcfReferencePosition, 1389304);
                    Assert.Equal(matchedVariants[1].VcfReferenceAllele, "N");
                    Assert.Equal(matchedVariants[1].VcfAlternateAllele, "N");

                    Assert.Equal(matchedVariants[2].VcfReferencePosition, 1389353);
                    Assert.Equal(matchedVariants[2].VcfReferenceAllele, "N");
                    Assert.Equal(matchedVariants[2].VcfAlternateAllele, "N");
                }
            );

            //test r2

            ExecuteTest(r2, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 1389296);
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "N");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "N");

                Assert.Equal(matchedVariants[1].VcfReferencePosition, 1389304);
                Assert.Equal(matchedVariants[1].VcfReferenceAllele, "N");
                Assert.Equal(matchedVariants[1].VcfAlternateAllele, "N");

                Assert.Equal(matchedVariants[2].VcfReferencePosition, 1389353);
                Assert.Equal(matchedVariants[2].VcfReferenceAllele, "C");
                Assert.Equal(matchedVariants[2].VcfAlternateAllele, "T");

                //only one variant was found. this read is useless for clustering.           
                //Assert.Equal(matchedVariants, null);

            });

            //test r3

            ExecuteTest(r3, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);
            },
                (matchedVariants) =>
                {
                    //all the positions of interest are softclipped. this read is useless.           
                    Assert.Equal(matchedVariants, null);
                });

            //test r4

            ExecuteTest(r4, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);
            },
                (matchedVariants) =>
                {
                    Assert.Equal(matchedVariants[0].VcfReferencePosition, 1389296);
                    Assert.Equal(matchedVariants[0].VcfReferenceAllele, "N");
                    Assert.Equal(matchedVariants[0].VcfAlternateAllele, "N");

                    Assert.Equal(matchedVariants[1].VcfReferencePosition, 1389304);
                    Assert.Equal(matchedVariants[1].VcfReferenceAllele, "N");
                    Assert.Equal(matchedVariants[1].VcfAlternateAllele, "N");

                    Assert.Equal(matchedVariants[2].VcfReferencePosition, 1389353);
                    Assert.Equal(matchedVariants[2].VcfReferenceAllele, "C");
                    Assert.Equal(matchedVariants[2].VcfAlternateAllele, "T");

                });

            //test r5

            ExecuteTest(r5, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 2);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);

            },
                (matchedVariants) =>
                {
                    Assert.Equal(matchedVariants[0].VcfReferencePosition, 1389296);
                    Assert.Equal(matchedVariants[0].VcfReferenceAllele, "N");
                    Assert.Equal(matchedVariants[0].VcfAlternateAllele, "N");

                    Assert.Equal(matchedVariants[1].VcfReferencePosition, 1389304);
                    Assert.Equal(matchedVariants[1].VcfReferenceAllele, "N");
                    Assert.Equal(matchedVariants[1].VcfAlternateAllele, "N");

                    Assert.Equal(matchedVariants[2].VcfReferencePosition, 1389353);
                    Assert.Equal(matchedVariants[2].VcfReferenceAllele, "C");
                    Assert.Equal(matchedVariants[2].VcfAlternateAllele, "T");

                });

            //test r6

            ExecuteTest(r6, 0, vsFromVcf, (fv) =>
            {
                Assert.Equal(fv[SubsequenceType.MatchOrMismatchSequence].Count, 2);
                Assert.Equal(fv[SubsequenceType.InsertionSquence].Count, 1);
                Assert.Equal(fv[SubsequenceType.DeletionSequence].Count, 0);
            },
                (mv) =>
                {
                    Assert.Equal(mv[2].VcfReferenceAllele, "C");
                    Assert.Equal(mv[2].VcfAlternateAllele, "C");
                });

        }

        [Fact]
        public void ProcessOneDeletionReadTest()
        {
            //reads with deletions, S102
            //       16187-121416587:COSM21479:GCCAGCTGCAGACGGAGCTC:GT:chr12:121416607-121417007-1014/2_rev_121416520	121416520	75M	AGGCGGCTAGCGTGGTGGACCCGGGCCGCGTGGCCCTGTGGCAGCCGAGCCATGGTTTCTAAACTGAGCCAGCTG
            //16187-121416587:COSM21479:GCCAGCTGCAGACGGAGCTC:GT:chr12:121416607-121417007-1484/2_fwd_121416520	121416520	68M18D7M	AGGCGGCTAGCGTGGTGGACCCGGGCCGCGTGGCCCTGTGGCAGCCGAGCCATGGTTTCTAAACTGAGTCTGGCG
            //16187-121416587:COSM21479:GCCAGCTGCAGACGGAGCTC:GT:chr12:121416607-121417007-1320/2_rev_121416520	121416520	68M18D7M	AGGCGGCTAGCGTGGTGGACCCGGGCCGCGTGGCCCTGTGGCAGCCGAGCCATGGTTTCTAAACTGAGTCTGGCG
            //16187-121416587:COSM21479:GCCAGCTGCAGACGGAGCTC:GT:chr12:121416607-121417007-1076/2_rev_121416520	121416520	68M18D7M	AGGCGGCTAGCGTGGTGGACCCGGGCCGCGTGGCCCTGTGGCAGCCGAGCCATGGTTTCTAAACTGAGTCTGGCG
            //416187-121416587:COSM21479:GCCAGCTGCAGACGGAGCTC:GT:chr12:121416607-121417007-850/2_rev_121416520	121416520	75M	AGGCGGCTAGCGTGGTGGACCCGGGCCGCGTGGCCCTGTGGCAGCCGAGCCATGGTTTCTAAACTGAGCCAGCTG

            var read = new BamAlignment();
            read.Bases = "AGGCGGCTAGCGTGGTGGACCCGGGCCGCGTGGCCCTGTGGCAGCCGAGCCATGGTTTCTAAACTGAGTCTGGCG";
            read.CigarData = new CigarAlignment("68M18D7M");
            read.Position = 121416520;
            read.Qualities = new byte[read.Bases.Length];

            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 121416588;
            vs1.VcfReferenceAllele = "GCCAGCTGCAGACGGAGCT";
            vs1.VcfAlternateAllele = "G"; //read should match ALT for this test


            var vsFromVcf = new List<VariantSite>() { vs1 };

            ExecuteTest(read, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 2);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 0);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 1);

            },
                (matchedVariants) =>
                {
                    Assert.Equal(matchedVariants[0].VcfReferencePosition, 121416588);
                    Assert.Equal(matchedVariants[0].VcfReferenceAllele, "GCCAGCTGCAGACGGAGCT");
                    Assert.Equal(matchedVariants[0].VcfAlternateAllele, "G");

                });
        }

        [Fact]
        public void ProcessInsertionReadTest()
        {
            //chr12:121431782-121432182:COSM46441:TGC:TACCTA:chr12:121432185-121432585-1478/2_fwd	121432113	72M3S	CGGGCCCCCCCCAGGGCCAGGCCCGGGACCTGCGCTGCCCGCTCACAGCTCCCCTGGCCTGCCTCCACCTACCTA
            //chr12:121431782-121432182:COSM46441:TGC:TACCTA:chr12:121432185-121432585-662/2_fwd	121432113	72M3S	CGGGCCCCCCCCAGGGCCAGGCCCGGGACCTGCGCTGCCCGCTCACAGCTCCCCTGGCCTGCCTCCACCTACCTA
            //chr12:121431782-121432182:COSM46441:TGC:TACCTA:chr12:121432185-121432585-1308/2_rev	121432114	71M3I1M	GGGCCCCCCCCAGGGCCAGGCCCGGGACCTGCGCTGCCCGCTCACAGCTCCCCTGGCCTGCCTCCACCTAC-CTA-C
            //chr12:121431782-121432182:COSM46441:TGC:TACCTA:chr12:121432185-121432585-64/2_rev	121432114	    71M3I1M	GGGCCCCCCCCAGGGCCAGGCCCGGGACCTGCGCTGCCCGCTCACAGCTCCCCTGGCCTGCCTCCACCTAC-TTA-C
            //chr12:121431782-121432182:COSM46441:TGC:TACCTA:chr12:121432185-121432585-1322/2_rev	121432114	75M	GGGCCCCCCCCAGGGCCAGGCCCGGGACCTGCGCTGCCCGCTCACAGCTCCCCTGGCCTGCCTCCACCTGC-CCTC

            var read = new BamAlignment();
            read.Bases = "GGGCCCCCCCCAGGGCCAGGCCCGGGACCTGCGCTGCCCGCTCACAGCTCCCCTGGCCTGCCTCCACCTACCTAC";
            //vcf coords  12-345678910-11,12,13,14
            read.CigarData = new CigarAlignment("71M3I1M");
            read.Position = 121432114;
            read.Qualities = new byte[read.Bases.Length];

            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 121432185;
            vs1.VcfReferenceAllele = "C";
            vs1.VcfAlternateAllele = "CCTA"; //read should match ALT for this test


            var vsFromVcf = new List<VariantSite>() { vs1 };

            //given a variant site, is it in the read?
            ExecuteTest(read, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SubsequenceType.MatchOrMismatchSequence].Count, 2);
                Assert.Equal(foundVariants[SubsequenceType.InsertionSquence].Count, 1);
                Assert.Equal(foundVariants[SubsequenceType.DeletionSequence].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 121432185);
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "C");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "CCTA");
            });
        }

        private void ExecuteTest(BamAlignment read, int minBaseCallQuality, List<VariantSite> vsFromVcf, Action<Dictionary<SubsequenceType, List<VariantSite>>> setCandidatesAssertions = null,
            Action<VariantSite[]> matchVariantsAssertions = null)
        {
            var readProcessor = new VeadFinder(new BamFilterParameters() { MinimumBaseCallQuality = minBaseCallQuality });
            int lastPos;
            var foundVariants
                = readProcessor.SetCandidateVariantsFoundInRead(minBaseCallQuality, read, out lastPos);

            //if (setCandidatesAssertions != null)
            //{
            setCandidatesAssertions(foundVariants);
            //}

            var matchedVariants
                       = readProcessor.MatchReadVariantsWithVcfVariants(vsFromVcf, foundVariants, read.Position + 1, lastPos);

            //if (matchVariantsAssertions != null)
            //{
            matchVariantsAssertions(matchedVariants);
            //}

            readProcessor.FindVariantResults(vsFromVcf, read);

        }

        [Fact]
        /// This is a test for the "CheckVariantSequenceForMatchInVariantSiteFromRead" fxn, where
        /// we are looking for evidence for a variant in the mismatch string we picked up from a read.
        public void CheckVariantSequenceForMatchInVariantSiteFromReadTest()
        {
            //go through the 4 cases that might be input to this method. (indels are managed a different way)

            CheckWeCanFindASnpInARead();
            CheckWeCanFindAnMNVInARead_healthyMNV();
            CheckWeCanFindAnMNVInARead_pathologicalMNV();
            CheckWeCanFindARefInARead();
       
        }

        private static void CheckWeCanFindASnpInARead()
        {
            var vcfSNP = new VariantSite();
            vcfSNP.VcfReferencePosition = 4;
            vcfSNP.VcfReferenceAllele = "T";
            vcfSNP.VcfAlternateAllele = "C";

            //a VS mined from a read that indeed contains the SNP
            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 2;
            vs1.VcfReferenceAllele = "AATAA";
            vs1.VcfAlternateAllele = "AACAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs1));

            //a VS mined from a shorter read that also contains the SNP
            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 4;
            vs2.VcfReferenceAllele = "T";
            vs2.VcfAlternateAllele = "C";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs2));

            //a VS mined from a read that contains a different SNP
            var vs3 = new VariantSite();
            vs3.VcfReferencePosition = 2;
            vs3.VcfReferenceAllele = "AATAA";
            vs3.VcfAlternateAllele = "AAGAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundDifferentVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs3));

            //a VS mined from a shorter read with a different SNP
            var vs4 = new VariantSite();
            vs4.VcfReferencePosition = 4;
            vs4.VcfReferenceAllele = "T";
            vs4.VcfAlternateAllele = "G";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundDifferentVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs4));

            //a VS mined from a read that contains a ref
            var vs5 = new VariantSite();
            vs5.VcfReferencePosition = 2;
            vs5.VcfReferenceAllele = "AATAA";
            vs5.VcfAlternateAllele = "AATAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundReferenceVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs5));

            //a VS mined from a shorter read with a ref
            var vs6 = new VariantSite();
            vs6.VcfReferencePosition = 4;
            vs6.VcfReferenceAllele = "T";
            vs6.VcfAlternateAllele = "T";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundReferenceVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs6));


            //a VS mined from a read that contains a no-call / base that failed filters
            var vs7 = new VariantSite();
            vs7.VcfReferencePosition = 2;
            vs7.VcfReferenceAllele = "AATAA";
            vs7.VcfAlternateAllele = "AANAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs7));

            //a VS mined from a shorter read with a no-call / base that failed filters
            var vs8 = new VariantSite();
            vs8.VcfReferencePosition = 4;
            vs8.VcfReferenceAllele = "T";
            vs8.VcfAlternateAllele = "N";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs8));
        }

        private static void CheckWeCanFindAnMNVInARead_healthyMNV()
        {
            var vcfSNP = new VariantSite();
            vcfSNP.VcfReferencePosition = 4;
            vcfSNP.VcfReferenceAllele = "TA";
            vcfSNP.VcfAlternateAllele = "CC";

            //a VS mined from a read that indeed contains the MNV
            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 2;
            vs1.VcfReferenceAllele = "AATAA";
            vs1.VcfAlternateAllele = "AACCAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs1));

            //a VS mined from a shorter read that also contains the MNV
            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 4;
            vs2.VcfReferenceAllele = "TA";
            vs2.VcfAlternateAllele = "CC";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs2));

            //a VS mined from a read that contains a different MNV
            var vs3 = new VariantSite();
            vs3.VcfReferencePosition = 2;
            vs3.VcfReferenceAllele = "AATAAA";
            vs3.VcfAlternateAllele = "AAGCAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundDifferentVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs3));

            //a VS mined from a shorter read with a different MNV
            var vs4 = new VariantSite();
            vs4.VcfReferencePosition = 4;
            vs4.VcfReferenceAllele = "TA";
            vs4.VcfAlternateAllele = "GC";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundDifferentVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs4));

            //a VS mined from a read that contains a ref
            var vs5 = new VariantSite();
            vs5.VcfReferencePosition = 2;
            vs5.VcfReferenceAllele = "AATAA";
            vs5.VcfAlternateAllele = "AATAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundReferenceVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs5));


            //a VS mined from a shorter read with a ref
            var vs6a = new VariantSite();
            vs6a.VcfReferencePosition = 4;
            vs6a.VcfReferenceAllele = "TA";
            vs6a.VcfAlternateAllele = "TA";
            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundReferenceVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs6a));

            //a VS mined from a shorter read with a ref
            var vs6 = new VariantSite();
            vs6.VcfReferencePosition = 4;
            vs6.VcfReferenceAllele = "T";
            vs6.VcfAlternateAllele = "T";

            //Here we dont claim we found the full reference sequence we are looking for. We run off the end of the read.
            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs6));

            //a VS mined from a read that contains a no-call / base that failed filters. Because of the "N".
            var vs7 = new VariantSite();
            vs7.VcfReferencePosition = 2;
            vs7.VcfReferenceAllele = "AATAA";
            vs7.VcfAlternateAllele = "AANAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs7));

            //a VS mined from a shorter read with a no-call / base that failed filters
            var vs8 = new VariantSite();
            vs8.VcfReferencePosition = 4;
            vs8.VcfReferenceAllele = "TA";
            vs8.VcfAlternateAllele = "NN";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs8));
        }

        /// <summary>
        /// Note, this test is deliberately stressing the algorithm over the usecase 
        /// of a pathological MNV (mnvs do not really have prepended bases inside Pisces/Scylla/ect).
        /// The point is to demonstrate that the method still check the whole alt allele sequence
        /// is a match with the read seqeunce being queried.
        /// </summary>
        private static void CheckWeCanFindAnMNVInARead_pathologicalMNV()
        {
            var vcfSNP = new VariantSite();
            vcfSNP.VcfReferencePosition = 4;
            vcfSNP.VcfReferenceAllele = "ATA";
            vcfSNP.VcfAlternateAllele = "ACG";

            //a VS section mined from a read that indeed contains the MNV (exactly)

            //what we are looking for:

            //                  1 2 3 4 5 6 7 8 9 
            // looking for ->   - - - A C G - - -
            // read:       ->   ? ? ? A C G ? ? ?


            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 4;
            vs1.VcfReferenceAllele = "ATA";
            vs1.VcfAlternateAllele = "ACG";  

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs1));

            //a VS mined from a longer read that also contains the MNV

            //what we are looking for:

            //                  1 2 3 4 5 6 7 8 9 
            // looking for  ->  - - - A C G - - -
            // read:        ->  ? ? A A C G A ? ?

            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 3;
            vs2.VcfReferenceAllele = "AATAA";
            vs2.VcfAlternateAllele = "AACGA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs2));


            //a VS mined from a shorter read that does NOT prove the existence of the MNV
            //IN this case (to get a section this short to parse) we must have seen a whacky cigar string with only 1 match (ex, 4I1M8I)
            //or we are approaching the end of the read and have one base left to check, perhaps the other bases failed filters or got softclipped.


            //what we are looking for:

            //                  1 2 3 4 5 6 7 8 9 
            // looking for  ->  - - - A C G - - -
            // read:        ->  ? ? ? ? C ? ? ? ?


            var vs3 = new VariantSite();
            vs3.VcfReferencePosition = 5;
            vs3.VcfReferenceAllele = "T";
            vs3.VcfAlternateAllele = "C";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs3));

            //a VS section mined from a read that indeed contain a ref site 
            var vs4 = new VariantSite();
            vs4.VcfReferencePosition = 3;
            vs4.VcfReferenceAllele = "AATAA";
            vs4.VcfAlternateAllele = "AATAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundReferenceVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs4));

            //a VS section mined from a read that indeed contain a diff MNV site 


            //what we are looking for:

            //                  1 2 3 4 5 6 7 8 9 
            // looking for  ->  - - - A C G - - -
            // read:        ->  ? ? G G G G G ? ?


            var vs5 = new VariantSite();
            vs5.VcfReferencePosition = 3;
            vs5.VcfReferenceAllele = "AATAA";
            vs5.VcfAlternateAllele = "GGGGG";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundDifferentVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs5));

            //a VS section mined from a read that indeed contain a diff MNV site 
            var vs6 = new VariantSite();
            vs6.VcfReferencePosition = 3;
            vs6.VcfReferenceAllele = "AATAA";
            vs6.VcfAlternateAllele = "AACAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundDifferentVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs6));


            //We are looking for ATA -> ACG , pos 4.
            //We will never find it in a sequence that starts on position 5 (as in vs7).
            //We either have found pos 4 ealier, when this method was called on a previous sequence (in which case we would exit before getting here)
            //~or~ all the bases at postion 4 got clipped or filtered off, which is why we are starting so late in the read.
            //( -> result should be that the bases in the test sequence for pos 4 must have FailedFilters and were not available to the query method)

            //what we are looking for:

            //                  1 2 3 4 5 6 7 8 9 
            // looking for ->   - - - A C G - - -
            // read:       ->   ? ? ? ? C A A ? ?

            var vs7 = new VariantSite();
            vs7.VcfReferencePosition = 5;
            vs7.VcfReferenceAllele = "TAA";
            vs7.VcfAlternateAllele = "CAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs7));


            //We are looking for ATA -> ACG , pos 4.
            //We will never find it in a sequence that starts on position 5 (as in vs8).
            //We either have found pos 4 ealier, when this method was called on a previous sequence (in which case we would exit before getting here)
            //~or~ all the bases at postion 4 got clipped or filtered off, which is why we are starting so late in the read.
            //( -> result should be that the bases in the test sequence for pos 4 must have FailedFilters and were not available to the query method)


            //what we are looking for:

            //                  1 2 3 4 5 6 7 8 9 
            // looking for ->   - - - A C G - - -
            // read:       ->   ? ? ? ? C G ? ? ?


            var vs8 = new VariantSite();
            vs8.VcfReferencePosition = 5;
            vs8.VcfReferenceAllele = "TA";
            vs8.VcfAlternateAllele = "CG";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs8));
        }

            private static void CheckWeCanFindARefInARead()
        {
            //for this case "found this varaint" or "found reference variant" are interchangeable results.
            //This variant we are looking for *is* the reference.

            var vcfSNP = new VariantSite();
            vcfSNP.VcfReferencePosition = 4;
            vcfSNP.VcfReferenceAllele = "T";
            vcfSNP.VcfAlternateAllele = "T";

            //a VS mined from a read that indeed contains the ref
            var vs1 = new VariantSite();
            vs1.VcfReferencePosition = 2;
            vs1.VcfReferenceAllele = "AATAA";
            vs1.VcfAlternateAllele = "AATCAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs1));

            //a VS mined from a shorter read that also contains the ref
            var vs2 = new VariantSite();
            vs2.VcfReferencePosition = 4;
            vs2.VcfReferenceAllele = "TA";
            vs2.VcfAlternateAllele = "TC";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs2));

            //a VS mined from a read that contains a different SNP
            var vs3 = new VariantSite();
            vs3.VcfReferencePosition = 2;
            vs3.VcfReferenceAllele = "AATAAA";
            vs3.VcfAlternateAllele = "AAGCAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundDifferentVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs3));

            //a VS mined from a shorter read with a different SNP
            var vs4 = new VariantSite();
            vs4.VcfReferencePosition = 4;
            vs4.VcfReferenceAllele = "TA";
            vs4.VcfAlternateAllele = "GC";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundDifferentVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs4));

            //a VS mined from a read that contains a ref
            var vs5 = new VariantSite();
            vs5.VcfReferencePosition = 2;
            vs5.VcfReferenceAllele = "AATAA";
            vs5.VcfAlternateAllele = "AATAA";
            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs5));


            //a VS mined from a shorter read with a ref
            var vs6a = new VariantSite();
            vs6a.VcfReferencePosition = 4;
            vs6a.VcfReferenceAllele = "TA";
            vs6a.VcfAlternateAllele = "TA";
            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs6a));

            //a VS mined from a shorter read with a ref
            var vs6 = new VariantSite();
            vs6.VcfReferencePosition = 4;
            vs6.VcfReferenceAllele = "T";
            vs6.VcfAlternateAllele = "T";
            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.FoundThisVariant, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs6));

            //a VS mined from a read that contains a no-call / base that failed filters. Because of the "N".
            var vs7 = new VariantSite();
            vs7.VcfReferencePosition = 2;
            vs7.VcfReferenceAllele = "AATAA";
            vs7.VcfAlternateAllele = "AANAA";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs7));

            //a VS mined from a shorter read with a no-call / base that failed filters
            var vs8 = new VariantSite();
            vs8.VcfReferencePosition = 4;
            vs8.VcfReferenceAllele = "TA";
            vs8.VcfAlternateAllele = "NN";

            Assert.Equal(VeadFinder.StateOfPhasingSiteInRead.HaveInsufficientData, VeadFinder.CheckVariantSequenceForMatchInVariantSiteFromRead(vcfSNP, vs8));
        }
    }
}
