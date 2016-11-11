using System;
using System.Collections.Generic;
using System.IO;
using Pisces.IO.Sequencing;
using TestUtilities;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void OpenLogTest()
        {
            var outDir = Path.Combine(UnitTestPaths.TestDataDirectory, "VQRoutDir");
            var options = new ApplicationOptions();
            options.OutputDirectory = outDir;
            options.LogFileName = "LogText.txt";
            Program.Init(options);

            Assert.True(Directory.Exists(outDir));
            Assert.True(File.Exists(Path.Combine(options.OutputDirectory, options.LogFileName)));
        }

    }
}
