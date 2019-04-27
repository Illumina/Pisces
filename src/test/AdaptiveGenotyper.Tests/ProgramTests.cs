using System.IO;
using CommandLine.Util;
using Common.IO.Utility;
using Xunit;

namespace AdaptiveGenotyper.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void OpenLogTest()
        {
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "GQRoutDir");
            var options = new AdaptiveGtOptions();
            options.OutputDirectory = outDir;
            options.LogFileName = "LogText.txt";

            Logger.OpenLog(options.OutputDirectory, options.LogFileName, true);
            Logger.CloseLog();
            Assert.True(Directory.Exists(outDir));
            Assert.True(File.Exists(Path.Combine(options.OutputDirectory, options.LogFileName)));

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

            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-vcf", "foo.genome.vcf", "-blah", "won't work" }));

        }

        [Fact]
        public void CheckCommandLineArgumentHandling_MissingRequiredArguments()
        {
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-blah", "won't work" }));

            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { "-models", "my.model" }));
        }

        [Fact]
        public void CheckCommandLineArgumentHandling_UnsupportedArguments()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "example_S1.genome.vcf");

            // Note, if the unit tests paths have gotten mangled, this will show as error code 2, file not found.
            //  FileNotFound = 2,

            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-vcf", vcfPath, "-blah", "won't work" }));

        }
    }
}
