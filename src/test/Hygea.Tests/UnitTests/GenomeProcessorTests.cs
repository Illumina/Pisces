using System.Collections.Generic;
using System.IO;
using RealignIndels.Logic;
using RealignIndels.Logic.Processing;
using RealignIndels.Tests.Utilities;
using Pisces.IO;
using Pisces.Domain.Options;
using Moq;
using RealignIndels.Interfaces;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Hygea.Tests;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class GenomeProcessorTests
    {
        private static string _existingBamPath = Path.Combine(TestPaths.LocalTestDataDirectory, "GenomeProcessorTestData", "test_file.bam");
        private static string _existingBamFolder = Path.Combine(TestPaths.LocalTestDataDirectory, "GenomeProcessorTestData");
        private static string _existingGenome = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");
        private static string _outputFolder = Path.Combine(TestPaths.LocalScratchDirectory, "TestResults");
        private static string _outputFilePath = Path.Combine(_outputFolder, "test_file.bam");

        [Fact]
        public void ReadGenome()
        {
            Directory.CreateDirectory(_outputFolder);
            var options_1 = new HygeaOptions()
            {
                BAMPaths = BamProcessorOptions.UpdateBamPathsWithBamsFromFolder(_existingBamFolder),
                GenomePaths = new[] {_existingGenome},
                OutputDirectory = _outputFolder
            };
           
            var factory = new Factory(options_1);

            Assert.Equal(factory.GetOutputFile(_existingBamPath), _outputFilePath);

            // Run the genome processor using the filter for chr19, it will run through the IndelRealigner path as usual.
            var genome = new Genome(_existingGenome, new List<string>() {"chr19"});
            var gp1 = new GenomeProcessor(factory, genome, "chr19");
            gp1.Execute(1);

            var outputFilePath = Path.Combine(_outputFolder, Path.GetFileName(_existingBamPath));
            Assert.True(File.Exists(outputFilePath));
            Assert.NotEqual(new FileInfo(outputFilePath).Length, new FileInfo(_existingBamPath).Length);
            File.Delete(outputFilePath);

            // Run the genome processor using the filter for chr18 to follow the path in GenomeProcessor.Process 
            // for chromosomes outside the filter.
            var gp2 = new GenomeProcessor(factory, genome, "chr18");
            gp2.Execute(1);

            Assert.True(File.Exists(outputFilePath));
            Assert.NotEqual(new FileInfo(outputFilePath).Length, new FileInfo(_existingBamPath).Length);

        }

        [Fact]
        public void Flow()
        {
            var factory = GetMockFactory();

            var processor = new GenomeProcessor(factory, GetGenome().Object);
            processor.Execute(1);

            factory.MockWriter.Verify(w => w.Initialize(), Times.Exactly(1));
            factory.MockWriter.Verify(w => w.FlushAllBufferedRecords(), Times.Exactly(3));  // flush inbetween each chr
            factory.MockWriter.Verify(w => w.FinishAll(), Times.Exactly(1));

            factory.MockChrRealigner.Verify(r => r.Execute(), Times.Exactly(3));
        }

        [Fact]
        public void FlowWithMultipleBams()
        {
            var factory = GetMockFactory(2);

            var processor = new GenomeProcessor(factory, GetGenome().Object);
            processor.Execute(1);

            factory.MockWriter.Verify(w => w.Initialize(), Times.Exactly(2));
            factory.MockWriter.Verify(w => w.FlushAllBufferedRecords(), Times.Exactly(6));  // flush inbetween each chr
            factory.MockWriter.Verify(w => w.FinishAll(), Times.Exactly(2));

            factory.MockChrRealigner.Verify(r => r.Execute(), Times.Exactly(6));
        }

        [Fact]
        public void FlowWithChrFilter()
        {
            var factory = GetMockFactory();
            factory.MockAlignmentExtractor.Setup(e => e.GetNextAlignment(It.IsAny<Read>())).Returns(false);

            var processor = new GenomeProcessor(factory, GetGenome().Object, "chr2");
            processor.Execute(1);

            factory.MockWriter.Verify(w => w.Initialize(), Times.Exactly(1));
            factory.MockWriter.Verify(w => w.FlushAllBufferedRecords(), Times.Exactly(3));  // flush inbetween each chr
            factory.MockWriter.Verify(w => w.FinishAll(), Times.Exactly(1));

            factory.MockChrRealigner.Verify(r => r.Execute(), Times.Exactly(1));
            factory.MockAlignmentExtractor.Verify(r => r.GetNextAlignment(It.IsAny<Read>()), Times.Exactly(2));
        }

        private MockFactoryWithDefaults GetMockFactory(int numBams = 1)
        {
            var factory = new MockFactoryWithDefaults(new HygeaOptions()
            {
                BAMPaths = numBams == 1 ? new[] { "bamfile" } : new[] { "bamfile", "bamfile2" },
                GenomePaths = new[] { "someGenome" }
            });

            factory.MockWriter = new Mock<IRealignmentWriter>();
            factory.MockAlignmentExtractor = new Mock<IAlignmentExtractor>();
            factory.MockChrRealigner = new Mock<IChrRealigner>();

            return factory;
        }

        private Mock<IGenome> GetGenome()
        {
            var mockGenome = new Mock<IGenome>();
            mockGenome.Setup(g => g.Directory).Returns("someGenome");
            mockGenome.Setup(g => g.ChromosomesToProcess).Returns(new List<string>() { "chr1", "chr2", "chr3" });
            mockGenome.Setup(g => g.GetChrReference(It.IsAny<string>())).Returns(
                (string c) => { return new ChrReference() {Name = c}; });

            return mockGenome;
        }
    }
}
