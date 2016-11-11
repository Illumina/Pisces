using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.IO.Sequencing;
using TestUtilities;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.IO.Tests.UnitTests
{
    public class ExtensionsTests
    {
        [Fact]
        public void GetVariantsByChromosome()
        {
            var vcfReader =
                new VcfReader(Path.Combine(UnitTestPaths.TestDataDirectory, "VcfReader_Extensions.vcf"));

            //Simple case
            var output = vcfReader.GetVariantsByChromosome(true, true,
                new List<AlleleCategory> {AlleleCategory.Insertion, AlleleCategory.Mnv});
            Assert.Equal(1, output.Count);
            Assert.True(output.ContainsKey("chr1"));
            var candidateAlleles= new List<CandidateAllele>();
            output.TryGetValue("chr1", out candidateAlleles);
            Assert.Equal(2, candidateAlleles.Count);
            Assert.Equal(AlleleCategory.Mnv, candidateAlleles[0].Type);
            Assert.Equal(AlleleCategory.Insertion, candidateAlleles[1].Type);

            //Custom rule
            var filteredVcfReader =
                new VcfReader(Path.Combine(UnitTestPaths.TestDataDirectory, "VcfReader_Extensions.vcf"));
            var filteredOutput = filteredVcfReader.GetVariantsByChromosome(true, true,
                new List<AlleleCategory> { AlleleCategory.Insertion, AlleleCategory.Mnv }, candidate => candidate.Reference.Length > 3);
            Assert.Equal(1, filteredOutput.Count);
            Assert.True(filteredOutput.ContainsKey("chr1"));
            var filteredCandidateAlleles = new List<CandidateAllele>();
            filteredOutput.TryGetValue("chr1", out filteredCandidateAlleles);
            Assert.Equal(1, filteredCandidateAlleles.Count);
            Assert.False(filteredCandidateAlleles.Any(c => c.Reference.Length > 3));

        }


        [Fact]
        public void IsSingleThresholdTests()
        {
            CheckSingleThresholdValue("q30", 1, 30, true);
            CheckSingleThresholdValue("Q20", 1, 20, true);
            CheckSingleThresholdValue("Q20", 2, 0, true);
            CheckSingleThresholdValue("freq20", 4, 20, true);
            CheckSingleThresholdValue("r8", 1, 8, true);
            CheckSingleThresholdValue("xxx?73", 4, 73, true);
            CheckSingleThresholdValue("-10", 0, -10, true);
            CheckSingleThresholdValue("xxx?73", 5, 3, true);
            CheckSingleThresholdValue("xxx?.3", 5, 3, true);

            CheckSingleThresholdValue("r5x9", 1, -1, false);
            CheckSingleThresholdValue("R5x9", 1, -1, false);
            CheckSingleThresholdValue("R5X9", 1, -1, false);

            CheckSingleThresholdValue("q30.3", 1, -1, false);
            CheckSingleThresholdValue("Q20", 0, -1, false);
            CheckSingleThresholdValue("freq20", 1, -1, false);
            CheckSingleThresholdValue("r8", 0, -1, false);
            CheckSingleThresholdValue("r8", 2, -1, false);
            CheckSingleThresholdValue("xxx?73", 3, -1, false);
            CheckSingleThresholdValue("-10xxx", 0, -1, false);
            CheckSingleThresholdValue("-10xxx36", 0, -1, false);
            CheckSingleThresholdValue("", 0, -1, false);
            CheckRMxN("r5x" + ((long)int.MaxValue + 1), -1, -1, false);
        }

        [Fact]
        public void IsRMxNTests()
        {
            CheckRMxN("r5x9", 5,9, true);
            CheckRMxN("R5x9", 5, 9, true);
            CheckRMxN("R5X9", 5, 9, true);
            CheckRMxN("r5x9123", 5, 9123, true);
            CheckRMxN("r5123x9", 5123, 9, true);
            CheckRMxN("r-5123x-9", -5123, -9, true);
            CheckRMxN("r0x0", 0, 0, true);

            CheckRMxN("rr5x9", -1, -1, false);
            CheckRMxN("r5L9", -1, -1, false);
            CheckRMxN("L5r9", -1, -1, false);
            CheckRMxN("r5x9x12", -1, -1, false);
            CheckRMxN("r5xx9", -1, -1, false);
            CheckRMxN("r5.1xx9.0", -1, -1, false);
            CheckRMxN("r5x" + ((long)int.MaxValue + 1), -1, -1, false);
        }

        private static void CheckSingleThresholdValue(string filter, int startPosition, int expectedInt, bool expectedWorked)
        {
            int m = int.MaxValue;
            var worked = Extensions.LookForSingleThresholdValue(startPosition, filter, out m);
            Assert.Equal(expectedInt, m);
            if (!expectedWorked)
                Assert.Equal(expectedInt, -1);
        }

        private static void CheckRMxN(string filter, int expectedM, int expectedN, bool expectedIsRMxN)
        {
            int m = -1;
            int n = -1;
            var worked1 = Extensions.IsRMxN(filter);
            var worked2 = Extensions.IsRMxN(filter, out m, out n);
            Assert.Equal(expectedM, m);
            Assert.Equal(expectedN, n);
            Assert.Equal(expectedIsRMxN, worked1);
            Assert.Equal(expectedIsRMxN, worked2);
        }


        [Fact]
        public void MapGT()
        {
            var numTestsNeeded = 9;

            Assert.Equal(Genotype.Alt12LikeNoCall,Extensions.MapGTString("./.",2));
            Assert.Equal(Genotype.AltAndNoCall, Extensions.MapGTString("1/.", 1));
            Assert.Equal(Genotype.AltLikeNoCall, Extensions.MapGTString("./.", 1));
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, Extensions.MapGTString("1/2", 2));
            Assert.Equal(Genotype.HeterozygousAltRef, Extensions.MapGTString("0/1", 1));
            Assert.Equal(Genotype.HomozygousAlt, Extensions.MapGTString("1/1", 1));
            Assert.Equal(Genotype.HomozygousRef, Extensions.MapGTString("0/0", 0));
            Assert.Equal(Genotype.RefAndNoCall, Extensions.MapGTString("0/.", 0));
            Assert.Equal(Genotype.RefLikeNoCall, Extensions.MapGTString("./.", 0));

            //sanity check we covered all the possibilities.
            Assert.Equal(Enum.GetValues(typeof(Genotype)).Length, numTestsNeeded);
        }

        [Fact]
        public void UnpackAlleles()
        {
            //two example vcf files that have been "crushed".
            var crushedVcf1 = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_Crushed_Padded_expected.vcf");
            var crushedVcf2 = Path.Combine(UnitTestPaths.TestDataDirectory, "crushed.genome.vcf");

            var vcfVariants1 = VcfReader.GetAllVariantsInFile(crushedVcf1);
            var vcfVariants2 = VcfReader.GetAllVariantsInFile(crushedVcf2);

            Assert.Equal(7, vcfVariants1.Count);
            Assert.Equal(90, vcfVariants2.Count);

            // 1/2 variants
            var hetAlt1 = vcfVariants1[5];
            var hetAlt2 = vcfVariants2[3];
            var hetAlt1next = vcfVariants1[6];
            var hetAlt2next = vcfVariants2[4];

            Assert.Equal(1, hetAlt1.Genotypes.Count);
            Assert.Equal(1, hetAlt2.Genotypes.Count);
            Assert.Equal(2, hetAlt1.VariantAlleles.Count());
            Assert.Equal(2, hetAlt2.VariantAlleles.Count());
            Assert.Equal("2387,2000", hetAlt1.Genotypes[0]["AD"]);
            Assert.Equal("0.8133", hetAlt1.Genotypes[0]["VF"]);
            Assert.Equal("254,254", hetAlt2.Genotypes[0]["AD"]);
            Assert.Equal("AA", hetAlt1.ReferenceAllele);
            Assert.Equal("GA", hetAlt1.VariantAlleles[0]);
            Assert.Equal("G", hetAlt1.VariantAlleles[1]);
            Assert.Equal(".", hetAlt1next.VariantAlleles[0]);
            Assert.Equal("0", hetAlt1next.Genotypes[0]["AD"]);
            Assert.Equal("532", hetAlt2next.Genotypes[0]["AD"]);
            Assert.Equal(10, hetAlt1.ReferencePosition);
            Assert.Equal(223906731, hetAlt2.ReferencePosition);
            Assert.Equal(10+1, hetAlt1next.ReferencePosition);
            Assert.Equal(223906731+1, hetAlt2next.ReferencePosition);

            var unpackedVariants1 = Extensions.UnpackVariants(vcfVariants1);
            var unpackedVariants2 = Extensions.UnpackVariants(vcfVariants2);

            Assert.Equal(8, unpackedVariants1.Count);
            Assert.Equal(91, unpackedVariants2.Count);

            hetAlt1 = unpackedVariants1[5];
            hetAlt2 = unpackedVariants2[3];
            hetAlt1next = unpackedVariants1[6];
            hetAlt2next = unpackedVariants2[4];

            //example one:
            //total depth = 5394, total variant count = 2387 + 2000 = 4387
            //so, ref counts ~1007.

            //example two:
            //total depth = 532, total variant count = 254 + 254 = 508
            //so, ref counts ~24.

            Assert.Equal(1, hetAlt1.Genotypes.Count);
            Assert.Equal(1, hetAlt2.Genotypes.Count);
            Assert.Equal("1007,2387", hetAlt1.Genotypes[0]["AD"]);
            Assert.Equal("24,254", hetAlt2.Genotypes[0]["AD"]);
            Assert.Equal("0.4425", hetAlt1.Genotypes[0]["VF"]);
            Assert.Equal(1, hetAlt1.VariantAlleles.Count());
            Assert.Equal(1, hetAlt2.VariantAlleles.Count());
            Assert.Equal(1, hetAlt1next.VariantAlleles.Count());
            Assert.Equal(1, hetAlt2next.VariantAlleles.Count());
            Assert.Equal("1007,2000", hetAlt1next.Genotypes[0]["AD"]);
            Assert.Equal("24,254", hetAlt2next.Genotypes[0]["AD"]);
            Assert.Equal("AA", hetAlt1.ReferenceAllele);
            Assert.Equal("GA", hetAlt1.VariantAlleles[0]);
            Assert.Equal("G", hetAlt1next.VariantAlleles[0]);
            Assert.Equal("0.3708", hetAlt1next.Genotypes[0]["VF"]);
            Assert.Equal(10, hetAlt1.ReferencePosition);
            Assert.Equal(223906731, hetAlt2.ReferencePosition);
            Assert.Equal(10, hetAlt1next.ReferencePosition);
            Assert.Equal(223906731, hetAlt2next.ReferencePosition);

        }


    }
}