using System.IO;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class SignatureSorter_AlignmentIssueTests
    {
        [Fact]
        public void WriteEdgeCountsFile()
        {
            /*
        chr10   7577500.C   G  //on edge (within 2 bases of chr10 amplicon end)
        chr10   7577501.C   G  //on edge (within 2 bases of chr10 amplicon end)
        chr17   7577537.C   G   100 PASS DP = 10  //on edge (within 2 bases of chr17 amplicon start)
        chr17   7577538.C   G   100 PASS DP = 11  //on edge (within 2 bases of chr17 amplicon start)
        chr17   7577540.G   A   100 PASS DP = 13   //NOT on edge
        chr17   7577541.T   A   100 PASS DP = 100  //on edge (within 2 bases of edge of hat distribution)
        chr17   7577542.G   T   100 PASS DP = 101   //on edge (within 2 bases of edge of hat distribution)
        chr17   7577543.T   C   100 PASS DP = 100  //on edge (within 2 bases of edge of hat distribution)
        chr17   7577544.T   C   100 PASS DP = 56   //on edge (within 2 bases of edge of hat distribution)
        chr17   7577549.C.   100 PASS DP = 10  //NOT on edge
        chr17   7577550.C.   100 PASS DP = 10  //NOT on edge
        chr17   7577552.C   A   100 PASS DP = 15 //NOT on edge
        chr17   7577553.T   C   100 PASS DP = 16 //on edge (within 2 bases of edge of hat distribution)
        chr17   7577554.C.   100 PASS DP = 3700  //on edge (within 2 bases of edge of hat distribution), BUT its a referece call so we dont count it
        chr17   7577555.C.   100 PASS DP = 0    //NOT on edge (DP=0)
        chr17   7577556.C   A   100 PASS DP = 0  //NOT on edge (DP=0)
        chr17   7577557.C   A   100 PASS DP = 0 //NOT on edge (DP=0)
        chr17   7577558.C   A   100 PASS DP = 0   //NOT on edge (DP=0)
        chr20   7577554.C   G   100 PASS DP = 3700 //on edge (within 2 bases of edge of hat distribution)
        chr20   7577555.C.   100 PASS DP = 0    //NOT on edge (DP=0)
          */


            VQROptions options = new VQROptions()
            {
                VcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "FindEdges.vcf"),
                OutputDirectory = Path.Combine(TestPaths.LocalScratchDirectory, "WriteEdgeCountsFile"),
                LociCount = -1,
                DoBasicChecks = false,
                DoAmpliconPositionChecks = true,
                ExtentofEdgeRegion = 2
            };


            var expectedCountsFile = Path.Combine(TestPaths.LocalTestDataDirectory, "Expected.edgecounts");
            var expectedIssuesFile = Path.Combine(TestPaths.LocalTestDataDirectory, "Expected.edgevariants");

            TestUtilities.TestHelper.RecreateDirectory(options.OutputDirectory);

            SignatureSorterResultFiles results = SignatureSorter.StrainVcf(options);

            TestUtilities.TestHelper.CompareFiles(results.AmpliconEdgeCountsFilePath, expectedCountsFile);
            TestUtilities.TestHelper.CompareFiles(results.AmpliconEdgeSuspectListFilePath, expectedIssuesFile);

            TestUtilities.TestHelper.RecreateDirectory(options.OutputDirectory);
        }

        [Fact]
        public void WriteEdgeCountsFileGivenLociCounts()
        {

            VQROptions options = new VQROptions()
            {
                VcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "FindEdges.vcf"),
                OutputDirectory = Path.Combine(TestPaths.LocalScratchDirectory, "WriteEdgeCountsFileGivenLociCounts"),
                LociCount = 1000,
                DoBasicChecks = false,
                DoAmpliconPositionChecks = true,
                ExtentofEdgeRegion = 2
            };


            var expectedPath = Path.Combine(TestPaths.LocalTestDataDirectory, "ExpectedGivenLociNum.edgecounts");
            TestUtilities.TestHelper.RecreateDirectory(options.OutputDirectory);


            SignatureSorterResultFiles results = SignatureSorter.StrainVcf(options);
            string outFile = results.AmpliconEdgeCountsFilePath;

            TestUtilities.TestHelper.CompareFiles(results.AmpliconEdgeCountsFilePath, expectedPath);
            TestUtilities.TestHelper.RecreateDirectory(options.OutputDirectory);

        }

    }
}