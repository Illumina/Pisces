using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using BamStitchingLogic;
using Gemini.Interfaces;
using Gemini.Utility;
using Moq;
using Xunit;
using ReadNumber = Alignment.Domain.ReadNumber;

namespace Gemini.Tests
{
    public class GeminiWorkflowTests
    {
        [Fact]
        public void ProcessBam()
        {
            var stitcherOptions = new StitcherOptions()
            {

            };
            var geminiOptions = new GeminiOptions()
            {
                RegionSize = 1000,
            };


            var readPair1 = TestHelpers.GetPair("5M1I5M", "5M1I5M", name: "Pair1");
            var readPair2 = TestHelpers.GetPair("5M1I5M", "5M1I5M", read1Position: 1001, name: "Pair2");
            var readPair3 = TestHelpers.GetPair("5M1I5M", "5M1I5M", read1Position: 1201, name: "Pair3");
            var readPair4 = TestHelpers.GetPair("5M1I5M", "5M1I5M", read1Position: 10000, name: "Pair4");
            var reads = new List<ReadPair>() { readPair1, readPair2, readPair3, readPair4 };

            var read = TestHelpers.CreateBamAlignment("AAAAAAAATA", 999, 1001, 30, true,
                cigar: new CigarAlignment("10M"), name: "LonerPair1");
            read.SetIsProperPair(true);
            var lonerPair1Mate1 = new ReadPair(read, "LonerPair1");
            var read2 = TestHelpers.CreateBamAlignment("AAAAATAAAA", 1002, 999, 30, true,
                cigar: new CigarAlignment("10M"), name: "LonerPair1", isFirstMate: false);
            read2.SetIsProperPair(true);
            var lonerPair1Mate2 = new ReadPair(read2, "LonerPair1", readNumber: ReadNumber.Read2);

            var read3 = TestHelpers.CreateBamAlignment("AAAAAAAAAA", 999, 5001, 30, true,
                cigar: new CigarAlignment("10M"), name: "LonerPairFarApart");
            read3.SetIsProperPair(true);
            var read4 = TestHelpers.CreateBamAlignment("AAAAAAAAAA", 5001, 999, 30, true,
                cigar: new CigarAlignment("10M"), name: "LonerPairFarApart", isFirstMate: false);
            read4.SetIsProperPair(true);
            var lonerPair2Mate1 = new ReadPair(read3, name: "LonerPairFarApart");
            var lonerPair2Mate2 = new ReadPair(read4, name: "LonerPairFarApart", readNumber: ReadNumber.Read2);

            var lonerReads = new List<ReadPair>() { lonerPair1Mate1, lonerPair1Mate2, lonerPair2Mate1, lonerPair2Mate2 };
            var alignments = new List<BamAlignment>();

            Execute(alignments, reads, geminiOptions, stitcherOptions, lonerReads);
            Assert.Equal(1, alignments.Count(x => x.Name == "Pair1"));
            Assert.Equal(1, alignments.Count(x => x.Name == "Pair2"));
            Assert.Equal(1, alignments.Count(x => x.Name == "Pair3"));
            Assert.Equal(1, alignments.Count(x => x.Name == "Pair4"));
            Assert.Equal(1, alignments.Count(x => x.Name == "LonerPair1"));
            Assert.Equal(2, alignments.Count(x => x.Name == "LonerPairFarApart"));
            Assert.Equal(7, alignments.Count);

            alignments.Clear();

        }

        private static void Execute(List<BamAlignment> alignments, List<ReadPair> reads, GeminiOptions geminiOptions, StitcherOptions stitcherOptions, List<ReadPair> lonerpairs = null)
        {
            var mockOutcomesWriter = new Mock<IOutcomesWriter>();
            var mockDataOutputFactory = DataflowMocks.MockDataOutputFactory(alignments);
            var mockTextWriter = new Mock<ITextWriter>();
            mockDataOutputFactory.Setup(x => x.GetTextWriter(It.IsAny<string>()))
                .Returns(mockTextWriter.Object);
            var mockReader = DataflowMocks.MockReader();
            var mockReadPairSource = DataflowMocks.MockDataSource(reads, lonerpairs);
            var mockDataSourceFactory = DataflowMocks.MockDataSourceFactory(mockReader, mockReadPairSource);
            var mockSamtoolsWrapper = new Mock<ISamtoolsWrapper>();
            var geminiSampleOptions = new GeminiSampleOptions() {RefId = 1, OutputFolder = "OutFolder"};

            var geminiWorkflow = new GeminiWorkflow(mockDataSourceFactory.Object, mockDataOutputFactory.Object,
                geminiOptions, geminiSampleOptions, new RealignmentOptions(), stitcherOptions, "outdir",
                new RealignmentAssessmentOptions(), new IndelFilteringOptions(), mockSamtoolsWrapper.Object);
            geminiWorkflow.Execute();
        }
    }
}