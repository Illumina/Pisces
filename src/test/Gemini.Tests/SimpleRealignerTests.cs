//using System.Collections.Generic;
//using Alignment.Domain;
//using Alignment.Domain.Sequencing;
//using Alignment.IO;
//using Alignment.IO.Sequencing;
//using BamStitchingLogic;
//using Gemini;
//using Gemini.Interfaces;
//using Gemini.IO;
//using Gemini.Realignment;
//using Gemini.Tests;
//using Gemini.Types;
//using Moq;
//using StitchingLogic;
//using Xunit;

//namespace Gemini.Tests
//{
//    public class SimpleRealignerTests
//    {
//        // TODO something up with this test, it's hanging. Disable for now.
//        [Fact (Skip ="Hanging")]
//        public void Execute()
//        {
//            var readPairs = new List<ReadPair>();
//            var pair1 = TestHelpers.GetPair("10M", "10M");
//            var pair2 = TestHelpers.GetPair("11M", "11M");
//            readPairs.Add(pair1);
//            readPairs.Add(pair2);
//            var stagedReturns = new Dictionary<ReadPair, List<BamAlignment>>()
//            {
//                {pair1, new List<BamAlignment>() {pair1.Read1}}, // Did stitch
//                {pair2, new List<BamAlignment>() {pair2.Read1, pair2.Read2}} // Didn't stitch
//            };

//            TestExecute(readPairs, stagedReturns, 2, 3);

//            var pair3 = TestHelpers.GetPair("11M", "11M");
//            readPairs.Add(pair3);
//            stagedReturns.Add(pair3, new List<BamAlignment>(){pair3.Read1});
//            var pair4 = TestHelpers.GetPair("11M", "11M");
//            readPairs.Add(pair4);
//            stagedReturns.Add(pair4, new List<BamAlignment>() { pair4.Read1, pair4.Read2 });

//            TestExecute(readPairs, stagedReturns, 4, 6);
//        }

//        private static void TestExecute(List<ReadPair> readPairs, Dictionary<ReadPair, List<BamAlignment>> stagedReturns, int expectedReadPairsProcessed, int expectedResultReads)
//        {
//            var indelSource = new Mock<IChromosomeIndelSource>();
//            var dataSourceFactory = new Mock<IGeminiDataSourceFactory>();
//            var readPairSource = MockDataSource(readPairs);
//            dataSourceFactory.Setup(x => x.CreateReadPairSource(It.IsAny<IBamReader>(), It.IsAny<ReadStatusCounter>()))
//                .Returns(readPairSource.Object);

//            var dataOutputFactory = new Mock<IGeminiDataOutputFactory>();
//            var bamWriterFactory = new Mock<IBamWriterFactory>();
//            var mockBamWriter = new Mock<IBamWriterMultithreaded>();
//            var handles = new List<IBamWriterHandle>();
//            var mockBamHandle = new Mock<IBamWriterHandle>();
//            handles.Add(mockBamHandle.Object);
//            mockBamWriter.Setup(x => x.GenerateHandles()).Returns(handles);
//            bamWriterFactory.Setup(x => x.CreateBamWriter(It.IsAny<string>(), It.IsAny<int?>()))
//                .Returns(mockBamWriter.Object);
//            dataOutputFactory.Setup(x => x.GetBamWriterFactory(It.IsAny<string>())).Returns(bamWriterFactory.Object);
//            var realignerFactory = new Mock<IBamRealignmentFactory>();
//            var mockHandler = new Mock<IReadPairHandler>();
//            mockHandler.Setup(x => x.ExtractReads(It.IsAny<ReadPair>())).Returns<ReadPair>((r) => stagedReturns[r]);
//            realignerFactory.Setup(x => x.GetRealignPairHandler(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
//                    It.IsAny<Dictionary<int, string>>(), It.IsAny<ReadStatusCounter>(), It.IsAny<bool>(),
//                    It.IsAny<IChromosomeIndelSource>(), It.IsAny<string>(), It.IsAny<Dictionary<string, int[]>>(), It.IsAny<bool>(), It.IsAny<Dictionary<HashableIndel, int[]>>(), It.IsAny<bool>()))
//                .Returns(mockHandler.Object);
//            var realigner = new SimpleRealigner(new StitcherOptions(), indelSource.Object, "chr1",
//                dataSourceFactory.Object, dataOutputFactory.Object, realignerFactory.Object);

//            realigner.Execute("inbam", "outbam", true, true, true, true, false);
//            mockBamWriter.Verify(x => x.GenerateHandles(), Times.Once);
//            mockBamHandle.Verify(x => x.WriteAlignment(It.IsAny<BamAlignment>()), Times.Exactly(expectedResultReads));
//            mockHandler.Verify(x => x.ExtractReads(It.IsAny<ReadPair>()), Times.Exactly(expectedReadPairsProcessed));
//            mockHandler.Verify(x => x.Finish(), Times.Once);
//            mockBamWriter.Verify(x => x.Flush(), Times.Once);
//        }

//        private static Mock<IDataSource<ReadPair>> MockDataSource(List<ReadPair> readPairs)
//        {
//            var mockDataSource = new Mock<IDataSource<ReadPair>>();
//            var i = 0;
//            mockDataSource.Setup(x => x.GetNextEntryUntilNull())
//                .Returns(() => { return i < readPairs.Count ? readPairs[i] : null; }).Callback(() => i++);
//            return mockDataSource;
//        }
//    }
//}
