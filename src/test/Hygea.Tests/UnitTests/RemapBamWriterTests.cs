using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RealignIndels.Logic;
using Alignment.Domain.Sequencing;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models;
using Hygea.Tests;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class RemapBamWriterTests
    {
        private static string _bamFilePath = Path.Combine("TestData", "small_S1.bam");
        private static string _outputDirectory = Path.Combine(TestPaths.LocalScratchDirectory,"TestResults");
        private static string _outputFile = Path.Combine(_outputDirectory, "RemapBamWriterTests.bam");
        [Fact]
        public void HappyPath()
        {
            if ( !Directory.Exists(_outputDirectory)) Directory.CreateDirectory(_outputDirectory);
            if (File.Exists(_outputFile)) File.Delete(_outputFile);
            if (File.Exists(_outputFile + ".bai")) File.Delete(_outputFile + ".bai");

            var happyBamWriter = new RemapBamWriter(_bamFilePath, _outputFile, 10);

            happyBamWriter.Initialize();

            BamAlignment bam = CreateAlignment(10, 20, "Test");

            happyBamWriter.WriteRead(ref bam, false);

            happyBamWriter.FinishAll();

            happyBamWriter.FlushAllBufferedRecords();

            Assert.True(File.Exists(_outputFile));
            Assert.True(File.Exists(_outputFile + ".bai"));
        }
        private BamAlignment CreateAlignment(int position, int matePosition, string name)
        {
            return new BamAlignment
            {
                Bases = "ACGT",
                TagData = new byte[0], 
                Position = position - 1,
                MatePosition = matePosition - 1,
                Name = name,
                Qualities = new byte[0]
            };
        }
    }
}
