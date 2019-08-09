using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using Alignment.Domain;
using BamStitchingLogic;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.Types;
using Gemini.Utility;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;

namespace Gemini
{
    public class BlockFactorySource : IBlockFactorySource
    {
        private readonly StitcherOptions _stitcherOptions;
        private readonly GeminiOptions _geminiOptions;
        private readonly Dictionary<int, string> _refIdMapping;
        private readonly BamRealignmentFactory _bamRealignmentFactory;
        private readonly IGeminiDataSourceFactory _dataSourceFactory;
        private readonly GeminiSampleOptions _geminiSampleOptions;
        private readonly RealignmentOptions _realignmentOptions;
        private readonly IGeminiFactory _geminiFactory;
        private readonly IGeminiDataOutputFactory _dataOutputFactory;
        private readonly int _maxDegreeOfParallelism;

        public BlockFactorySource(StitcherOptions stitcherOptions, GeminiOptions geminiOptions, Dictionary<int, string> refIdMapping, BamRealignmentFactory bamRealignmentFactory,
            IGeminiDataSourceFactory dataSourceFactory, GeminiSampleOptions geminiSampleOptions, RealignmentOptions realignmentOptions, IGeminiFactory geminiFactory)
        {
            _stitcherOptions = stitcherOptions;
            _geminiOptions = geminiOptions;
            _refIdMapping = refIdMapping;
            _bamRealignmentFactory = bamRealignmentFactory;
            _dataSourceFactory = dataSourceFactory;
            _geminiSampleOptions = geminiSampleOptions;
            _realignmentOptions = realignmentOptions;
            _geminiFactory = geminiFactory;
            _maxDegreeOfParallelism = Math.Min(_stitcherOptions.NumThreads, Environment.ProcessorCount);
        }
        public IBatchBlockFactory<BatchBlock<ReadPair>,ReadPair> GetBatchBlockFactory()
        {
            return new BatchBlockFactory(_geminiOptions.ReadCacheSize);
        }

        public ITransformerBlockFactory<TransformManyBlock<IEnumerable<ReadPair>, PairResult>> GetClassifierBlockFactory()
        {
            return new ClassifierTransformerBlockFactory(_refIdMapping, _stitcherOptions, _geminiOptions, _geminiSampleOptions);
        }

        public IClassificationBlockProvider GetBlockProvider(Dictionary<int, string> refIdMapping, string chrom,
            IWriterSource writerSource, ConcurrentDictionary<string, int> progressTracker,
            ConcurrentDictionary<PairClassification, int> categoryLookup, ConcurrentDictionary<string, IndelEvidence> masterIndelLookup,
            ConcurrentDictionary<HashableIndel, int[]> masterOutcomesLookup,
            ConcurrentDictionary<HashableIndel, int> masterFinalIndels, ChrReference chrReference)
        {
            var actionBlockFactoryProvider = new PairResultActionBlockFactoryProvider(writerSource, _geminiOptions.Debug,
                _geminiOptions.LightDebug, chrom, _geminiSampleOptions.RefId.Value, _maxDegreeOfParallelism,
                _stitcherOptions.FilterForProperPairs, _geminiOptions.MessySiteWidth, progressTracker, categoryLookup);
            var aggregateProcessor = new AggregateRegionProcessor(chrReference, refIdMapping,
                _bamRealignmentFactory, _geminiOptions, _geminiFactory, chrom, _dataSourceFactory, _realignmentOptions, masterIndelLookup, masterOutcomesLookup, masterFinalIndels, _realignmentOptions.CategoriesForRealignment, progressTracker);
            var batchBlockFactory = new PairResultBatchBlockFactory(_geminiOptions.ReadCacheSize / 5);

            return new ClassificationBlockProvider(_geminiOptions, chrom, progressTracker, categoryLookup, actionBlockFactoryProvider, aggregateProcessor,
                _geminiOptions.LightDebug, batchBlockFactory, new BinEvidenceFactory(_geminiOptions, _geminiSampleOptions), _realignmentOptions.CategoriesForRealignment, _maxDegreeOfParallelism, _geminiSampleOptions.OutputFolder);
        }
    }
}