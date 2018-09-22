using System.IO;
using CommandLine.Util;
using System.Collections.Generic;
using Common.IO.Utility;
using RealignIndels;
using Xunit;

namespace Hygea.Tests
{
    public class ProgramTests
    {
        private string _existingBamPath = Path.Combine(TestPaths.LocalTestDataDirectory, "var123var35.bam");

        [Fact]
        public void OpenLogTest()
        {
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "HygeaTestsOutDir");
            var options = new HygeaOptions();
            options.OutputDirectory = outDir;

            Logger.OpenLog(options.OutputDirectory, "testLog.txt", true);
            Logger.CloseLog();
            Assert.True(Directory.Exists(outDir));
            Assert.True(File.Exists(Path.Combine(options.OutputDirectory, "testLog.txt")));

            //cleanup and redirect logging
            var SafeLogDir = TestPaths.LocalScratchDirectory;
            Logger.OpenLog(SafeLogDir, "DefaultLog.txt", true);
            Logger.CloseLog();
            Directory.Delete(outDir, true);
        }



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

            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-bam", "foo.genome.bam", "-blah", "won't work" }));

        }

        [Fact]
        public void CheckCommandLineArgumentHandling_MissingRequiredArguments()
        {
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-blah", "won't work" }));

            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { "-debug", "true" }));
        }

        [Fact]
        public void CheckCommandLineArgumentHandling_UnsupportedArguments()
        {          
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-bam", _existingBamPath, "-blah", "won't work" }));
        }

        [Fact]
        public void CheckCommandLineArgumentHandling_HappyPathTest()
        {

            var priorPath = Path.Combine(TestPaths.LocalTestDataDirectory, "priors.vcf");
            var genomePath = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");
            var outFolder = Path.Combine(TestPaths.LocalScratchDirectory, "HygeaHappyPath");

            var arguments = new string[] { "-bam", _existingBamPath, "-priorsFile",
                priorPath, "-genomefolders", genomePath , "-o", outFolder  };


            var expectedOutputFiles = new List<string>()
            {
                   Path.Combine(outFolder, "var123var35.bam"),
                   Path.Combine(outFolder, "var123var35.bam.bai"),
                   Path.Combine(outFolder, "HygeaLogs", "HygeaOptions.used.json"),
                   Path.Combine(outFolder, "HygeaLogs", "HygeaLog.txt")
            };

            foreach (var file in expectedOutputFiles)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }

            Assert.Equal((int)ExitCodeType.Success, Program.Main(arguments));
            Assert.True(Directory.Exists(outFolder));

            foreach (var file in expectedOutputFiles)
            {
                Assert.True(File.Exists(file)); 
            }

            foreach (var file in expectedOutputFiles)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }

    }
}
