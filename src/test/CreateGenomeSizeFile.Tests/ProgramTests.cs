using System.IO;
using CommandLine.IO.Utilities;
using Xunit;

namespace CreateGenomeSizeFile.Tests
{
   
    public class ProgamTests
    {
        private string _existingGenomeFolder = Path.Combine(TestPaths.SharedGenomesDirectory, "Bacillus_cereus", "Sequence", "WholeGenomeFasta");


        /// <summary>
        ///The following tests check the new argument handling takes care of the following cases:
        ///(1) No arguments given
        ///(2) Version num requested 
        ///(3) unknown arguments given
        ///(4) missing required input (no vcf given)
        /// </summary>
        [Fact]
        public void CheckCommandLineArgumentHandling_noArguments()
        {
            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { }));

            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-v" }));

            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "--v" }));

            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-g", "foo.genome.bam", "-blah", "won't work" }));

            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-g", "folder", "-s", "Homo sapiens (UCSC hg19)", "-a", "wrong" }));

            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-blah", "won't work" }));

            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-debug", "true" }));
        }

        [Fact]
        public void CheckCommandLineArgumentHandling_MissingRequiredArguments()
        {
            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { "-g", "folder" }));

            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { "" }));
        }



        [Fact]
        public void CheckHappyPathExecution()
        {
            var outFolder = Path.Combine(TestPaths.LocalScratchDirectory, "BacillusTest");
            var observedOutGSFile = Path.Combine(outFolder, "GenomeSize.xml");
            var expectedOutGSFile = Path.Combine(TestPaths.LocalTestDataDirectory, "GenomeSize.xml");
            var observedOutDictFile = Path.Combine(outFolder, "genome.dict");
            var observedOutFaiFile = Path.Combine(outFolder, "genome.fa.fai");
           
            if (File.Exists(observedOutGSFile))
                File.Delete(observedOutGSFile);

            if (File.Exists(observedOutDictFile))
                File.Delete(observedOutDictFile);

            if (File.Exists(observedOutFaiFile))
                File.Delete(observedOutFaiFile);


            var aguments = new string[] {"-g", _existingGenomeFolder, "-S","Bacillus cereus ATCC 10987 (NCBI 2004-02-13)", "-o", outFolder };
            var exitCode =  Program.Main(aguments);

            Assert.Equal((int)ExitCodeType.Success, exitCode);
          
            TestUtilities.TestHelper.CompareFiles(observedOutGSFile, expectedOutGSFile);
            Assert.True(File.Exists(observedOutDictFile));
            Assert.True(File.Exists(observedOutFaiFile));
        }
    }
}
