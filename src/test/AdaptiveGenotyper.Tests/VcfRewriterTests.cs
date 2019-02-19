using System.Collections.Generic;
using Pisces.Genotyping;
using TestUtilities;
using Xunit;
using System.IO;

namespace AdaptiveGenotyper.Tests
{
    public class VcfRewriterTests
    {
        private List<double[]> DefaultMeans, DefaultPriors;
        public VcfRewriterTests()
        {
            List<MixtureModelInput> temp = MixtureModel.ReadModelsFile(Path.Combine(TestPaths.LocalTestDataDirectory, "example.model"));
            DefaultMeans = new List<double[]>
            {
                temp[0].Means,
                temp[1].Means
            };
            DefaultPriors = new List<double[]>
            {
                temp[0].Weights,
                temp[1].Weights
            };


        }

        [Fact]
        public void RewriteMultiallelicTest()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "MultiAllelicVariantTest.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "MultiAllelicVariantTest.recal.vcf");
            var outPath = TestPaths.LocalScratchDirectory;
            RecalibratedVariantsCollection testCollection = new RecalibratedVariantsCollection();

            VcfRewriter rewriter = new VcfRewriter(new List<RecalibratedVariantsCollection>(), DefaultMeans, DefaultPriors);
            rewriter.Rewrite(vcfPath, outPath, "expectedCmdLine");
            string outFile = Path.Combine(outPath, "MultiAllelicVariantTest.recal.vcf");

            TestHelper.CompareFiles(outFile, expectedPath);

            File.Delete(outFile);
        }

        [Fact]
        public void RewriteDeletionTest()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "DeletionVariantTest.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "DeletionVariantTest.recal.vcf");
            var outPath = TestPaths.LocalScratchDirectory;

            VcfRewriter rewriter = new VcfRewriter(new List<RecalibratedVariantsCollection>(),
                DefaultMeans, DefaultPriors);
            rewriter.Rewrite(vcfPath, outPath, "expectedCmdLine");
            string outFile = Path.Combine(outPath, "DeletionVariantTest.recal.vcf");

            TestHelper.CompareFiles(outFile, expectedPath);
    
            File.Delete(outFile);
        }

        [Fact]
        public void RewriteSpecialDeletionTest()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "DeletionSpecialCaseTest.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "DeletionSpecialCaseTest.recal.vcf");
            var outPath = TestPaths.LocalScratchDirectory;

            VcfRewriter rewriter = new VcfRewriter(new List<RecalibratedVariantsCollection>(), DefaultMeans, DefaultPriors);
            rewriter.Rewrite(vcfPath, outPath, "expectedCmdLine");
            string outFile = Path.Combine(outPath, "DeletionSpecialCaseTest.recal.vcf");

            TestHelper.CompareFiles(outFile, expectedPath);

            File.Delete(outFile);
        }

        /*
        [Fact]
        public void UpdateVariantTest()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "VariantGTTest.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "VariantGTTest.recal.vcf");
            var outPath = TestPaths.LocalScratchDirectory;

            List<RecalibratedVariantsCollection> variants = new VariantDepthReader().GetVariantFrequencies(vcfPath);
            PrivateObject obj = new PrivateObject(variants[0]);
            variants[0].Categories = new int[] { 2, 0, 1, 2, 1 };
            variants[0].QScores = new int[] { 100, 50, 40, 40, 80 };
            variants[0].Gp = new List<float[]>
            {
                new float[] { 10.45f, 100.23f, 0 },
                new float[] { 0, 10.45f, 1000f },
                new float[] { 10.45f, 0, 55f },
                new float[] {2000f, 1000f, 0 },
                new float[] {22.22f, 0, 55.55f }
            };

            VcfRewriter rewriter = new VcfRewriter(variants, DefaultMeans, DefaultPriors);
            rewriter.Rewrite(vcfPath, outPath, "expectedCmdLine");
            string outFile = Path.Combine(outPath, "VariantGTTest.recal.vcf");

            Assert.True(File.Exists(outFile));
            var observedLines = File.ReadAllLines(outFile);
            var expectedLines = File.ReadAllLines(expectedPath);
            Assert.Equal(expectedLines.Length, observedLines.Length);

            for (int i = 0; i < expectedLines.Length; i++)
                Assert.Equal(expectedLines[i], observedLines[i]);            

            File.Delete(outFile);
        }
        */
        /*
        [Fact]
        public void UpdateSomaticToGermlineTest()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "VariantGTTest.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "VariantGTTestSom.vcf");
            var outPath = TestPaths.LocalScratchDirectory;

            VcfRewriter rewriter = new VcfRewriter(new List<RecalibratedVariantsCollection>(), new List<double[]> { new double[3] });
            rewriter.Rewrite(vcfPath, outPath, "expectedCmdLine");
            string outFile = Path.Combine(outPath, "VariantGTTest.recal.vcf");
            
            Assert.True(File.Exists(outFile));
            var observedLines = File.ReadAllLines(outFile);
            var expectedLines = File.ReadAllLines(expectedPath);
            Assert.Equal(expectedLines.Length, observedLines.Length);

            for (int i = 0; i < expectedLines.Length; i++)
                Assert.Equal(expectedLines[i], observedLines[i]);

            File.Delete(outFile);
        }*/
    }
}
