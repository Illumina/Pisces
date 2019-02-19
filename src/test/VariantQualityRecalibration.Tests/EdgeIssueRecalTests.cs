using System.IO;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class EdgeIssueRecalTests
    {


        [Fact]
        public void RecalibrateDirtyVcfs()
        {

            var inputVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "TestEdgeExample.vcf");
            var outputDirectory = Path.Combine(TestPaths.LocalScratchDirectory, "RecalWithEdgeCounts");
            var outputVcf = Path.Combine(outputDirectory, "TestEdgeExample.vcf.recal");
            var expectedVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "ExpectedEdgeExample.vcf.recal");
            var logFile = Path.Combine(outputDirectory, "VQRLogs", "MyVQRLog.txt");
            var usedOptionsFile = Path.Combine(outputDirectory, "VQRLogs", "VQROptions.used.json");

            
            TestUtilities.TestHelper.RecreateDirectory(outputDirectory);
        
            // Note, here we are setting -alignmentwarningthreshold to 1. So its basically always going to go off. (The default is 10)
            Program.Main(new string[] { "-vcf", inputVcf, "-out", outputDirectory, "-dobasicchecks", "true",
                "-doampliconpositionchecks", "true", "-extentofedgeregion", "2", "-alignmentwarningthreshold", "1",
            "-log" ,"MyVQRLog.txt"});

            Assert.True(File.Exists(outputVcf));
            Assert.True(File.Exists(logFile));
            Assert.True(File.Exists(usedOptionsFile));

            TestUtilities.TestHelper.CompareFiles(outputVcf,expectedVcf);
           
        }
    }
    
}