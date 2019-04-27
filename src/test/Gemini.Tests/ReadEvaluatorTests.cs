//using System.Collections.Generic;
//using Alignment.Domain;
//using Alignment.Domain.Sequencing;
//using Alignment.IO;
//using Alignment.IO.Sequencing;
//using BamStitchingLogic;
//using Gemini.Interfaces;
//using Gemini.IO;
//using Gemini.Logic;
//using Gemini.Types;
//using Gemini.Utility;
//using Moq;
//using StitchingLogic;
//using Xunit;

//namespace Gemini.Tests
//{
//    public class ReadEvaluatorTests
//    {
//        [Fact]
//        public void CollectAndCategorize()
//        {
//            var pairClassificationsExpectedToWriteBamsFor = new[]
//            {
//                PairClassification.PerfectStitched, PairClassification.ImperfectStitched,
//                PairClassification.FailStitch, PairClassification.UnstitchIndel, PairClassification.Split,
//                PairClassification.Unstitchable,
//                PairClassification.Disagree, PairClassification.MessyStitched, PairClassification.MessySplit,
//                PairClassification.UnusableSplit,
//                PairClassification.UnstitchImperfect
//            };

//            var options = new StitcherOptions() {NumThreads = 2};
//            var readEvaluator = new ReadEvaluator(options, "test_in", "test_out", new GeminiOptions());
//            var mockReader = new Mock<IBamReader>();
//            mockReader.Setup(x => x.GetReferenceNames()).Returns(new List<string>() {"chr1", "chr2"});
//            mockReader.Setup(x => x.GetReferenceIndex("chr1")).Returns(1);
//            mockReader.Setup(x => x.GetReferenceIndex("chr2")).Returns(2);

//            var readPair1 = TestHelpers.GetPair("5M1I5M", "5M1I5M");
//            var mockReadPairSource = MockDataSource(new List<ReadPair>() {readPair1});
//            var mockDataSourceFactory = new Mock<IGeminiDataSourceFactory>();
//            mockDataSourceFactory.Setup(x => x.CreateBamReader(It.IsAny<string>())).Returns(mockReader.Object);
//            mockDataSourceFactory.Setup(x => x.CreateReadPairSource(It.IsAny<IBamReader>(), It.IsAny<ReadStatusCounter>()))
//                .Returns(mockReadPairSource.Object);
//            mockDataSourceFactory.Setup(x => x.GetRefIdMapping(It.IsAny<string>()))
//                .Returns(new Dictionary<int, string>() {{1, "chr1"}, {2, "chr2"},{-1, "Unknown"}});

//            var alignments = new List<BamAlignment>();
//            var mockWriter = new Mock<IBamWriterHandle>();
//            mockWriter.Setup(x => x.WriteAlignment(It.IsAny<BamAlignment>())).Callback<BamAlignment>(b => alignments.Add(b));

//            var mockWriterFactory = new Mock<IBamWriterFactory>();
//            mockWriterFactory.Setup(x => x.CreateSingleBamWriter(It.IsAny<string>())).Returns(mockWriter.Object);
//            var mockDataOutputFactory = new Mock<IGeminiDataOutputFactory>();
//            mockDataOutputFactory.Setup(x => x.GetBamWriterFactory(It.IsAny<string>()))
//                .Returns(mockWriterFactory.Object);
//            readEvaluator.CollectAndCategorize(mockDataSourceFactory.Object, mockDataOutputFactory.Object);
            
//            var numCategoriesToWriteBamFor = pairClassificationsExpectedToWriteBamsFor.Length;
//            var numBamsExpected = numCategoriesToWriteBamFor * 3 * 2; //3 for num chroms, 2 for num threads
//            mockWriterFactory.Verify(x=>x.CreateSingleBamWriter(It.IsAny<string>()), Times.Exactly(numBamsExpected));

//            foreach (var classification in pairClassificationsExpectedToWriteBamsFor)
//            {
//                foreach (var chr in new List<string>(){"chr1", "chr2"})
//                {
//                    mockWriterFactory.Verify(x => x.CreateSingleBamWriter("test_out_" + classification + "_" + chr + "_0"), Times.Once);
//                    mockWriterFactory.Verify(x => x.CreateSingleBamWriter("test_out_" + classification + "_" + chr + "_1"), Times.Once);
//                }
//            }

//            mockWriter.Verify(x=>x.WriteAlignment(null), Times.Exactly(66));
//            // TODO verify actual reads?


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