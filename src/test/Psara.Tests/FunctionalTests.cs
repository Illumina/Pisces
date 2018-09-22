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
            string inputCrushedVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "GermlinePhasedInput.vcf");
            string expectedOutVcf_default = Path.Combine(TestPaths.LocalTestDataDirectory, "Simple.filtered.vcf");
            string expectedOutgVcf_default = Path.Combine(TestPaths.LocalTestDataDirectory, "Simple.filtered.genome.vcf");
            string expectedOutVcf_expanded = Path.Combine(TestPaths.LocalTestDataDirectory, "Expanded.filtered.vcf");
            string expectedOutgVcf_expanded = Path.Combine(TestPaths.LocalTestDataDirectory, "Expanded.filtered.genome.vcf");
            string expectedOutpVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "GermlinePhasedOutput.filtered.vcf");
            string regionOfInterestFile1 = Path.Combine(TestPaths.LocalTestDataDirectory, "roi.txt");
            string regionOfInterestFile2 = Path.Combine(TestPaths.LocalTestDataDirectory, "roi2.txt");
            string outFolder = Path.Combine(TestPaths.LocalScratchDirectory, "PsaraExecutionTests");
            string logFolder = Path.Combine(outFolder, "PsaraLogs");

            string resultVcf = Path.Combine(outFolder, "PsaraTestInput.filtered.vcf");
            string resultgVcf = Path.Combine(outFolder, "PsaraTestInput.filtered.genome.vcf");
            string resultpVcf = Path.Combine(outFolder, "GermlinePhasedInput.filtered.vcf");
            string resultLog = Path.Combine(logFolder, "PsaraOptions.used.json");
            string resultOptions = Path.Combine(logFolder, "PsaraLog.txt");

            TestWithDefaults(inputVcf, inputgVcf, expectedOutVcf_default, expectedOutgVcf_default, regionOfInterestFile1, outFolder, resultVcf, resultgVcf, resultLog, resultOptions);

            TestWithIntervalExpansionOption(inputVcf, inputgVcf, expectedOutVcf_expanded, expectedOutgVcf_expanded, regionOfInterestFile1, outFolder, resultVcf, resultgVcf, resultLog, resultOptions);

            TestWithCrushedInput(inputCrushedVcf, expectedOutpVcf, regionOfInterestFile2, outFolder, resultpVcf, resultLog, resultOptions);

        }


        private static void TestWithDefaults(string inputVcf, string inputgVcf, string expectedOutVcf, string expectedOutgVcf, string regionOfInterestFile, string outFolder, string resultVcf, string resultgVcf, string resultLog, string resultOptions)
        {
            Cleanup(outFolder, resultVcf, resultgVcf, resultLog, resultOptions);

            //test a somatic .vcf
            var args = new string[] { "-vcf", inputVcf, "-outfolder", outFolder, "-roi", regionOfInterestFile };
            Program.Main(args);
            TestHelper.CompareFiles(resultVcf, expectedOutVcf);
           
            //test a somatic .genome.vcf
            args = new string[] { "-vcf", inputgVcf, "-outfolder", outFolder, "-roi", regionOfInterestFile };
            Program.Main(args);
            TestHelper.CompareFiles(resultgVcf, expectedOutgVcf);

          
            Assert.True(File.Exists(resultLog));
            Assert.True(File.Exists(resultOptions));
        }

        private static void TestWithIntervalExpansionOption(string inputVcf, string inputgVcf, string expectedOutVcf, string expectedOutgVcf, string regionOfInterestFile, string outFolder, string resultVcf, string resultgVcf, string resultLog, string resultOptions)
        {
            Cleanup(outFolder, resultVcf, resultgVcf, resultLog, resultOptions);

            //test a .vcf
            var args = new string[] { "-vcf", inputVcf, "-outfolder", outFolder, "-roi", regionOfInterestFile, "-inclusionmodel" , "expand" };
            Program.Main(args);
            TestHelper.CompareFiles(resultVcf, expectedOutVcf);

            //test a .genome.vcf
            args = new string[] { "-vcf", inputgVcf, "-outfolder", outFolder, "-roi", regionOfInterestFile, "-inclusionmodel", "expand" };
            Program.Main(args);
            TestHelper.CompareFiles(resultgVcf, expectedOutgVcf);

            Assert.True(File.Exists(resultLog));
            Assert.True(File.Exists(resultOptions));
        }


        private static void TestWithCrushedInput(string inputVcf, string expectedOutVcf, string regionOfInterestFile, string outFolder, string resultVcf, string resultLog, string resultOptions)
        {
            Cleanup(outFolder, resultVcf, resultVcf, resultLog, resultOptions);

            //test a diploid crushed vcf that was output by Scylla 
            var args = new string[] { "-vcf", inputVcf, "-outfolder", outFolder, "-roi", regionOfInterestFile };
            Program.Main(args);
            TestHelper.CompareFiles(resultVcf, expectedOutVcf);

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
