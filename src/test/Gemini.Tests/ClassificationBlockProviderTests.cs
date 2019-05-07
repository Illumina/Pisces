using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using BamStitchingLogic;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.Types;
using Gemini.Utility;
using Moq;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using Xunit;

namespace Gemini.Tests
{
    public class ClassificationBlockProviderTests
    {
        [Fact]
        public void GetAndLinkAllClassificationBlocksWithEcFinalization()
        {
            var geminiOptions = new GeminiOptions();
            var chrom = "chr1";
            var tracker = new ConcurrentDictionary<string, int>();
            var categoryLookup = new ConcurrentDictionary<PairClassification, int>();
            var mockWriterSource = new Mock<IWriterSource>();
            var actionBlockProvider = new PairResultActionBlockFactoryProvider(mockWriterSource.Object, false, false,
                "chr1", 1, 1, false, 500, tracker, categoryLookup);
            var chrReference = new ChrReference();
            var realignFactory = new BamRealignmentFactory(geminiOptions, new RealignmentAssessmentOptions(),
                new StitcherOptions(), new RealignmentOptions(), "outdir");
            var gemFactory = new GeminiFactory(geminiOptions, new IndelFilteringOptions());
            var dataSourceFactory = new Mock<IGeminiDataSourceFactory>();
            var dataOutputFactory = new Mock<IGeminiDataOutputFactory>();
            ConcurrentDictionary<string, IndelEvidence> masterIndelLOokup = new ConcurrentDictionary<string, IndelEvidence>();
            ConcurrentDictionary<HashableIndel, int[]> masterOutcomesLookup = new ConcurrentDictionary<HashableIndel, int[]>();
            ConcurrentDictionary<HashableIndel, int> masterFinalIndels = new ConcurrentDictionary<HashableIndel, int>();
            var binEvidenceFactory = new BinEvidenceFactory(geminiOptions, new GeminiSampleOptions());
            var catsForRealign = new List<PairClassification>();

            var aggRegionProcessor = new AggregateRegionProcessor(chrReference,
                new Dictionary<int, string>() {{1, "chr1"}},
                realignFactory, geminiOptions, gemFactory, chrom, dataSourceFactory.Object, new RealignmentOptions(),
                masterIndelLOokup, masterOutcomesLookup, masterFinalIndels,
                catsForRealign, tracker);

            var provider = new ClassificationBlockProvider(geminiOptions, chrom, tracker, categoryLookup,
                actionBlockProvider, aggRegionProcessor, false, new PairResultBatchBlockFactory(10), binEvidenceFactory,
                catsForRealign, 1);


            var sourceBlock = new Mock<ISourceBlock<PairResult>>();
            bool consumed;
            sourceBlock.Setup(x => x.ConsumeMessage(It.IsAny<DataflowMessageHeader>(),
                It.IsAny<ITargetBlock<PairResult>>(), out consumed)).Returns(new PairResult());
            ConcurrentDictionary<int, EdgeState> edgeStates = new ConcurrentDictionary<int, EdgeState>();
            ConcurrentDictionary<int, Task> edgeToWaitOn = new ConcurrentDictionary<int, Task>();
            provider.GetAndLinkAllClassificationBlocksWithEcFinalization(sourceBlock.Object, 1000, 2000, edgeStates,
                edgeToWaitOn, 0, false);
        }
    }
}