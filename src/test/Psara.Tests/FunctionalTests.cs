using System.IO;
using TestUtilities;
using Xunit;

namespace Psara.Tests
{
    public class FunctionalTests
    {
        
        
        [Fact]
        public void ExecutionTest()
        {
            //example cmd line
            //-vcf MGL4-04_S1.genome.vcf -outfolder \\out -roi \\roi.txt
            string inputVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "PsaraTestInput.vcf");
            string inputgVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "PsaraTestInput.genome.vcf");
            string expectedOutVcf_default = Path.Combine(TestPaths.LocalTestDataDirectory, "Simple.filtered.vcf");
            string expectedOutgVcf_default = Path.Combine(TestPaths.LocalTestDataDirectory, "Simple.filtered.genome.vcf");
            string expectedOutVcf_expanded = Path.Combine(TestPaths.LocalTestDataDirectory, "Expanded.filtered.vcf");
            string expectedOutgVcf_expanded = Path.Combine(TestPaths.LocalTestDataDirectory, "Expanded.filtered.genome.vcf");
            string regionOfInterestFile = Path.Combine(TestPaths.LocalTestDataDirectory, "roi.txt");
            string outFolder = Path.Combine(TestPaths.LocalScratchDirectory, "PsaraExecutionTests");
            string logFolder = Path.Combine(outFolder, "PsaraLogs");

            string resultVcf = Path.Combine(outFolder, "PsaraTestInput.filtered.vcf");
            string resultgVcf = Path.Combine(outFolder, "PsaraTestInput.filtered.genome.vcf");
            string resultLog = Path.Combine(logFolder, "PsaraOptions.used.json");
            string resultOptions = Path.Combine(logFolder, "PsaraLog.txt");

            TestWithDefaults(inputVcf, inputgVcf, expectedOutVcf_default, expectedOutgVcf_default, regionOfInterestFile, outFolder, resultVcf, resultgVcf, resultLog, resultOptions);

            TestWithIntervalExpansionOption(inputVcf, inputgVcf, expectedOutVcf_expanded, expectedOutgVcf_expanded, regionOfInterestFile, outFolder, resultVcf, resultgVcf, resultLog, resultOptions);


        }

        public static void RunPsaraForTest(string[] args)
        {
            Program.Main(args);
        }

        private static void TestWithDefaults(string inputVcf, string inputgVcf, string expectedOutVcf, string expectedOutgVcf, string regionOfInterestFile, string outFolder, string resultVcf, string resultgVcf, string resultLog, string resultOptions)
        {
            Cleanup(outFolder, resultVcf, resultgVcf, resultLog, resultOptions);

            //test a .vcf
            var args = new string[] { "-vcf", inputVcf, "-outfolder", outFolder, "-roi", regionOfInterestFile };
            RunPsaraForTest(args);
            TestHelper.CompareFiles(resultVcf, expectedOutVcf);
           
            //test a .genome.vcf
            args = new string[] { "-vcf", inputgVcf, "-outfolder", outFolder, "-roi", regionOfInterestFile };
            RunPsaraForTest(args);
            TestHelper.CompareFiles(resultgVcf, expectedOutgVcf);

            Assert.True(File.Exists(resultLog));
            Assert.True(File.Exists(resultOptions));
        }

        private static void TestWithIntervalExpansionOption(string inputVcf, string inputgVcf, string expectedOutVcf, string expectedOutgVcf, string regionOfInterestFile, string outFolder, string resultVcf, string resultgVcf, string resultLog, string resultOptions)
        {
            Cleanup(outFolder, resultVcf, resultgVcf, resultLog, resultOptions);

            //test a .vcf
            var args = new string[] { "-vcf", inputVcf, "-outfolder", outFolder, "-roi", regionOfInterestFile, "-inclusionmodel" , "expand" };
            RunPsaraForTest(args);
            TestHelper.CompareFiles(resultVcf, expectedOutVcf);

            //test a .genome.vcf
            args = new string[] { "-vcf", inputgVcf, "-outfolder", outFolder, "-roi", regionOfInterestFile, "-inclusionmodel", "expand" };
            RunPsaraForTest(args);
            TestHelper.CompareFiles(resultgVcf, expectedOutgVcf);

            Assert.True(File.Exists(resultLog));
            Assert.True(File.Exists(resultOptions));
        }

        private static void Cleanup(string outFolder, string resultVcf, string resultgVcf, string resultLog, string resultOptions)
        {
            if (!Directory.Exists(outFolder))
                Directory.CreateDirectory(outFolder);

            if (File.Exists(resultVcf))
                File.Delete(resultVcf);

            if (File.Exists(resultgVcf))
                File.Delete(resultgVcf);

            if (File.Exists(resultLog))
                File.Delete(resultLog);

            if (File.Exists(resultOptions))
                File.Delete(resultOptions);
        }
    }
}
