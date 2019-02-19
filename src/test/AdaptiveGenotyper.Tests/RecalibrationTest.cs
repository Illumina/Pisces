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
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "GM12877-1FT-r1_S17.stitched.sorted.genome.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "GM12877-1FT-r1_S17.stitched.sorted.genome.model");
            var vcfOutTestFile = Path.Combine(TestPaths.LocalTestDataDirectory, "GM12877-1FT-r1_S17.stitched.sorted.recal.vcf");
            var outPath = TestPaths.LocalScratchDirectory;
            var vcfOutFile = Path.Combine(outPath, "GM12877-1FT-r1_S17.stitched.sorted.recal.vcf");

            Recalibration recal = new Recalibration();
            recal.Recalibrate(vcfPath, outPath, null, "cmdArg");

            // Check model file
            var outFile = Path.Combine(outPath, "GM12877-1FT-r1_S17.stitched.sorted.genome.model");
            TestHelper.CompareFiles(outFile, expectedPath);

            // Check vcf file
            TestHelper.CompareFiles(vcfOutFile, vcfOutTestFile);

            File.Delete(outFile);
            File.Delete(vcfOutFile);
        }

    }
}
