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
                CommandLine = new[] { "myCommandLine" },
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
            var reader = new VcfReader(inputFilePath, true);


            //set up the original variants
            var originalVcfVariant1 = PhasedVariantTestUtilities.CreateDummyAllele("chr2", 116380048, "A", "New", 1000, 156);
            var originalVcfVariant2 = PhasedVariantTestUtilities.CreateDummyAllele("chr2", 116380048, "AAA", "New", 1000, 156);
            var originalVcfVariant4 = PhasedVariantTestUtilities.CreateDummyAllele("chr7", 116380051, "A", "New", 1000, 156);
            var originalVcfVariant5 = PhasedVariantTestUtilities.CreateDummyAllele("chr7", 116380052, "AC", "New", 1000, 156);

            var vs1 = new VariantSite((originalVcfVariant1));
            var vs2 = new VariantSite((originalVcfVariant2));
            var vs4 = new VariantSite((originalVcfVariant4));
            var vs5 = new VariantSite((originalVcfVariant5));


            //have to replace variants at positon 116380048 (we call two new MNVS here)
            var nbhd1 = new VcfNeighborhood(new VariantCallingParameters(), 0, "chr2", vs1, vs2, "");
            nbhd1.SetRangeOfInterest();

            //have to replace variants at positon 116380051 and 52  (we call one new MNV at 51)
            var nbhd2 = new VcfNeighborhood(new VariantCallingParameters(), 0, "chr7", vs4, vs5, "");
            nbhd2.SetRangeOfInterest();

         
            VcfMerger merger = new VcfMerger(reader);
            List<CalledAllele> allelesPastNbh = new List<CalledAllele>();

            nbhd1.CalledVariants = new Dictionary<int, List<CalledAllele>> { { originalVcfVariant1.ReferencePosition, new List<CalledAllele> {originalVcfVariant1, originalVcfVariant2 } } };
            nbhd2.CalledVariants = new Dictionary<int, List<CalledAllele>> { { originalVcfVariant4.ReferencePosition, new List<CalledAllele> {originalVcfVariant4 } } };


            allelesPastNbh = merger.WriteVariantsUptoChr(writer, allelesPastNbh, nbhd1.ReferenceName);

            allelesPastNbh = merger.WriteVariantsUptoIncludingNbhd(nbhd1, writer, allelesPastNbh);

            allelesPastNbh = merger.WriteVariantsUptoChr(writer, allelesPastNbh, nbhd2.ReferenceName);

            allelesPastNbh = merger.WriteVariantsUptoIncludingNbhd(nbhd2, writer, allelesPastNbh);

            merger.WriteRemainingVariants(writer, allelesPastNbh);

            writer.Dispose();

            var expectedLines = File.ReadLines(expectedFilePath).ToList();
            var outputLines = File.ReadLines(outputFilePath).ToList();

            Assert.Equal(expectedLines.Count(), outputLines.Count());

            for (int i=0;i<expectedLines.Count;i++)
                Assert.Equal(expectedLines[i], outputLines[i]);
        }

        [Fact]
        public void GetAcceptedVariants_MergeNull()
        {
            var originalVcfVariant = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 123, "A", "T", 1000, 156);
            var originalVcfVariant2 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 124, "A", "T", 1000, 156);
            var originalVcfVariant3 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 234, "A", "T", 1000, 156);
            var stagedVcfVariants = new List<CalledAllele> { originalVcfVariant, originalVcfVariant2, originalVcfVariant3 };
          
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

            var mockNeighborhood = new Mock<IVcfNeighborhood>();
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

         


            CheckVariantsMatch(originalVcfVariant, accepted[0]);
            CheckVariantsMatch(vcfVariant2asNull, accepted[1]);
            CheckVariantsMatch(originalVcfVariant3, accepted[2]);

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

            accepted = VcfMerger.GetMergedListOfVariants(mockNeighborhood.Object, stagedVcfVariants);


            Assert.Equal(3, accepted.Count);

            vcfVariant2asNull = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 124,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() { { "GT", "./." } } },
            };

            CheckVariantsMatch(originalVcfVariant, accepted[0]);
            CheckVariantsMatch(vcfVariant2asNull, accepted[1]);
            CheckVariantsMatch(originalVcfVariant3, accepted[2]);


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
            var originalVcfVariant = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 123, "A", "T", 1000, 156);
            var originalVcfVariant2 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 124, "A", "T", 1000, 156);
            var originalVcfVariant3 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 234, "A", "T", 1000, 156);
            var originalVcfVariant4 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 234, "A", "C", 1000, 156);

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

            var stagedVcfVariants = new List<CalledAllele> { originalVcfVariant, originalVcfVariant2, originalVcfVariant3, originalVcfVariant4 };
           
            var variantsUsedByCaller2 = new List<CalledAllele>() {originalVcfVariant, originalVcfVariant2, originalVcfVariant3 };

            var nbhd = new Mock<IVcfNeighborhood>();
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

            CheckVariantsMatch(vcfVariant0asRef, accepted[0]);
            CheckVariantsMatch(vcfVariant2asNull, accepted[1]);
            CheckVariantsMatch(newMNV, accepted[2]);
            CheckVariantsMatch(vcfVariant3asRef, accepted[3]);
            CheckVariantsMatch(originalVcfVariant4, accepted[4]);

        }

        public static void CheckVariantsMatch(VcfVariant baseline, CalledAllele test)
        {
            Assert.Equal(baseline.ReferenceAllele, test.ReferenceAllele);
            Assert.Equal(baseline.VariantAlleles[0], test.AlternateAllele);
            Assert.Equal(baseline.VariantAlleles.Length, 1);
            Assert.Equal(baseline.ReferenceName, test.Chromosome);
            Assert.Equal(baseline.ReferencePosition, test.ReferencePosition);

            int numAlts = (baseline.VariantAlleles[0] == ".") ? 0 : baseline.VariantAlleles.Length;
            Assert.Equal(Pisces.IO.Extensions.MapGTString(baseline.Genotypes[0]["GT"], numAlts), test.Genotype);
        }

        public static void CheckVariantsMatch(CalledAllele baseline, CalledAllele test)
        {
            Assert.True(test.IsSameAllele(baseline));
            Assert.Equal(baseline.Genotype, test.Genotype);
        }
    }
}
