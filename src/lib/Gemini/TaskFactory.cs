using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using BamStitchingLogic;
using Common.IO.Utility;
using Gemini.BinSignalCollection;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.Interfaces;
using Gemini.Types;
using Gemini.Utility;
using StitchingLogic;

namespace Gemini
{
    public interface ITransformerBlockFactory<T> where T: IDataflowBlock, IPropagatorBlock<IEnumerable<ReadPair>, PairResult>, 
        ISourceBlock<PairResult>, ITargetBlock<IEnumerable<ReadPair>>, IReceivableSourceBlock<PairResult>
    {
        T GetClassifierBlock();
    }

    public class ClassifierTransformerBlockFactory : ITransformerBlockFactory<TransformManyBlock<IEnumerable<ReadPair>, PairResult>>
    {
        private readonly Dictionary<int, string> _refIdMapping;
        private readonly GeminiOptions _geminiOptions;
        private readonly StitcherOptions _stitcherOptions;
        private readonly int _maxDegreeOfParallelism;

        public ClassifierTransformerBlockFactory(Dictionary<int, string> refIdMapping, StitcherOptions stitcherOptions, GeminiOptions geminiOptions)
        {
            _refIdMapping = refIdMapping;
            _stitcherOptions = stitcherOptions;
            _geminiOptions = geminiOptions;
            _maxDegreeOfParallelism = Math.Min(stitcherOptions.NumThreads, Environment.ProcessorCount);
        }
        public TransformManyBlock<IEnumerable<ReadPair>, PairResult> GetClassifierBlock()
        {
            return GetPairClassifierBlock();
        }

        private TransformManyBlock<IEnumerable<ReadPair>, PairResult> GetPairClassifierBlock()
        {
            var readPair = new TransformManyBlock<IEnumerable<ReadPair>, PairResult>(
                (data) =>
                {

                    var classifier = new ReadPairClassifierAndExtractor(_geminiOptions.TrustSoftclips,
                        (int)_stitcherOptions.FilterMinMapQuality, skipStitch: _geminiOptions.SkipStitching, treatAbnormalOrientationAsImproper: _geminiOptions.TreatAbnormalOrientationAsImproper, messyMapq: _geminiOptions.MessyMapq, numSoftclipsToBeConsideredMessy: _geminiOptions.NumSoftclipsToBeConsideredMessy, numMismatchesToBeConsideredMessy: _geminiOptions.NumMismatchesToBeConsideredMessy, stringTagsToKeepFromR1: _stitcherOptions.StringTagsToKeepFromR1, checkMd: _geminiOptions.SilenceSuspiciousMdReads);
                    var pairHandler = GetPairHandler(_refIdMapping);
                    // Get batches from groups
                    var batches = new List<PairResult>();
                    foreach (var pair in data)
                    {
                        var pairResult = classifier.GetBamAlignmentsAndClassification(pair, pairHandler);
                        if (pairResult.Classification != PairClassification.Unusable &&
                            pairResult.Classification != PairClassification.UnusableSplit && 
                            (!_geminiOptions.SkipAndRemoveDups || pairResult.Classification != PairClassification.Duplicate)) 
                        {
                            batches.Add(pairResult);
                        }
                    }

                    //pairHandler.Finish();
                    return batches;
                }, new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                    EnsureOrdered = true
                }
            );
            return readPair;
        }


