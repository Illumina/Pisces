using System;
using System.Collections.Generic;
using System.IO;
using Alignment.Domain.Sequencing;
using Alignment.IO.Sequencing;
using BamStitchingLogic;
using Common.IO.Sequencing;
using Gemini;
using Gemini.IO;
using Moq;
using StitchingLogic;
using TestUtilities;
using Xunit;

namespace Gemini.Tests
{

    // Apparently we need to add this dummy class to anchor the test paths, according to other test projects...
    public class AnchorPointForTestPaths { }
    public class TestPaths : BaseTestPaths<AnchorPointForTestPaths> { }

    public class BamWriterFactoryTests
    {
        [Fact]
        public void HappyPath()
        {
            //var bamFilePath = Path.Combine(TestPaths.SharedBamDirectory, "Chr17Chr19.bam");
            //Assert.True(File.Exists(bamFilePath));
            // TODO figure out how to access the shared bams

            var tempPath = $"TemporaryBamFile_{Guid.NewGuid()}.bam";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            using (var bamWriter = new BamWriter(tempPath, "header", new List<GenomeMetadata.SequenceMetadata>()))
            {
                bamWriter.WriteAlignment(TestHelpers.CreateBamAlignment("ATCG", 1, 10, 30, true));
            }

            var bamWriterFactory = new BamWriterFactory(1, tempPath);

            var tempPath2 = $"TemporaryBamFile_{Guid.NewGuid()}.bam";
            if (File.Exists(tempPath2))
            {
                File.Delete(tempPath2);
            }

            var bamWriterHandle = bamWriterFactory.CreateSingleBamWriter(tempPath2);
            bamWriterHandle.WriteAlignment(TestHelpers.CreateBamAlignment("ATCAG", 1, 10, 30, true));
            bamWriterHandle.WriteAlignment(null);

            using (var reader = new BamReader(tempPath2))
            {
                // TODO more specific?
                var header = reader.GetHeader();
                Assert.Contains("ID:Gemini", header);
                Assert.Contains("PN:Gemini", header);
            }

            File.Delete(tempPath);
            File.Delete(tempPath2);
        }
    }
    public class BamWriterHandleTests
    {
        [Fact]
        public void HappyPath()
        {
            var tempPath = $"TemporaryBamFile_{Guid.NewGuid()}.bam";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            using (var bamWriter = new BamWriter(tempPath, "header", new List<GenomeMetadata.SequenceMetadata>()))
            {
                var bamWriterHandle = new BamWriterHandle(bamWriter);
                bamWriterHandle.WriteAlignment(TestHelpers.CreateBamAlignment("ATCG", 1, 10, 30, true));
                bamWriterHandle.WriteAlignment(null);
            }

            Assert.True(File.Exists(tempPath));

            File.Delete(tempPath);

        }
    
    }

    public class GeminiDataSourceFactoryTests
    {
        [Fact]
        public void CreateReadPairSource()
        {
            var stitcherOptions = new StitcherOptions();
            var factory = new GeminiDataSourceFactory(stitcherOptions, "fakeGenomePath", false);

            var bamReader = new Mock<IBamReader>();
            var readPairSource = factory.CreateReadPairSource(bamReader.Object, new ReadStatusCounter());

            Assert.Equal(typeof(PairFilterReadPairSource), readPairSource.GetType());
            // TODO maybe I can do some asserts on the configuration of the readpair source by passing through some reads and checking result? that's pretty indirect though.
        }

        [Fact]
        public void CreateBamReader()
        {
            // Should return a fully functional bam reader
            var tempPath = $"TemporaryBamFile_{Guid.NewGuid()}.bam";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            using (var bamWriter = new BamWriter(tempPath, "header", new List<GenomeMetadata.SequenceMetadata>()))
            {
                var bamWriterHandle = new BamWriterHandle(bamWriter);
                bamWriterHandle.WriteAlignment(TestHelpers.CreateBamAlignment("ATCG", 8, 10, 30, true));
                bamWriterHandle.WriteAlignment(null);
            }

            Assert.True(File.Exists(tempPath));

            var stitcherOptions = new StitcherOptions();
            var factory = new GeminiDataSourceFactory(stitcherOptions, "fakeGenomePath", false);

            using (var bamReader = factory.CreateBamReader(tempPath))
            {
                BamAlignment alignment = new BamAlignment();
                var getNext = bamReader.GetNextAlignment(ref alignment, true);
                Assert.True(getNext);
                Assert.Equal(7, alignment.Position);
            }

            File.Delete(tempPath);
        }

    }

    public class GeminiTextWriterTests
    {
        [Fact]
        public void HappyPath()
        {
            var tempPath = $"TemporaryTextFile_{Guid.NewGuid()}.txt";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            using (var textWriter = new GeminiTextWriter(tempPath))
            {
                textWriter.WriteLine("This is a test");
                textWriter.WriteLine("See?");
            }

            Assert.True(File.Exists(tempPath));
            var lines = File.ReadAllLines(tempPath);
            Assert.Equal(2, lines.Length);
            Assert.Equal("This is a test", lines[0]);
            Assert.Equal("See?", lines[1]);

            File.Delete(tempPath);

        }
    }
}