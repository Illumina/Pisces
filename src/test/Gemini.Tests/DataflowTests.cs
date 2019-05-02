using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO.Sequencing;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.Types;
using Gemini.Utility;
using Moq;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{

    public class DataflowTests
    {
        [Fact]
        public void Flow()
        {
            var readPair1 = TestHelpers.GetPair("5M1I5M", "5M1I5M");
            var readPair2 = TestHelpers.GetPair("5M1I5M", "5M1I5M", read1Position: 1001);
            var readPair3 = TestHelpers.GetPair("5M1I5M", "5M1I5M", read1Position: 1201);
            var readPair4 = TestHelpers.GetPair("5M1I5M", "5M1I5M", read1Position: 10000);
            var reads = new List<ReadPair>() { readPair1, readPair2, readPair3, readPair4 };
            var geminiOptions = new GeminiOptions()
            {
                RegionSize = 1000
            };

            // Initial region (0-1000), 1000-2000, 10000-11000, final region
            VerifyFlow(reads, geminiOptions, 4);

            geminiOptions.RegionSize = 100;
            VerifyFlow(reads, geminiOptions, 5);

            geminiOptions.RegionSize = 100000;
            VerifyFlow(reads, geminiOptions, 2);
        }

        private static void VerifyFlow(List<ReadPair> reads, GeminiOptions geminiOptions, int expectedRegions)
        {
            var batchBlockFactory = BatchBlockFactory();
            var classificationBlockFactory = new Mock<IClassificationBlockProvider>();
            var classifierBlockFactory = ClassifierBlockFactory();
            var blockFactory = BlockFactory(batchBlockFactory, classifierBlockFactory, classificationBlockFactory);

            var alignments = new List<BamAlignment>();
            var mockDataOutputFactory = DataflowMocks.MockDataOutputFactory(alignments);

            var mockReader = DataflowMocks.MockReader();
            var mockReadPairSource = DataflowMocks.MockDataSource(reads);
            var mockDataSourceFactory = DataflowMocks.MockDataSourceFactory(mockReader, mockReadPairSource);


            var dataflowEvaluator = new DataflowReadEvaluator(geminiOptions, mockDataSourceFactory.Object,
                new GeminiSampleOptions(){OutputFolder = "outfoldersample"}, mockDataOutputFactory.Object, blockFactory.Object);

            dataflowEvaluator.ProcessBam();

            VerifyCallNumbers(batchBlockFactory, classifierBlockFactory, classificationBlockFactory, mockDataSourceFactory,
                blockFactory, expectedRegions);
        }

        private static Mock<IBatchBlockFactory<BatchBlock<ReadPair>,ReadPair>> BatchBlockFactory()
        {
            var batchBlockFactory = new Mock<IBatchBlockFactory<BatchBlock<ReadPair>,ReadPair>>();
            var batchBlock = new BatchBlock<ReadPair>(10);
            batchBlock.Complete();
            batchBlockFactory.Setup(x => x.GetBlock()).Returns(batchBlock);
            return batchBlockFactory;
        }

        private static Mock<ITransformerBlockFactory<TransformManyBlock<IEnumerable<ReadPair>, PairResult>>> ClassifierBlockFactory()
        {
            var classifierBlockFactory =
                new Mock<ITransformerBlockFactory<TransformManyBlock<IEnumerable<ReadPair>, PairResult>>>();
            var classificationBlock = new TransformManyBlock<IEnumerable<ReadPair>, PairResult>((p) =>
            {
                var pairResults = new List<PairResult>();
                return pairResults;
            });
            classifierBlockFactory.Setup(x => x.GetClassifierBlock()).Returns(classificationBlock);
            return classifierBlockFactory;
        }

        private static Mock<IBlockFactorySource> BlockFactory(Mock<IBatchBlockFactory<BatchBlock<ReadPair>, ReadPair>> batchBlockFactory, Mock<ITransformerBlockFactory<TransformManyBlock<IEnumerable<ReadPair>, PairResult>>> classifierBlockFactory, Mock<IClassificationBlockProvider> classificationBlockFactory)
        {
            var blockFactory = new Mock<IBlockFactorySource>();
            blockFactory.Setup(x => x.GetBatchBlockFactory()).Returns(batchBlockFactory.Object);
            blockFactory.Setup(x => x.GetClassifierBlockFactory()).Returns(classifierBlockFactory.Object);
            blockFactory.Setup(x => x.GetBlockProvider(It.IsAny<Dictionary<int, string>>(), It.IsAny<string>(), It.IsAny<IWriterSource>(),
                    It.IsAny<ConcurrentDictionary<string, int>>(),
                    It.IsAny<ConcurrentDictionary<PairClassification, int>>(), It.IsAny<ConcurrentDictionary<string, IndelEvidence>>(), It.IsAny<ConcurrentDictionary<HashableIndel, int[]>>(), 
                    It.IsAny<ConcurrentDictionary<HashableIndel, int>>(), It.IsAny<ChrReference>()))
                .Returns(classificationBlockFactory.Object);
            return blockFactory;
        }

        private static void VerifyCallNumbers(Mock<IBatchBlockFactory<BatchBlock<ReadPair>, ReadPair>> batchBlockFactory, Mock<ITransformerBlockFactory<TransformManyBlock<IEnumerable<ReadPair>, PairResult>>> classifierBlockFactory,
            Mock<IClassificationBlockProvider> classificationBlockFactory, Mock<IGeminiDataSourceFactory> mockDataSourceFactory, Mock<IBlockFactorySource> blockFactory, int expectedTimes)
        {
            // Per-region calls
            batchBlockFactory.Verify(x => x.GetBlock(), Times.Exactly(expectedTimes));
            classifierBlockFactory.Verify(x => x.GetClassifierBlock(), Times.Exactly(expectedTimes));
            classificationBlockFactory.Verify(x => x.GetAndLinkAllClassificationBlocksWithEcFinalization(
                It.IsAny<TransformManyBlock<IEnumerable<ReadPair>, PairResult>>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<ConcurrentDictionary<int, EdgeState>>(),
                It.IsAny<ConcurrentDictionary<int, Task>>(), It.IsAny<int>(),  It.IsAny<bool>()), Times.Exactly(expectedTimes));

            // Everything else should be called only once
            mockDataSourceFactory.Verify(x => x.CreateReadPairSource(It.IsAny<IBamReader>(), It.IsAny<ReadStatusCounter>()),
                Times.Once);
            blockFactory.Verify(x => x.GetBatchBlockFactory(), Times.Once);
        }


    }
}