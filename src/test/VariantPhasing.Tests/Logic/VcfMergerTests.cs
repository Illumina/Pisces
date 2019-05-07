using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Options;
using Pisces.IO;
using Pisces.IO.Interfaces;
using TestUtilities;
using VariantPhasing.Interfaces;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class VcfMergerTests
    {

        [Fact]
        public void WriteANbhd()
        {

            var outputFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "PhasedVcfFileNbhdWriterTest.vcf");
            var inputFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "MergerInput.vcf");
            var expectedFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "MergerOutput.vcf");

            File.Delete(outputFilePath);

            var context = new VcfWriterInputContext
            {
                QuotedCommandLineString = "myCommandLine",
                SampleName = "mySample",
                ReferenceName = "myReference",
                ContigsByChr = new List<Tuple<string, long>>
                {
                    new Tuple<string, long>("chr1", 10001),
                    new Tuple<string, long>("chrX", 500)
                }
            };

            var config = new VcfWriterConfig
            {
                DepthFilterThreshold = 500,
                VariantQualityFilterThreshold = 30,
                FrequencyFilterThreshold = 0.007f,
                ShouldOutputNoCallFraction = true,
                ShouldOutputStrandBiasAndNoiseLevel = true,
                EstimatedBaseCallQuality = 23,
                PloidyModel = PloidyModel.Somatic,
                AllowMultipleVcfLinesPerLoci = true
            };
            var writer = new PhasedVcfWriter(outputFilePath, config, new VcfWriterInputContext(), new List<string>() { }, null);
            var reader = new AlleleReader(inputFilePath, true);


            //set up the original variants
            var originalVcfVariant1 = TestHelper.CreateDummyAllele("chr2", 116380048, "A", "New", 1000, 156);
            var originalVcfVariant2 = TestHelper.CreateDummyAllele("chr2", 116380048, "AAA", "New", 1000, 156);
            var originalVcfVariant4 = TestHelper.CreateDummyAllele("chr7", 116380051, "A", "New", 1000, 156);
            var originalVcfVariant5 = TestHelper.CreateDummyAllele("chr7", 116380052, "AC", "New", 1000, 156);

            var vs1 = new VariantSite((originalVcfVariant1));
            var vs2 = new VariantSite((originalVcfVariant2));
            var vs4 = new VariantSite((originalVcfVariant4));
            var vs5 = new VariantSite((originalVcfVariant5));


            //have to replace variants at positon 116380048 (we call two new MNVS here)
            var nbhd1 = new VcfNeighborhood( 0, "chr2", vs1, vs2);
            var calledNbh1 = new CallableNeighborhood(nbhd1, new VariantCallingParameters());

            //have to replace variants at positon 116380051 and 52  (we call one new MNV at 51)
            var nbhd2 = new VcfNeighborhood(0, "chr7", vs4, vs5);
            var calledNbh2 = new CallableNeighborhood(nbhd2, new VariantCallingParameters());

            VcfMerger merger = new VcfMerger(reader);
            List<Tuple<CalledAllele, string>> alleleTuplesPastNbhd = new List<Tuple<CalledAllele, string>>();

            calledNbh1.CalledVariants = new Dictionary<int, List<CalledAllele>> { { originalVcfVariant1.ReferencePosition, new List<CalledAllele> {originalVcfVariant1, originalVcfVariant2 } } };
            calledNbh2.CalledVariants = new Dictionary<int, List<CalledAllele>> { { originalVcfVariant4.ReferencePosition, new List<CalledAllele> {originalVcfVariant4 } } };


            alleleTuplesPastNbhd = merger.WriteVariantsUptoChr(writer, alleleTuplesPastNbhd, nbhd1.ReferenceName);

            alleleTuplesPastNbhd = merger.WriteVariantsUptoIncludingNbhd(writer, alleleTuplesPastNbhd, calledNbh1);

            alleleTuplesPastNbhd = merger.WriteVariantsUptoChr(writer, alleleTuplesPastNbhd, nbhd2.ReferenceName);

            alleleTuplesPastNbhd = merger.WriteVariantsUptoIncludingNbhd(writer, alleleTuplesPastNbhd, calledNbh2);

            merger.WriteRemainingVariants(writer, alleleTuplesPastNbhd);

            writer.Dispose();

            var expectedLines = File.ReadLines(expectedFilePath).ToList();
            var outputLines = File.ReadLines(outputFilePath).ToList();

            Assert.Equal(expectedLines.Count(), outputLines.Count());

            for (int i=0;i<expectedLines.Count;i++)
                Assert.Equal(expectedLines[i], outputLines[i]);
        }


        [Fact]
        public void WriteADiploidNbhd()
        {
            var outputDir = Path.Combine(TestPaths.LocalScratchDirectory, "MergerWriteADiploidNbhd");
            var outputFilePath = Path.Combine(outputDir, "TinyDiploid.Phased.vcf");
            var inputFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "TinyDiploid.vcf");
            var expectedFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "TinyDiploidOutput.vcf");

            TestHelper.RecreateDirectory(outputDir);

            var context = new VcfWriterInputContext
            {
                QuotedCommandLineString = "myCommandLine",
                SampleName = "mySample",
                ReferenceName = "myReference",
                ContigsByChr = new List<Tuple<string, long>>
                {
                    new Tuple<string, long>("chr1", 10001),
                    new Tuple<string, long>("chr22", 51304566),
                    new Tuple<string, long>("chrX", 500)
                }
            };

            var config = new VcfWriterConfig
            {
                DepthFilterThreshold = 500,
                VariantQualityFilterThreshold = 30,
                FrequencyFilterThreshold = 0.007f,
                ShouldOutputNoCallFraction = true,
                ShouldOutputStrandBiasAndNoiseLevel = true,
                EstimatedBaseCallQuality = 23,
                PloidyModel = PloidyModel.DiploidByThresholding,
                AllowMultipleVcfLinesPerLoci = false
            };
            var writer = new PhasedVcfWriter(outputFilePath, config, new VcfWriterInputContext(), new List<string>() { }, null);
            var reader = new AlleleReader(inputFilePath, true);


            //set up the original variants
            var originalVcfVariant1 = TestHelper.CreateDummyAllele("chr1", 1, "A", "G", 1000, 156);
            var originalVcfVariant2 = TestHelper.CreateDummyAllele("chr1", 1, "A", "T", 1000, 156);
            var originalVcfVariant4 = TestHelper.CreateDummyAllele("chr22", 1230237, "GTC", "G", 1000, 156);
            var originalVcfVariant5 = TestHelper.CreateDummyAllele("chr22", 1230237, "GTC", "GTCT", 1000, 156);

            var vs1 = new VariantSite((originalVcfVariant1));
            var vs2 = new VariantSite((originalVcfVariant2));
            var vs4 = new VariantSite((originalVcfVariant4));
            var vs5 = new VariantSite((originalVcfVariant5));


            //have to replace variants at positon 116380048 (we call two new MNVS here)
            var nbhd1 = new VcfNeighborhood(0, "chr1", vs1, vs2);
            var calledNbh1 = new CallableNeighborhood(nbhd1, new VariantCallingParameters());

            VcfMerger merger = new VcfMerger(reader);
            List<Tuple<CalledAllele, string>> alleleTuplesPastNbhd = new List<Tuple<CalledAllele, string>>();

            //we will just say, we called the variants that were in the origina vcf. Ie, we agree with it.
            calledNbh1.CalledVariants = new Dictionary<int, List<CalledAllele>> { { originalVcfVariant1.ReferencePosition, new List<CalledAllele> { originalVcfVariant1, originalVcfVariant2 } } };

            //Realizes the first nbhd starts at chr1 . We have to do something with the first lines of the vcf (chr1	1	.	A	G,T)
            //so, alleleTuplesPastNbhd = chr1	1	.	A	G,T
            alleleTuplesPastNbhd = merger.WriteVariantsUptoChr(writer, alleleTuplesPastNbhd, nbhd1.ReferenceName);
            Assert.True(alleleTuplesPastNbhd[0].Item1.IsSameAllele(originalVcfVariant1));
            Assert.True(alleleTuplesPastNbhd[1].Item1.IsSameAllele(originalVcfVariant2));

            //This method writes everything up to the end of nbhd 1,
            //so "(chr1	1	.	A	G,T)" from the vcf and the variants scylla detected "(chr1	1	.	A	G,T)" need to be dealt with.
            //Since these 4 variants are actually the same two, we need to remove the vcf ones and only write the scylla ones.
            //Thn we peek into the vcf and see the next line is "chr22	1230237	.	GTC	G,GTCT", clearly outside nbh1.
            //so we write out everything we need for nbhd1, and save the peeked line
            alleleTuplesPastNbhd = merger.WriteVariantsUptoIncludingNbhd(writer, alleleTuplesPastNbhd, calledNbh1);
            Assert.True(alleleTuplesPastNbhd[0].Item1.IsSameAllele(originalVcfVariant4));
            Assert.True(alleleTuplesPastNbhd[1].Item1.IsSameAllele(originalVcfVariant5));

            //now write out 
            //chr22   1230237.GTC G,GTCT  50  DP = 1370 GT: GQ: AD: DP: VF: NL: SB: NC: US  1 / 2:100:185,68:364:0.258:20:-100.0000:0.0000:0,0,0,0,0,0,1,1,0,0,0,2
            //chrX    79.CG  GTG,AA  50  DP = 1370 GT: GQ: AD: DP: VF: NL: SB: NC: US  1 / 2:100:185,68:364:0.258:20:-100.0000:0.0000:0,0,0,0,0,0,1,1,0,0,0,2
            merger.WriteRemainingVariants(writer, alleleTuplesPastNbhd);

            writer.Dispose();

            var expectedLines = File.ReadLines(expectedFilePath).ToList();
            var outputLines = File.ReadLines(outputFilePath).ToList();

            Assert.Equal(expectedLines.Count(), outputLines.Count());

            for (int i = 0; i < expectedLines.Count; i++)
                Assert.Equal(expectedLines[i], outputLines[i]);
        }

        [Fact]
        public void GetAcceptedVariants_MergeNull()
        {
            var originalVcfVariant = TestHelper.CreateDummyAllele("chr1", 123, "A", "T", 1000, 156);
            var originalVcfVariant2 = TestHelper.CreateDummyAllele("chr1", 124, "A", "T", 1000, 156);
            var originalVcfVariant3 = TestHelper.CreateDummyAllele("chr1", 234, "A", "T", 1000, 156);
            Tuple<CalledAllele, string>[] stagedVcfVariants = 
                { new Tuple<CalledAllele, string>(originalVcfVariant, ""),
                    new Tuple<CalledAllele, string>(originalVcfVariant2, ""),
                    new Tuple<CalledAllele, string>(originalVcfVariant3, "") };

            var variantsUsedByCaller = new List<CalledAllele>() { originalVcfVariant, originalVcfVariant2 };

            var stagedCalledMNV = new CalledAllele(AlleleCategory.Snv) { Chromosome = "chr1", ReferencePosition = 123, ReferenceAllele = "A", AlternateAllele = "T" };

            var stagedCalledMNVs = new Dictionary<int, List<CalledAllele>>() {
                { stagedCalledMNV.ReferencePosition, new List<CalledAllele>() {  stagedCalledMNV} } } ;

            var stagedCalledRefs = new Dictionary<int, CalledAllele>() {
                { 123, new CalledAllele(AlleleCategory.Reference) {ReferencePosition=123,Chromosome="chr1",ReferenceAllele="A",AlternateAllele="." }   },
                { 124, new CalledAllele(AlleleCategory.Reference) { ReferencePosition = 124, Chromosome = "chr1", ReferenceAllele = "A", AlternateAllele = "." }  }
                } ;


            //since there is an alt at position 124 ( a call of 156 alt / 1000 total, that means 844 original ref calls.
            //Of which we said, 100 will get sucked up. So that leaves 744 / 1000 calls for a reference.
            //So, we can still make a confident ref call. 

            var mockNeighborhood = new Mock<ICallableNeighborhood>();
            mockNeighborhood.Setup(n => n.GetOriginalVcfVariants()).Returns(variantsUsedByCaller.ToList());
            mockNeighborhood.Setup(n => n.CalledVariants).Returns(stagedCalledMNVs);
            mockNeighborhood.Setup(n => n.CalledRefs).Returns(stagedCalledRefs);

            
            var accepted = VcfMerger.GetMergedListOfVariants(mockNeighborhood.Object, stagedVcfVariants.ToList());

            Assert.Equal(3, accepted.Count);

            var vcfVariant2asNull = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 124,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>()
                { new Dictionary<string, string>() {{"GT", "0/0"},{"DP", "1000"},{"AD", "744"} }},
            };
            
            CheckVariantsMatch(originalVcfVariant, accepted[0].Item1);
            CheckVariantsMatch(vcfVariant2asNull, accepted[1].Item1);
            CheckVariantsMatch(originalVcfVariant3, accepted[2].Item1);


            //re-stage the MNVs
            var stagedCalledMNVs2 = new Dictionary<int, List<CalledAllele>>() {
                { stagedCalledMNV.ReferencePosition, new List<CalledAllele>() {  stagedCalledMNV} } };
            mockNeighborhood.Setup(n => n.CalledVariants).Returns(stagedCalledMNVs2);

            // If one has been sucked up all the way, we should output it as a nocall 
            // (but we have to statge it already as a no call allready, becasue the merger can't do the conversion.
            var stagedCalledRefs2 = new Dictionary<int, CalledAllele>() {
                { 123, new CalledAllele(AlleleCategory.Reference) {ReferencePosition=123,Chromosome="chr1",ReferenceAllele="A",AlternateAllele="." }   },
                { 124, new CalledAllele(AlleleCategory.Reference) { ReferencePosition = 124, Chromosome = "chr1", ReferenceAllele = "A", AlternateAllele = ".", Genotype= Genotype.RefLikeNoCall } }
                };
            mockNeighborhood.Setup(n => n.CalledRefs).Returns(stagedCalledRefs2);

            accepted = VcfMerger.GetMergedListOfVariants(mockNeighborhood.Object, stagedVcfVariants.ToList());


            Assert.Equal(3, accepted.Count);

            vcfVariant2asNull = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 124,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() { { "GT", "./." } } },
            };

            CheckVariantsMatch(originalVcfVariant, accepted[0].Item1);
            CheckVariantsMatch(vcfVariant2asNull, accepted[1].Item1);
            CheckVariantsMatch(originalVcfVariant3, accepted[2].Item1);


        }

        [Fact]
        public void GetMergedListOfVariants_LeaveUntouchedAsIs()
        {
            //chr7	55242464	.	A	G	6	LowSupport	DP=287	GT:GQ:AD:DP:VF:NL:SB:NC:US:AQ:LQ	0/1:6:286,1:287:0.00348:30:-7.4908:0.0304:0,0,0,0,0,1,56,17,49,56,69,40:4.294:0.000
            //chr7	55242464	.	AGGAATTAAGAGAAGC	A	100	PASS	DP=298	GT:GQ:AD:DP:VF:NL:SB:NC:US:AQ:LQ	0/1:100:284,14:298:0.04698:30:-75.6792:0.0000:1,0,1,4,5,3,58,18,49,55,71,41:100.000:100.000
            //chr7	55242481	.	A	T	6	LowSupport	DP=306	GT:GQ:AD:DP:VF:NL:SB:NC:US:AQ:LQ	0/1:6:305,1:306:0.00327:30:-7.4622:0.0556:0,0,0,0,0,1,63,20,54,52,69,48:3.669:0.000
            //chr7	55242487	.	C	T	6	LowSupport	DP=325	GT:GQ:AD:DP:VF:NL:SB:NC:US:AQ:LQ	0/1:6:324,1:325:0.00308:30:-7.1283:0.0469:0,0,0,1,0,0,67,24,61,53,68,52:1.954:0.000
            //chr7	55242489	.	G	T	6	LowSupport	DP=327	GT:GQ:AD:DP:VF:NL:SB:NC:US:AQ:LQ	0/1:6:326,1:327:0.00306:30:-7.0226:0.0411:0,0,1,0,0,0,71,23,60,54,67,52:2.177:0.000

            var originalVcfVariant1 = TestHelper.CreateDummyAllele("chr7", 55242464, "A", "G", 287, 1);
            originalVcfVariant1.ReferenceSupport = 286;
            var originalVcfVariant2 = TestHelper.CreateDummyAllele("chr2", 55242464, "AGGAATTAAGAGAAGC", "A", 298, 14);
            originalVcfVariant2.ReferenceSupport = 284;
            var originalVcfVariant3 = TestHelper.CreateDummyAllele("chr7", 55242481, "A", "T", 306, 1);
            originalVcfVariant3.ReferenceSupport = 305;
            var originalVcfVariant4 = TestHelper.CreateDummyAllele("chr7", 55242487, "C", "T", 325, 1);
            originalVcfVariant4.ReferenceSupport = 324;
            var originalVcfVariant5 = TestHelper.CreateDummyAllele("chr7", 55242489, "G", "T", 327, 1);
            originalVcfVariant5.ReferenceSupport = 326;

            //#2mnv accepted: chr7 55242464 . AGGAATTAAGAGAAGC A
            //chr7	55242464	.	AGGAATTAAGAGAAGC	A	100	PASS	DP=286	GT:GQ:AD:DP:VF:NL:SB:NC:US	0/1:100:272,13:286:0.04545:30:-100.0000:0.3024:0,0,0,0,0,0,0,0,0,0,0,0
            //#3mnv accepted: chr7 55242464 . AGGAATTAAGAGAAGCAA GAT.   
            //chr7	55242464	.	AGGAATTAAGAGAAGCAA	GAT	6	PASS	DP=293	GT:GQ:AD:DP:VF:NL:SB:NC:US	0/1:6:226,1:293:0.00341:30:-100.0000:0.2854:0,0,0,0,0,0,0,0,0,0,0,0

            var mnv1 = TestHelper.CreateDummyAllele("chr7", 55242464, "AGGAATTAAGAGAAGC", "A", 286, 13);
            mnv1.ReferenceSupport = 272;
            var mnv2 = TestHelper.CreateDummyAllele("chr7", 55242464, "AGGAATTAAGAGAAGCAA", "GAT", 293, 1);
            mnv2.ReferenceSupport = 226;
            //#4mnv accepted: chr7 55242487 . C T.
            var mnv3 = TestHelper.CreateDummyAllele("chr7", 55242487, "C", "T", 325, 1);
            mnv3.ReferenceSupport = 324;
            //#5mnv accepted: chr7 55242489 . G T.
            var mnv4 = TestHelper.CreateDummyAllele("chr7", 55242489, "G", "T", 327, 1);
            mnv4.ReferenceSupport = 326;

            var vs1 = new VariantSite((originalVcfVariant1));
            var vs2 = new VariantSite((originalVcfVariant2));
            var vs3 = new VariantSite((originalVcfVariant3));
            var vs4 = new VariantSite((originalVcfVariant4));
            var vs5 = new VariantSite((originalVcfVariant5));

            var nbhd1 = new VcfNeighborhood(0, "chr7", vs1, vs2);
            nbhd1.AddVariantSite(vs3);
            nbhd1.AddVariantSite(vs4);
            nbhd1.AddVariantSite(vs5);
            var calledNbhd = new CallableNeighborhood(nbhd1, new VariantCallingParameters());
            calledNbhd.CalledVariants = new Dictionary<int, List<CalledAllele>> {
                {mnv1.ReferencePosition, new List<CalledAllele>(){mnv1, mnv2}},
                {mnv3.ReferencePosition, new List<CalledAllele>(){mnv3}},
                {mnv4.ReferencePosition, new List<CalledAllele>(){mnv4}},
            };
            //Became ref
            //chr7	55242481	.	A	.	100	PASS	DP=306	GT:GQ:AD:DP:VF:NL:SB:NC:US	0/.:100:305:306:0.00327:30:-100.0000:0.0556:0,0,0,0,0,0,0,0,0,0,0,0
            var var3AsRef = TestHelper.CreateDummyAllele("chr7", 55242481, "A", ".", 306, 0);
            calledNbhd.CalledRefs = new Dictionary<int, CalledAllele>()
            {
                {var3AsRef.ReferencePosition, var3AsRef}
            };

            var origAlleles = new List<Tuple<CalledAllele, string>>();
            origAlleles.Add(new Tuple<CalledAllele, string>(originalVcfVariant1, "Variant1"));
            origAlleles.Add(new Tuple<CalledAllele, string>(originalVcfVariant2, "Variant2"));
            origAlleles.Add(new Tuple<CalledAllele, string>(originalVcfVariant3, "Variant3"));
            origAlleles.Add(new Tuple<CalledAllele, string>(originalVcfVariant4, "Variant4"));
            origAlleles.Add(new Tuple<CalledAllele, string>(originalVcfVariant5, "Variant5"));
            var mergedList = VcfMerger.GetMergedListOfVariants(calledNbhd, origAlleles);
            Assert.Equal(5, mergedList.Count);
            // Anything that is new from phasing (real MNV, ref conversion) should have empty string portion of the tuple.
            Assert.Equal(3, mergedList.Count(x => x.Item2 == ""));
            // Variant4 and 5 should be retained as-is because after being spat out of phasing nothing has changed in terms of allele, ref support, allele support, or coverage
            Assert.Equal(1, mergedList.Count(x => x.Item2 == "Variant4"));
            Assert.Equal(1, mergedList.Count(x => x.Item2 == "Variant5"));

            //Should take new one if anything is changed
            // Pretend mnv3 had a ref base sucked up by other MNV
            mnv3.ReferenceSupport = originalVcfVariant4.ReferenceSupport - 1; 
            calledNbhd.CalledVariants = new Dictionary<int, List<CalledAllele>> {
                {mnv1.ReferencePosition, new List<CalledAllele>(){mnv1, mnv2}},
                {mnv3.ReferencePosition, new List<CalledAllele>(){mnv3}},
                {mnv4.ReferencePosition, new List<CalledAllele>(){mnv4}},
            };
            mergedList = VcfMerger.GetMergedListOfVariants(calledNbhd, origAlleles);
            Assert.Equal(5, mergedList.Count);
            // Anything that is new from phasing (real MNV, ref conversion) should have empty string portion of the tuple.
            Assert.Equal(4, mergedList.Count(x => x.Item2 == ""));
            // Only Variant5 should be retained as-is because after being spat out of phasing nothing has changed in terms of allele, ref support, allele support, or coverage
            // Variant 4 has changed in terms of ref support.
            Assert.Equal(0, mergedList.Count(x => x.Item2 == "Variant4"));
            Assert.Equal(1, mergedList.Count(x => x.Item2 == "Variant5"));

            // Pretend mnv3 had coverage changed (not sure this is realistic, but to cover all bases adding test)
            mnv3.ReferenceSupport = originalVcfVariant4.ReferenceSupport;
            mnv3.TotalCoverage = originalVcfVariant4.TotalCoverage - 1;
            calledNbhd.CalledVariants = new Dictionary<int, List<CalledAllele>> {
                {mnv1.ReferencePosition, new List<CalledAllele>(){mnv1, mnv2}},
                {mnv3.ReferencePosition, new List<CalledAllele>(){mnv3}},
                {mnv4.ReferencePosition, new List<CalledAllele>(){mnv4}},
            };
            mergedList = VcfMerger.GetMergedListOfVariants(calledNbhd, origAlleles);
            Assert.Equal(5, mergedList.Count);
            // Anything that is new from phasing (real MNV, ref conversion) should have empty string portion of the tuple.
            Assert.Equal(4, mergedList.Count(x => x.Item2 == ""));
            // Only Variant5 should be retained as-is because after being spat out of phasing nothing has changed in terms of allele, ref support, allele support, or coverage
            // Variant 4 has changed in terms of ref support.
            Assert.Equal(0, mergedList.Count(x => x.Item2 == "Variant4"));
            Assert.Equal(1, mergedList.Count(x => x.Item2 == "Variant5"));

            // Pretend mnv3 had allele support changed (not sure this is realistic, but to cover all bases adding test)
            mnv3.TotalCoverage = originalVcfVariant4.TotalCoverage;
            mnv3.AlleleSupport = originalVcfVariant4.AlleleSupport - 1;
            calledNbhd.CalledVariants = new Dictionary<int, List<CalledAllele>> {
                {mnv1.ReferencePosition, new List<CalledAllele>(){mnv1, mnv2}},
                {mnv3.ReferencePosition, new List<CalledAllele>(){mnv3}},
                {mnv4.ReferencePosition, new List<CalledAllele>(){mnv4}},
            };
            mergedList = VcfMerger.GetMergedListOfVariants(calledNbhd, origAlleles);
            Assert.Equal(5, mergedList.Count);
            // Anything that is new from phasing (real MNV, ref conversion) should have empty string portion of the tuple.
            Assert.Equal(4, mergedList.Count(x => x.Item2 == ""));
            // Only Variant5 should be retained as-is because after being spat out of phasing nothing has changed in terms of allele, ref support, allele support, or coverage
            // Variant 4 has changed in terms of ref support.
            Assert.Equal(0, mergedList.Count(x => x.Item2 == "Variant4"));
            Assert.Equal(1, mergedList.Count(x => x.Item2 == "Variant5"));

        }

        // new test-request from code review:
        //If we found a new MNV, not in a sucked-up position,
        //make sure we do not over-write an existing MNV that was not used for variant calling 
        //
        //In this example, original variants are at positions123, 124, and two at position 234
        //We will say one sucked up variant is at 123, another at 234, one variant to keep is at 234.
        //And one new MNV is at position 229.

        [Fact]
        public void GetAcceptedVariants_MergeVariants()
        {
            var originalVcfVariant = TestHelper.CreateDummyAllele("chr1", 123, "A", "T", 1000, 156);
            var originalVcfVariant2 = TestHelper.CreateDummyAllele("chr1", 124, "A", "T", 1000, 156);
            var originalVcfVariant3 = TestHelper.CreateDummyAllele("chr1", 234, "A", "T", 1000, 156);
            var originalVcfVariant4 = TestHelper.CreateDummyAllele("chr1", 234, "A", "C", 1000, 156);

            var vcfVariant0asRef = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 123,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() { { "GT", "0/0" } } },
            };

            var vcfVariant3asRef = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 234,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() { { "GT", "0/0" } } },
            };

            var vcfVariant2asNull = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 124,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() { { "GT", "./." } } },
            };

            var newMNV = new CalledAllele()
            {
                Chromosome = "chr1",
                ReferencePosition = 229,
                ReferenceAllele = "AA",
                AlternateAllele= "T",
                Genotype = Genotype.HeterozygousAltRef
            };

            Tuple<CalledAllele, string>[] stagedVcfVariants =
            { new Tuple<CalledAllele, string>(originalVcfVariant, ""),
                new Tuple<CalledAllele, string>(originalVcfVariant2, ""),
                new Tuple<CalledAllele, string>(originalVcfVariant3, ""),
                new Tuple<CalledAllele, string>(originalVcfVariant4, "") };

            var variantsUsedByCaller2 = new List<CalledAllele>() {originalVcfVariant, originalVcfVariant2, originalVcfVariant3 };

            var nbhd = new Mock<ICallableNeighborhood>();
            nbhd.Setup(n => n.GetOriginalVcfVariants()).Returns(variantsUsedByCaller2.ToList());

            var stagedCalledMNVs2 = new Dictionary<int, List<CalledAllele>>() {
                { newMNV.ReferencePosition, new List<CalledAllele>() {  newMNV } } };
            nbhd.Setup(n => n.CalledVariants).Returns(stagedCalledMNVs2);

            // If one has been sucked up all the way, we should output it as a nocall 
            // (but we have to statge it already as a no call allready, becasue the merger can't do the conversion.
            var stagedCalledRefs2 = new Dictionary<int, CalledAllele>() {
                { 123,  new CalledAllele(AlleleCategory.Reference) {ReferencePosition=123,Chromosome="chr1",ReferenceAllele="A",AlternateAllele="." }   },
                { 124,   new CalledAllele(AlleleCategory.Reference) { ReferencePosition = 124, Chromosome = "chr1", ReferenceAllele = "A", AlternateAllele = ".", Genotype= Genotype.RefLikeNoCall }  },
                  { 234,  new CalledAllele(AlleleCategory.Reference) { ReferencePosition = 234, Chromosome = "chr1", ReferenceAllele = "A", AlternateAllele = ".", Genotype= Genotype.HomozygousRef }  }
            };

            nbhd.Setup(n => n.CalledRefs).Returns(stagedCalledRefs2);


            var accepted = VcfMerger.GetMergedListOfVariants(nbhd.Object, stagedVcfVariants.ToList());


            Assert.Equal(5, accepted.Count);

            CheckVariantsMatch(vcfVariant0asRef, accepted[0].Item1);
            CheckVariantsMatch(vcfVariant2asNull, accepted[1].Item1);
            CheckVariantsMatch(newMNV, accepted[2].Item1);
            CheckVariantsMatch(vcfVariant3asRef, accepted[3].Item1);
            CheckVariantsMatch(originalVcfVariant4, accepted[4].Item1);

        }

        public static void CheckVariantsMatch(VcfVariant baseline, CalledAllele test)
        {
            Assert.Equal(baseline.ReferenceAllele, test.ReferenceAllele);
            Assert.Equal(baseline.VariantAlleles[0], test.AlternateAllele);
            Assert.Equal(baseline.VariantAlleles.Length, 1);
            Assert.Equal(baseline.ReferenceName, test.Chromosome);
            Assert.Equal(baseline.ReferencePosition, test.ReferencePosition);

            int numAlts = (baseline.VariantAlleles[0] == ".") ? 0 : baseline.VariantAlleles.Length;
            Assert.Equal(VcfVariantUtilities.MapGTString(baseline.Genotypes[0]["GT"], numAlts), test.Genotype);
        }

        public static void CheckVariantsMatch(CalledAllele baseline, CalledAllele test)
        {
            Assert.True(test.IsSameAllele(baseline));
            Assert.Equal(baseline.Genotype, test.Genotype);
        }
    }
}
