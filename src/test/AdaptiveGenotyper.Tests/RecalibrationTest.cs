using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;
using Xunit;
using System.Linq;

namespace AdaptiveGenotyper.Tests
{
    public class RecalibrationTest
    {
        [Fact]
        public void RecalibrateTest()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "testData.PairRealigned.genome.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "testData.PairRealigned.genome.model");
            var vcfOutTestFile = Path.Combine(TestPaths.LocalTestDataDirectory, "testData.PairRealigned.recal.vcf");
            var outPath = TestPaths.LocalScratchDirectory;
            var vcfOutFile = Path.Combine(outPath, "testData.PairRealigned.recal.vcf");

            var options = new AdaptiveGtOptions
            {
                VcfPath = vcfPath,
                OutputDirectory = outPath
            };
            Recalibration recal = new Recalibration(options);
            recal.Recalibrate();

            // Check model file
            var outFile = Path.Combine(outPath, "testData.PairRealigned.genome.model");
            TestHelper.CompareFiles(outFile, expectedPath);

            // Check vcf file
            Assert.True(File.Exists(vcfOutFile));
            CompareVariants.AssertSameVariants_QScoreAgnostic(vcfOutFile, vcfOutTestFile);

            File.Delete(vcfOutFile);

            // Use model to generate vcf and check file
            recal = new Recalibration(options);
            recal.Recalibrate();

            Assert.True(File.Exists(vcfOutFile));
            CompareVariants.AssertSameVariants_QScoreAgnostic(vcfOutFile, vcfOutTestFile);

            File.Delete(outFile);
            File.Delete(vcfOutFile);
        }

    }
}
