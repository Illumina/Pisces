using System.IO;
using System.Collections.Generic;
using CommandLine.Util;
using Common.IO.Utility;
using Xunit;

namespace Stitcher.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void OpenLogTest()
        {
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "StitcherTestsOutDir");
            var options = new StitcherApplicationOptions();
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


        [Fact]
        public void CheckCommandLineArgumentHandling_noArguments()
        {
            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { }));

            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-v" }));

            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "--v" }));

            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] {  }));

        }

        [Fact]
        public void CheckCommandLineArgumentHandling_MissingRequiredArguments()
        {
           
            var outFolder = Path.Combine(TestPaths.LocalScratchDirectory, "StitcherHappyPath");

            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { "-OutFolder", outFolder }));
        }

        [Fact]
        public void CheckCommandLineArgumentHandling_UnsupportedArguments()
        {
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-blah", "won't work" }));

            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.genome.vcf");
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-vcf", vcfPath, "-blah", "won't work" }));

        }

        [Fact]
        public void CheckCommandLineArgumentHandling_HappyPathTest()
        {
            //test #1: user-supplied output dir 
            var bamPath = Path.Combine(TestPaths.SharedBamDirectory, "Bcereus_S4.bam");
            var outFolder = Path.Combine(TestPaths.LocalScratchDirectory, "StitcherHappyPath");

            var arguments = new string[] { "-bam", bamPath, "-OutFolder", outFolder };


            var expectedOutputFiles = new List<string>()
            {
                   Path.Combine(outFolder, "Bcereus_S4.stitched.bam"),
                   Path.Combine(outFolder, "StitcherLogs", "StitcherOptions.used.json"),
                   Path.Combine(outFolder, "StitcherLogs", "StitcherLog.txt")
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


            //test #2: Output dir derived from input dir 

            var outFolder2 = Path.Combine(TestPaths.LocalScratchDirectory, "StitcherHappyPath_NoOutFolder");
            if (!Directory.Exists(outFolder2))
                Directory.CreateDirectory(outFolder2);

            var bamPath2 = Path.Combine(outFolder2, "Bcereus_S4.bam");
            if (!File.Exists(bamPath2))
                File.Copy(bamPath, bamPath2);


            arguments = new string[] { "-bam", bamPath2 }; //note, no outfolder given


            expectedOutputFiles = new List<string>()
            {
                   Path.Combine(outFolder2, "Bcereus_S4.stitched.bam"),
                   Path.Combine(outFolder2, "StitcherLogs", "StitcherOptions.used.json"),
                   Path.Combine(outFolder2, "StitcherLogs", "StitcherLog.txt")
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


            //test #3: Overriding log folder name

            var outFolder3 = Path.Combine(TestPaths.LocalScratchDirectory, "StitcherHappyPath_LogOverride");
            if (!Directory.Exists(outFolder3))
                Directory.CreateDirectory(outFolder3);
            var userLogName = "SnoopDogLog.McLog";

            arguments = new string[] { "-bam", bamPath, "-OutFolder", outFolder3, "-LogFileName" , userLogName };


            expectedOutputFiles = new List<string>()
            {
                   Path.Combine(outFolder3, "Bcereus_S4.stitched.bam"),
                   Path.Combine(outFolder3, "StitcherLogs", "StitcherOptions.used.json"),
                   Path.Combine(outFolder3, "StitcherLogs", userLogName)
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