        private PairHandler GetPairHandler(Dictionary<int, string> refIdMapping)
        {
            var stitcher = new BasicStitcher(_stitcherOptions.MinBaseCallQuality, useSoftclippedBases: _stitcherOptions.UseSoftClippedBases,
                nifyDisagreements: _stitcherOptions.NifyDisagreements, debug: _stitcherOptions.Debug,
                nifyUnstitchablePairs: _stitcherOptions.NifyUnstitchablePairs, ignoreProbeSoftclips: !_stitcherOptions.StitchProbeSoftclips,
                maxReadLength: _stitcherOptions.MaxReadLength, ignoreReadsAboveMaxLength: _stitcherOptions.IgnoreReadsAboveMaxLength,
                thresholdNumDisagreeingBases: _stitcherOptions.MaxNumDisagreeingBases, dontStitchHomopolymerBridge: _stitcherOptions.DontStitchHomopolymerBridge, minMapQuality: _stitcherOptions.FilterMinMapQuality, countNsTowardNumDisagreeingBases: _stitcherOptions.CountNsTowardDisagreeingBases);

            return new PairHandler(refIdMapping, stitcher, tryStitch: !_geminiOptions.SkipStitching);
        }
    }

    public interface IBatchBlockFactory<T,U>
        where T : IDataflowBlock, IPropagatorBlock<U, U[]>, ISourceBlock<U[]>, ITargetBlock<U>,
        IReceivableSourceBlock<U[]>
    {
        T GetBlock();
    }

    public abstract class GenericBatchBlockFactory<U> : IBatchBlockFactory<BatchBlock<U>, U>
    {
        private readonly int _chunkSize;

        protected GenericBatchBlockFactory(int chunkSize)
        {
            _chunkSize = chunkSize;
        }
        public virtual BatchBlock<U> GetBlock()
        {
            return new BatchBlock<U>(_chunkSize, new GroupingDataflowBlockOptions() { EnsureOrdered = true, Greedy = true });
        }
    }

    public class BatchBlockFactory : GenericBatchBlockFactory<ReadPair>
    {
        public BatchBlockFactory(int chunkSize) : base(chunkSize)
        {
        }
    }

    public class PairResultBatchBlockFactory : GenericBatchBlockFactory<PairResult>
    {
        public PairResultBatchBlockFactory(int chunkSize) : base(chunkSize)
        {
        }
    }
    

    public interface IActionBlockFactory<T> where T : IDataflowBlock, ITargetBlock<PairResult[]>
    {
        T GetEarlyFlushBlock(PairClassification classification, bool isSingleMismatch);

        T GetActionablePairsBlock(PairClassification classification,
            ConcurrentDictionary<PairClassification, List<PairResult>> pairResultLookup);
    }

    public class PairResultActionBlockFactoryProvider
    {
        private readonly IWriterSource _writerSource;
        private readonly bool _debug;
        private readonly bool _lightDebug;
        private readonly string _chrom;
        private readonly int _refId;
        private readonly int _maxDegreeOfParallelism;
        private readonly bool _filterForProperPairs;
        private readonly int _messySiteWidth;
        private readonly ConcurrentDictionary<string, int> _progressTracker;
        private readonly ConcurrentDictionary<PairClassification, int> _categoryLookup;

        public PairResultActionBlockFactoryProvider(IWriterSource writerSource, bool debug, bool lightDebug, string chrom, int refId, int maxDegreeOfParallelism, bool filterForProperPairs, int messySiteWidth, ConcurrentDictionary<string, int> progressTracker, ConcurrentDictionary<PairClassification, int> categoryLookup)
        {
            _writerSource = writerSource;
            _debug = debug;
            _lightDebug = lightDebug;
            _chrom = chrom;
            _refId = refId;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _filterForProperPairs = filterForProperPairs;
            _messySiteWidth = messySiteWidth;
            _progressTracker = progressTracker;
            _categoryLookup = categoryLookup;
        }

        public IPairResultActionBlockFactory GetFactory(int startPosition, int endPosition, int adjustedStartPosition,
            ConcurrentDictionary<int, uint> totalBinCounts, ConcurrentDictionary<int, uint> singleMismatchBinCounts, int numBins, ConcurrentDictionary<Task, int> allToWaitFor)
        {
            return new PairResultActionBlockFactory(_writerSource, _debug, _lightDebug, _chrom, _refId, _maxDegreeOfParallelism, _filterForProperPairs, _messySiteWidth,
                _progressTracker, _categoryLookup, startPosition, endPosition, adjustedStartPosition, totalBinCounts, singleMismatchBinCounts, numBins, allToWaitFor);
        }
    }

    public interface IPairResultActionBlockFactory : IActionBlockFactory<ActionBlock<PairResult[]>>
    {
        ActionBlock<List<BamAlignment>> GetWriterBlock(PairClassification classification = PairClassification.Unknown);

    }

    class PairResultActionBlockFactory : IPairResultActionBlockFactory
    {
        private readonly IWriterSource _writerSource;
        private readonly bool _debug;
        private readonly bool _lightDebug;
        private readonly string _chrom;
        private readonly int _refId;
        private readonly int _maxDegreeOfParallelism;
        private readonly bool _filterForProperPairs;
        private readonly int _messySiteWidth;
        private readonly ConcurrentDictionary<string, int> _progressTracker;
        private readonly ConcurrentDictionary<PairClassification, int> _categoryLookup;
        private readonly int _startPosition;
        private readonly int _endPosition;
        private readonly int _adjustedStartPosition;
        private readonly ConcurrentDictionary<int, uint> _totalBinCounts;
        private readonly ConcurrentDictionary<int, uint> _singleMismatchBinCounts;
        private readonly int _numBins;
        private readonly ConcurrentDictionary<Task, int> _allToWaitFor;

        public PairResultActionBlockFactory(IWriterSource writerSource, bool debug, bool lightDebug, string chrom, 
            int refId, int maxDegreeOfParallelism, bool filterForProperPairs, int messySiteWidth, 
            ConcurrentDictionary<string, int> progressTracker, ConcurrentDictionary<PairClassification, int> categoryLookup, 
            int startPosition, int endPosition, int adjustedStartPosition,
            ConcurrentDictionary<int, uint> totalBinCounts, ConcurrentDictionary<int, uint> singleMismatchBinCounts, int numBins,
            ConcurrentDictionary<Task, int> allToWaitFor)
        {
            _writerSource = writerSource;
            _debug = debug;
            _lightDebug = lightDebug;
            _chrom = chrom;
            _refId = refId;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _filterForProperPairs = filterForProperPairs;
            _messySiteWidth = messySiteWidth;
            _progressTracker = progressTracker;
            _categoryLookup = categoryLookup;
            _startPosition = startPosition;
            _endPosition = endPosition;
            _adjustedStartPosition = adjustedStartPosition;
            _totalBinCounts = totalBinCounts;
            _singleMismatchBinCounts = singleMismatchBinCounts;
            _numBins = numBins;
            _allToWaitFor = allToWaitFor;
        }

        private readonly List<PairClassification> _improperTypes = new List<PairClassification>()
        {
            PairClassification.Improper,
            PairClassification.IndelImproper,

        };

        public ActionBlock<PairResult[]> GetActionablePairsBlock(PairClassification classification, ConcurrentDictionary<PairClassification, List<PairResult>> pairResultLookup)
        {
            var actBlock = new ActionBlock<PairResult[]>((p) =>
            {
                if (_lightDebug)
                {
                    Logger.WriteToLog(
                        $"Started handling {classification} block for region {_chrom}:{_startPosition}-{_endPosition}.");
                }

                pairResultLookup.AddOrUpdate(classification, p.ToList(), (c, n) => { return n.Concat(p).ToList(); });

                _categoryLookup.AddOrUpdate(classification, p.Sum(x => x.ReadPair.NumPrimaryReads),
                    (c, n) => { return n + p.Sum(x => x.ReadPair.NumPrimaryReads); });

                var toRemove = _allToWaitFor.Keys.Where(x => x.IsCompleted);
                foreach (var task in toRemove)
                {
                    _allToWaitFor.TryRemove(task, out _);
                }

                if (_lightDebug)
                {
                    Logger.WriteToLog(
                        $"Done handling {classification} block for region {_chrom}:{_startPosition}-{_endPosition}.");
                }
            }, new ExecutionDataflowBlockOptions() { EnsureOrdered = false });
            return actBlock;
        }

        public ActionBlock<PairResult[]> GetEarlyFlushBlock(PairClassification classification, bool isSingleMismatch)
        {
            var actBlock = new ActionBlock<PairResult[]>((p) =>
            {
                if (_lightDebug)
                {
                    Logger.WriteToLog(
                        $"Started handling {classification} block for region {_chrom}:{_startPosition}-{_endPosition}.");
                }

                var toRemove2 = _allToWaitFor.Keys.Where(x => x.IsCompleted);
                foreach (var task in toRemove2)
                {
                    _allToWaitFor.TryRemove(task, out _);
                }

                if (_debug)
                {
                    Console.WriteLine($"{p.Length} pairs in category {classification} in {_startPosition}-{_endPosition}");
                }

                var idNum = Thread.CurrentThread.ManagedThreadId;
                var writerHandle = _writerSource.BamWriterHandle(_chrom, classification, _startPosition);

                _categoryLookup.AddOrUpdate(classification, p.Sum(x => x.ReadPair.NumPrimaryReads),
                    (c, n) => { return n + p.Sum(x => x.ReadPair.NumPrimaryReads); });

                if (_filterForProperPairs && _improperTypes.Contains(classification))
                {
                    _progressTracker.AddOrUpdate("Skipped", p.Sum(x => x.ReadPair.NumPrimaryReads),
                        (c, n) => { return n + p.Sum(x => x.ReadPair.NumPrimaryReads); });
                }
                else
                {
                    var numAlignmentsWritten = 0;

                    var classificationString = classification.ToString();
                    foreach (var pair in p)
                    {
                        if (classification != PairClassification.Duplicate)
                        {
                            // Don't add bin evidence for duplicates, may wash the signal out
                            BinEvidenceHelpers.AddEvidence(pair, _messySiteWidth, _adjustedStartPosition,
                                _totalBinCounts,
                                _singleMismatchBinCounts, isSingleMismatch, _numBins, _refId);
                        }

                        foreach (var alignment in pair.Alignments)
                        {
                            if (alignment == null)
                            {
                                continue;
                            }

                            alignment.ReplaceOrAddStringTag("XP", classificationString);
                            numAlignmentsWritten++;

                            if (writerHandle == null)
                            {
                                throw new Exception("This is odd, why is the handle null");
                            }

                            writerHandle.WriteAlignment(alignment);
                        }
                    }

                    _progressTracker.AddOrUpdate("Early Flushed", p.Sum(x => x.ReadPair.NumPrimaryReads),
                        (s, n) => { return n + p.Sum(x => x.ReadPair.NumPrimaryReads); });
                    _progressTracker.AddOrUpdate("Simple Alignments Written", numAlignmentsWritten,
                        (s, n) => { return n + numAlignmentsWritten; });


                    Array.Clear(p, 0, p.Length);
                    _writerSource.DoneWithWriter(_chrom, classification, idNum, numAlignmentsWritten, writerHandle);
                    if (_lightDebug)
                    {
                        Logger.WriteToLog(
                            $"Done handling {classification} block for region {_chrom}:{_startPosition}-{_endPosition}.");
                    }
                }
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = _maxDegreeOfParallelism, EnsureOrdered = true });
            return actBlock;
        }

        public ActionBlock<List<BamAlignment>> GetWriterBlock(PairClassification classification = PairClassification.Unknown)
        {
            var writerTask = new ActionBlock<List<BamAlignment>>((alignments) =>
            {
                WriteAlignments(_chrom, _writerSource, classification, alignments);
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, EnsureOrdered = false });
            return writerTask;
        }

        private static void WriteAlignments(string chrom, IWriterSource writerSource, PairClassification classification,
            List<BamAlignment> alignments)
        {
            var idNum = Thread.CurrentThread.ManagedThreadId;
            var writerHandle = writerSource.BamWriterHandle(chrom, classification, idNum);
            var numAlignmentsWritten = 0;
            foreach (var alignment in alignments)
            {
                if (alignment == null)
                {
                    continue;
                }

                numAlignmentsWritten++;

                writerHandle.WriteAlignment(alignment);
            }

            alignments.Clear();
            writerSource.DoneWithWriter(chrom, classification, idNum, numAlignmentsWritten, writerHandle);
        }
    }
    
}