using System.IO;
using Common.IO.Utility;
using Xunit;
using Pisces.Domain.Types;

namespace VariantQualityRecalibration.Tests
{
    public class VcfRewritingTests
    {


        [Fact]
        public void TestOnADiploidVcf()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "TestWithDiploidCalls.vcf");
            var countsPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Dirty.counts");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "RecalibrateDiploidVcf");
            var outFile = Path.Combine(outDir, "TestWithDiploidCalls.vcf.recal");
            var expectedFile = Path.Combine(TestPaths.LocalTestDataDirectory, "ExpectedDiploidCalls.vcf.recal");

            if (File.Exists(outFile))
                File.Delete(outFile);

            Logger.OpenLog(outDir, "RecalibrateDiploidCallsLog.txt", true);

            VQROptions options = new VQROptions
            {
                CommandLineArguments = new string[] { "-vcf", "TestWithDiploidCalls.vcf" },
               // FilterQScore = 30, <- set below
                ZFactor = 0,
                MaxQScore = 66,
                // BaseQNoise = 30, <- set below
                VcfPath = vcfPath,
                OutputDirectory = outDir,
            };

            options.BamFilterParams.MinimumBaseCallQuality = 30; //-1
            options.VariantCallingParams.MinimumVariantQScoreFilter = 30;
            options.VariantCallingParams.PloidyModel = PloidyModel.DiploidByThresholding;
            options.VcfWritingParams.AllowMultipleVcfLinesPerLoci = false;
            options.VariantCallingParams.AmpliconBiasFilterThreshold = null;

            SignatureSorterResultFiles resultFiles = new SignatureSorterResultFiles(countsPath, "foo.txt", "foo.txt");
            QualityRecalibration.Recalibrate(resultFiles, options);
            

            Logger.CloseLog();

            Assert.True(File.Exists(outFile));

            TestUtilities.TestHelper.CompareFiles(outFile, expectedFile);

            //redirect log incase any other thread is logging here.
            var SafeLogDir = TestPaths.LocalScratchDirectory;
            Logger.OpenLog(SafeLogDir, "DefaultLog.txt", true);
            Logger.CloseLog();

            //delete our log
            File.Delete(outFile);
        }

    }
}
