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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);

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

            var vsFromVcf = new List<VariantSite>() { vs1, vs2 };
            vsFromVcf.Sort();

            //given a variant site, is it in the read? none of these bases should pass

            ExecuteTest(read, 10, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 4);
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "N");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "N");

                Assert.Equal(matchedVariants[1].VcfReferencePosition, 4);  //a deletion not supported by the reads
                Assert.Equal(matchedVariants[1].VcfReferenceAllele, "N");  //to we just return T>T, a reference call at this loci.
                Assert.Equal(matchedVariants[1].VcfAlternateAllele, "N");

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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);

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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);

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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);

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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);

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
                Assert.Equal(fv[SomaticVariantType.SNP].Count, 2);
                Assert.Equal(fv[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(fv[SomaticVariantType.Deletion].Count, 1);
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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 2);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 1);                
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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);

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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);
            },
                (matchedVariants) =>
                {
                    //all the positions of interest are softclipped. this read is useless.           
                    Assert.Equal(matchedVariants, null);
                });

            //test r4

            ExecuteTest(r4, 0, vsFromVcf, (foundVariants) =>
            {
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);                
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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 2);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);

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

            ExecuteTest(r6, 0, vsFromVcf, (fv)=>{ 
                Assert.Equal(fv[SomaticVariantType.SNP].Count, 2);
                Assert.Equal(fv[SomaticVariantType.Insertion].Count, 1);
                Assert.Equal(fv[SomaticVariantType.Deletion].Count, 0);},
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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 2);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 0);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 1);

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
                Assert.Equal(foundVariants[SomaticVariantType.SNP].Count, 2);
                Assert.Equal(foundVariants[SomaticVariantType.Insertion].Count, 1);
                Assert.Equal(foundVariants[SomaticVariantType.Deletion].Count, 0);

            }, (matchedVariants) =>
            {
                Assert.Equal(matchedVariants[0].VcfReferencePosition, 121432185);
                Assert.Equal(matchedVariants[0].VcfReferenceAllele, "C");
                Assert.Equal(matchedVariants[0].VcfAlternateAllele, "CCTA");
            });
        }

        private void ExecuteTest(BamAlignment read, int minBaseCallQuality, List<VariantSite> vsFromVcf, Action<Dictionary<SomaticVariantType, List<VariantSite>>> setCandidatesAssertions = null,
            Action<VariantSite[]> matchVariantsAssertions = null)
        {
            var readProcessor = new VeadFinder(new BamFilterParameters() { MinimumBaseCallQuality = minBaseCallQuality });
            int lastPos;
            var foundVariants
                = readProcessor.SetCandidateVariantsFoundInRead(minBaseCallQuality, read, out lastPos);

            if (setCandidatesAssertions != null)
            {
                setCandidatesAssertions(foundVariants);
            }

            var matchedVariants
                       = readProcessor.MatchReadVariantsWithVcfVariants(vsFromVcf, foundVariants, read.Position + 1, lastPos);

            if (matchVariantsAssertions != null)
            {
                matchVariantsAssertions(matchedVariants);
            }

            readProcessor.FindVariantResults(vsFromVcf, read);

        }
    }
}
