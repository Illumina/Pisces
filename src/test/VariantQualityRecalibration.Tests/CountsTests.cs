using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class CountsTests
    {
        [Fact]
        public void WriteCountsFile()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Test.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Expected.counts");

            var outPath = TestPaths.LocalScratchDirectory;

            var outFile = Counts.WriteCountsFile(vcfPath, outPath, -1);

            Assert.True(File.Exists(outFile));

            var observedLines = File.ReadAllLines(outFile);
            var expectedLines = File.ReadAllLines(expectedPath);
            Assert.Equal(expectedLines.Length, observedLines.Length);

            for (int i =0; i<expectedLines.Length;i++)
                Assert.Equal(expectedLines[i], observedLines[i]);

            File.Delete(outFile);
        }

        [Fact]
        public void WriteCountsFileGivenLociCounts()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Test.vcf");
            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "ExpectedGivenLociNum.counts");

            var outPath = TestPaths.LocalScratchDirectory;

            var outFile = Counts.WriteCountsFile(vcfPath, outPath, 1000);

            Assert.True(File.Exists(outFile));

            var observedLines = File.ReadAllLines(outFile);
            var expectedLines = File.ReadAllLines(expectedPath);
            Assert.Equal(expectedLines.Length, observedLines.Length);

            for (int i = 0; i < expectedLines.Length; i++)
                Assert.Equal(expectedLines[i], observedLines[i]);

            File.Delete(outFile);
        }

        [Fact]
        public void ReadCountsFile()
        {
            var countsPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Expected.counts");

            var c = new Counts();
            c.LoadCountsFile(countsPath);

            Assert.Equal(10.0, c.NumPossibleVariants);
            Assert.Equal(0.0, c.CountsByCategory[MutationCategory.AtoC]);
            Assert.Equal(2.0, c.CountsByCategory[MutationCategory.CtoG]); //two 0/1 and one 0/.

        }
    }
}
