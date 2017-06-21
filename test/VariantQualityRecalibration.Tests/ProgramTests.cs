using System;
using System.Collections.Generic;
using System.IO;
using Pisces.IO.Sequencing;
using Common.IO.Utility;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void OpenLogTest()
        {
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "VQRoutDir");
            var options = new ApplicationOptions();
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

    }
}
