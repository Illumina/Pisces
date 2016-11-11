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
            var vcfPath = Path.Combine(UnitTestPaths.TestDataDirectory, "Test.vcf");
            var expectedPath = Path.Combine(UnitTestPaths.TestDataDirectory, "Expected.counts");

            var outPath = UnitTestPaths.WorkingDirectory;
            var outFile = Counts.WriteCountsFile(vcfPath, outPath);

            Assert.True(File.Exists(outFile));

            var observedLines = File.ReadAllLines(outFile);
            var expectedLines = File.ReadAllLines(expectedPath);
            Assert.Equal(expectedLines.Length, observedLines.Length);

            for (int i =0; i<expectedLines.Length;i++)
                Assert.Equal(expectedLines[0], observedLines[0]);
        }

        [Fact]
        public void ReadCountsFile()
        {
            var countsPath = Path.Combine(UnitTestPaths.TestDataDirectory, "Expected.counts");

            var c = new Counts();
            c.LoadCountsFile(countsPath);

            Assert.Equal(4.0, c.NumPossibleVariants);
            Assert.Equal(0.0, c.CountsByCategory[MutationCategory.AtoC]);
            Assert.Equal(2.0, c.CountsByCategory[MutationCategory.CtoG]);
        }

      
    }
}
