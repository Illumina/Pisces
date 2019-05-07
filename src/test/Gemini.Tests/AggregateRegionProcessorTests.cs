using System.Collections.Concurrent;
using System.Collections.Generic;
using BamStitchingLogic;
using Gemini.BinSignalCollection;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.Models;
using Gemini.Realignment;
using Gemini.Types;
using Gemini.Utility;
using Moq;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using Xunit;

namespace Gemini.Tests
{
    public class AggregateRegionProcessorTests
    {
        [Fact]
        public void GetAggregateRegionResults()
        {
            var geminiOptions = new GeminiOptions();
            ChrReference chrReference = null;
            var refIdMapping = new Dictionary<int, string>() {{1, "chr1"}};
            var bamRealignmentFactory = new BamRealignmentFactory(new GeminiOptions(),
                new RealignmentAssessmentOptions(), new StitcherOptions(), new RealignmentOptions(), "out");

            var geminiFactory = new GeminiFactory(geminiOptions, new IndelFilteringOptions());
            var dataSourceFactoryMock = new Mock<IGeminiDataSourceFactory>();
            var chromIndelSource = new Mock<IChromosomeIndelSource>();
            var indel = new KeyValuePair<HashableIndel, GenomeSnippet>();
            chromIndelSource
                .Setup(x => x.GetRelevantIndels(It.IsAny<int>(), It.IsAny<List<PreIndel>>(),
                    It.IsAny<List<HashableIndel>>(), It.IsAny<List<PreIndel>>(), It.IsAny<List<PreIndel>>())).Returns(new List<KeyValuePair<HashableIndel, GenomeSnippet>>()
                {
                    //indel
                });
            dataSourceFactoryMock
                .Setup(x => x.GetChromosomeIndelSource(It.IsAny<List<HashableIndel>>(),
                    It.IsAny<IGenomeSnippetSource>())).Returns(chromIndelSource.Object);
            var dataSourceFactory = dataSourceFactoryMock.Object;
            var masterIndelLookup = new ConcurrentDictionary<string, IndelEvidence>();
            var masterOutcomesLookup = new ConcurrentDictionary<HashableIndel, int[]>();
            var masterFinalIndels = new ConcurrentDictionary<HashableIndel, int>();
            var categoriesForRealignment = new List<PairClassification>();
            var progressTracker = new ConcurrentDictionary<string, int>();

            var processor = new AggregateRegionProcessor(chrReference, refIdMapping, bamRealignmentFactory,
                geminiOptions, geminiFactory, "chr1", dataSourceFactory, new RealignmentOptions(){CategoriesForSnowballing = new List<PairClassification>(){PairClassification.Disagree}},
                masterIndelLookup, masterOutcomesLookup, masterFinalIndels, categoriesForRealignment, progressTracker);

            var indelLookup = new ConcurrentDictionary<string, IndelEvidence>();
            var binEvidence = new BinEvidence(1, true, 20, false, 500, 1000);
            var edgeBinEvidence = new BinEvidence(1, true, 20, false, 500, 1000);
            var edgeState = new EdgeState()
            {
                Name = "0-1000",
                EdgeAlignments = new Dictionary<PairClassification, List<PairResult>>(),
                BinEvidence = edgeBinEvidence,
                EdgeIndels = new List<HashableIndel>(),
                EffectiveMinPosition = 0
            };

            var pairResultLookup =
                new ConcurrentDictionary<PairClassification, List<PairResult>>();
            pairResultLookup.TryAdd(PairClassification.Disagree, new List<PairResult>()
            {
                TestHelpers.GetPairResult(10000),
                TestHelpers.GetPairResult(10001),
                TestHelpers.GetPairResult(10002),
                TestHelpers.GetPairResult(19995)
            });
            pairResultLookup.TryAdd(PairClassification.SingleMismatchStitched, new List<PairResult>()
            {
                TestHelpers.GetPairResult(19995),
                TestHelpers.GetPairResult(19995)
            });

            // Borderline case: the max position in the pair is >= EffectiveMaxPosition - 5000, even if one of the reads in the pair is not
            var effectiveMax = 19999;
            var r2BorderlinePos = effectiveMax - 5000 + 1;
            var offset = 1;
            var r1BorderlinePos = r2BorderlinePos - offset;
            pairResultLookup.TryAdd(PairClassification.UnstitchForwardMessy, new List<PairResult>()
            {
                TestHelpers.GetPairResult(r1BorderlinePos, offset), // One is just over border
                TestHelpers.GetPairResult(r1BorderlinePos, 0), // Both are within safe range
            });

            var regionData = new RegionDataForAggregation()
            {
                BinEvidence = binEvidence,
                EdgeState = edgeState,
                EffectiveMaxPosition = effectiveMax,
                EffectiveMinPosition = 10000,
                PairResultLookup = pairResultLookup
            };
            var regionResults = processor.GetAggregateRegionResults(indelLookup, 
                10000, 20000, false, regionData);

            // New edge state should have the correct items carrying over
            Assert.Equal("10000-20000", regionResults.EdgeState.Name);
            Assert.Equal(14999, regionResults.EdgeState.EffectiveMinPosition);
            Assert.Equal(4, regionResults.AlignmentsReadyToBeFlushed.Count); // The four that are solidly in-bounds should be flushable immediately

            var edgeAlignmentsLookup = regionResults.EdgeState.EdgeAlignments;
            Assert.Equal(1, edgeAlignmentsLookup[PairClassification.Disagree].Count);
            Assert.Equal(2, edgeAlignmentsLookup[PairClassification.SingleMismatchStitched].Count);
            Assert.Equal(1, edgeAlignmentsLookup[PairClassification.UnstitchForwardMessy].Count);
        }
    }
}