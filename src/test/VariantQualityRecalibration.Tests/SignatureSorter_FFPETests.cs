using System.IO;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class SignatureSorter_FFPETests
    {
        [Fact]
        public void WriteCountsFile()
        {
            VQROptions options = new VQROptions()
            {
                InputVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "Test.vcf"),
                OutputDirectory = TestPaths.LocalScratchDirectory,
                LociCount = -1,
                DoBasicChecks = true,
                DoAmpliconPositionChecks = false
            };


            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Expected.counts");
            SignatureSorterResultFiles results = SignatureSorter.StrainVcf(options);
            string outFile = results.BasicCountsFilePath;
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

            VQROptions options = new VQROptions()
            {
                InputVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "Test.vcf"),
                OutputDirectory = TestPaths.LocalScratchDirectory,
                LociCount = 1000,
                DoBasicChecks = true,
                DoAmpliconPositionChecks = false
            };

            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "ExpectedGivenLociNum.counts");
         
            SignatureSorterResultFiles results = SignatureSorter.StrainVcf(options);
            string outFile = results.BasicCountsFilePath;


            Assert.True(File.Exists(outFile));

            var observedLines = File.ReadAllLines(outFile);
            var expectedLines = File.ReadAllLines(expectedPath);
            Assert.Equal(expectedLines.Length, observedLines.Length);

            for (int i = 0; i < expectedLines.Length; i++)
                Assert.Equal(expectedLines[i], observedLines[i]);

            File.Delete(outFile);
        }     

    }
}
