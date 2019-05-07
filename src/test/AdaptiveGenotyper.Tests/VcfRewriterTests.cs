using System.Collections.Generic;
using Pisces.Genotyping;
using TestUtilities;
using Xunit;
using System.IO;

namespace AdaptiveGenotyper.Tests
{
    public class AdaptiveGtWriterTests
    {
        private readonly RecalibrationResults DefaultResults;

        public AdaptiveGtWriterTests()
        {
            List<MixtureModelParameters> modelParameters = MixtureModel.ReadModelsFile(
                Path.Combine(TestPaths.LocalTestDataDirectory, "example.model"));

            DefaultResults = new RecalibrationResults
            {
                SnvResults = new RecalibrationResult
                {
                    Means = modelParameters[0].Means,
                    Priors = modelParameters[0].Priors,
                    Variants = new RecalibratedVariantsCollection()
                },
                IndelResults = new RecalibrationResult
                {
                    Means = modelParameters[1].Means,
                    Priors = modelParameters[1].Priors,
                    Variants = new RecalibratedVariantsCollection()
                }
            };
        }

        [Fact]
        public void RewriteMultiallelicTest()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "MultiAllelicVariantTest.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "MultiAllelicVariantTest.recal.vcf");
            var outPath = TestPaths.LocalScratchDirectory;

            var options = new AdaptiveGtOptions
            {
                VcfPath = vcfPath
            };

            AdaptiveGtWriter.RewriteVcf(vcfPath, outPath, options, DefaultResults);
            string outFile = Path.Combine(outPath, "MultiAllelicVariantTest.recal.vcf");

            Assert.True(File.Exists(outFile));
            CompareVariants.AssertSameVariants_QScoreAgnostic(outFile, expectedPath);

            File.Delete(outFile);
        }

        [Fact]
        public void RewriteDeletionTest()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "DeletionVariantTest.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "DeletionVariantTest.recal.vcf");
            var outPath = TestPaths.LocalScratchDirectory;

            var options = new AdaptiveGtOptions
            {
                VcfPath = vcfPath
            };

            AdaptiveGtWriter.RewriteVcf(vcfPath, outPath, options, DefaultResults);
            string outFile = Path.Combine(outPath, "DeletionVariantTest.recal.vcf");
            
            Assert.True(File.Exists(outFile));
            CompareVariants.AssertSameVariants_QScoreAgnostic(outFile, expectedPath);

            File.Delete(outFile);
        }

        [Fact]
        public void RewriteSpecialDeletionTest()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "DeletionSpecialCaseTest.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "DeletionSpecialCaseTest.recal.vcf");
            var outPath = TestPaths.LocalScratchDirectory;
            var options = new AdaptiveGtOptions
            {
                VcfPath = vcfPath
            };

            AdaptiveGtWriter.RewriteVcf(vcfPath, outPath, options, DefaultResults);
            string outFile = Path.Combine(outPath, "DeletionSpecialCaseTest.recal.vcf");
            
            Assert.True(File.Exists(outFile));
            CompareVariants.AssertSameVariants_QScoreAgnostic(outFile, expectedPath);          

            File.Delete(outFile);
        }
    }
}
