using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Alignment.Domain.Sequencing;
using Common.IO.Utility;
using Gemini.BinSignalCollection;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Types;
using Gemini.Utility;

namespace Gemini
{
    public interface IBinEvidenceFactory
    {
        BinEvidence GetBinEvidence(int numBins, int startPosition);
        int GetNumBins(int regionLength);
    }
    public class BinEvidenceFactory : IBinEvidenceFactory
    {
        private readonly GeminiOptions _geminiOptions;
        private readonly GeminiSampleOptions _geminiSampleOptions;

        public BinEvidenceFactory(GeminiOptions geminiOptions, GeminiSampleOptions geminiSampleOptions)
        {
            _geminiOptions = geminiOptions;
            _geminiSampleOptions = geminiSampleOptions;
        }

        public BinEvidence GetBinEvidence(int numBins, int startPosition)
        {
            var binEvidence = new BinEvidence(_geminiSampleOptions.RefId.Value, _geminiOptions.CollectDepth,
                numBins, _geminiOptions.AvoidLikelySnvs, _geminiOptions.MessySiteWidth, startPosition, trackDirectionalMess: _geminiOptions.SilenceDirectionalMessReads, trackMapqMess: _geminiOptions.SilenceMessyMapMessReads);
            return binEvidence;
        }

        public int GetNumBins(int regionLength)
        {
            return regionLength / _geminiOptions.MessySiteWidth;
        }
    }

    public class ClassificationBlockProvider : IClassificationBlockProvider
    {
        private readonly IBinEvidenceFactory _binEvidenceFactory;
        private readonly GeminiOptions _geminiOptions;
        private readonly IndelTargetFinder _targetFinder = new IndelTargetFinder();
        private readonly string _chrom;
        private readonly ConcurrentDictionary<string, int> _progressTracker;
        private readonly ConcurrentDictionary<PairClassification, int> _categoryLookup;
        private readonly int _maxDegreeOfParallelism;
        private readonly PairResultActionBlockFactoryProvider _actionBlockFactoryProvider;
        private readonly IAggregateRegionProcessor _aggregateRegionProcessor;
        private readonly bool _lightDebug;
        private readonly List<PairClassification> _categoriesForRealignment;
        private readonly PairResultBatchBlockFactory _batchBlockFactory;

        PairClassification[] classifications = new[]
        {
            PairClassification.PerfectStitched, PairClassification.ImperfectStitched,
            PairClassification.FailStitch, PairClassification.UnstitchIndel, PairClassification.Split,
            PairClassification.Unstitchable,
            PairClassification.Disagree, PairClassification.MessyStitched, PairClassification.MessySplit,
            PairClassification.UnusableSplit,
            PairClassification.UnstitchImperfect,
            PairClassification.UnstitchPerfect,
            PairClassification.LongFragment,
            PairClassification.UnstitchMessy, PairClassification.UnstitchSingleMismatch, PairClassification.SingleMismatchStitched,
            PairClassification.UnstitchMessySuspiciousRead,
            PairClassification.UnstitchableAsSingleton,
            PairClassification.IndelSingleton,
            PairClassification.IndelUnstitchable,
            PairClassification.UnstitchForwardMessy,
            PairClassification.UnstitchReverseMessy,
            PairClassification.Improper,
            PairClassification.IndelImproper,
            PairClassification.UnstitchMessyIndel,
            PairClassification.UnstitchMessyIndelSuspiciousRead,
            PairClassification.UnstitchForwardMessyIndel,
            PairClassification.UnstitchReverseMessyIndel,
            PairClassification.Duplicate,
            PairClassification.UnstitchMessySuspiciousMd
        };

        public ClassificationBlockProvider(GeminiOptions geminiOptions, string chrom, ConcurrentDictionary<string, int> progressTracker, 
            ConcurrentDictionary<PairClassification, int> categoryLookup, PairResultActionBlockFactoryProvider actionBlockFactoryProvider, 
            IAggregateRegionProcessor aggregateRegionProcessor, bool lightDebug, PairResultBatchBlockFactory batchBlockFactory, IBinEvidenceFactory binEvidenceFactory, List<PairClassification> categoriesForRealignment, int maxDegreeOfParallelism)
        {
            _geminiOptions = geminiOptions;
            _chrom = chrom;
            _progressTracker = progressTracker;
            _categoryLookup = categoryLookup;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _actionBlockFactoryProvider = actionBlockFactoryProvider;
            _aggregateRegionProcessor = aggregateRegionProcessor;
            _lightDebug = lightDebug;
            _binEvidenceFactory = binEvidenceFactory;
            _categoriesForRealignment = categoriesForRealignment;
            _batchBlockFactory = batchBlockFactory;
        }

        public Task[] GetAndLinkAllClassificationBlocksWithEcFinalization(
            ISourceBlock<PairResult> pairClassifierBlock,
            int startPosition, int endPosition, ConcurrentDictionary<int, EdgeState> edgeStates,
            ConcurrentDictionary<int, Task> edgeToWaitOn, int prevBlockStart, 
            bool isFinalTask = false)
        {
            if (_lightDebug)
            {
                Logger.WriteToLog(
                    $"Creating tasks for region {_chrom}:{startPosition}-{endPosition}.");
            }

            var allToWaitFor = new ConcurrentDictionary<Task, int>();

            var messySiteWidth = _geminiOptions.MessySiteWidth;
            var effectiveMax = 0;

            var adjustedStartPosition = startPosition;
            adjustedStartPosition = RoundedStartPosition(adjustedStartPosition, messySiteWidth);

            var pairResultLookup = new ConcurrentDictionary<PairClassification, List<PairResult>>();
            var indelLookup = new ConcurrentDictionary<string, IndelEvidence>();

            var regionLength = endPosition - adjustedStartPosition;
            var numBins = (regionLength / messySiteWidth) + 1000;

            var totalBinCounts = InitializeTotalBinCounts(numBins);
            var singleMismatchBinCounts = InitializeSingleMismatchBinCounts(numBins);

            var actBlockFactory = _actionBlockFactoryProvider.GetFactory(startPosition, endPosition,
                adjustedStartPosition, totalBinCounts, singleMismatchBinCounts, numBins, allToWaitFor);

            foreach (var classification in classifications)
            {   
                var toWaitFor = GetAndLinkPerClassificationBlocksWithEcFinalization(pairClassifierBlock, classification, indelLookup);

                var doStitch = !_geminiOptions.SkipStitching && TypeClassifier.ClassificationIsStitchable(classification);
                var categoryIsRealignable = _categoriesForRealignment.Contains(classification);

                // Even if we're not going to realign these reads, they may still be useful for bin evidence, so don't give them the immediate flush
                var shouldCollectBinEvidence = TypeClassifier.MessyTypes.Contains(classification) || TypeClassifier._indelTypes.Contains(classification);

                var isSingleMismatch = _geminiOptions.AvoidLikelySnvs && (classification == PairClassification.SingleMismatchStitched ||
                                                                          classification == PairClassification.UnstitchSingleMismatch);

                if (!(categoryIsRealignable || doStitch || shouldCollectBinEvidence))
                {
                    var actBlock = actBlockFactory.GetEarlyFlushBlock(classification, isSingleMismatch);

                    foreach (var transformBlock in toWaitFor)
                    {
                        transformBlock.LinkTo(actBlock, new DataflowLinkOptions() { PropagateCompletion = true });
                    }

                    var toRemove = allToWaitFor.Keys.Where(x => x.IsCompleted);
                    foreach (var task in toRemove)
                    {
                        allToWaitFor.TryRemove(task, out _);
                    }
                    if (!allToWaitFor.TryAdd(actBlock.Completion, 1))
                    {
                        throw new Exception("Failed to add task.");
                    }
                }
                else
                {
                    var actBlock = actBlockFactory.GetActionablePairsBlock(classification, pairResultLookup);

                    foreach (var transformBlock in toWaitFor)
                    {
                        transformBlock.LinkTo(actBlock, new DataflowLinkOptions() { PropagateCompletion = true });
                    }

                    if (!allToWaitFor.TryAdd(actBlock.Completion, 1))
                    {
                        throw new Exception("Failed to add task.");
                    }
                }

            }

            var finalTask = AggregateTask(indelLookup, startPosition, endPosition, isFinalTask, _progressTracker);
            var intermediateWriterTask = new TransformBlock<AggregateRegionResults, List<BamAlignment>>(results =>
            {
                edgeStates.AddOrUpdate(startPosition, results.EdgeState, (s, e) =>
                {
                    Logger.WriteWarningToLog($"Edge state already exists: {s}.");
                    return results.EdgeState;
                });
                return results.AlignmentsReadyToBeFlushed;
            }, new ExecutionDataflowBlockOptions() { EnsureOrdered = false });

            var finalWriteTask = actBlockFactory.GetWriterBlock();
            finalTask.LinkTo(intermediateWriterTask, new DataflowLinkOptions() { PropagateCompletion = true });
            intermediateWriterTask.LinkTo(finalWriteTask, new DataflowLinkOptions() { PropagateCompletion = true });

            if (edgeToWaitOn.ContainsKey(prevBlockStart))
            {
                if (!allToWaitFor.TryAdd(edgeToWaitOn[prevBlockStart], 1))
                {
                    throw new Exception("Failed to add task for previous edge.");
                }
            }
            else
            {
                Logger.WriteToLog($"At {startPosition}, prev block is {prevBlockStart}, nothing to wait on.");
            }

            if (!isFinalTask)
            {
                edgeToWaitOn.AddOrUpdate(startPosition, intermediateWriterTask.Completion, (s, e) =>
                {
                    Logger.WriteWarningToLog($"Edge state task already exists: {s}.");
                    return intermediateWriterTask.Completion;
                });
            }

            var allTasks = allToWaitFor.Keys.ToList();
            var t = Task.WhenAll(allTasks)
                .ContinueWith(_ =>
                {
                    if (_lightDebug)
                    {
                        Logger.WriteToLog($"Preparing for aggregation for region {_chrom}:{startPosition}-{endPosition}.");
                    }

                    if (allTasks.Any(x => x.Status != TaskStatus.RanToCompletion))
                    {
                        Logger.WriteToLog("ERROR: Task did not complete.");

                        foreach (var task in allTasks)
                        {
                            Logger.WriteToLog($"{task.Id}\t{task.Status}\t{task.Exception}");
                            if (task.Status == TaskStatus.Faulted)
                            {
                                // Pass the exception along to the final task so it can be forced to error out.
                                finalTask = ForceFailFinalTask(intermediateWriterTask, task.Exception);
                            }
                        }
                    }

                    var numStillToProcess = 0;
                    foreach (var item in pairResultLookup)
                    {
                        effectiveMax = Math.Max(effectiveMax, item.Value.Max(x => x.ReadPair.MaxPosition));
                        numStillToProcess += item.Value.Count;
                    }

                    if (_lightDebug)
                    {
                        Logger.WriteToLog($"Preparing edge state info for region {_chrom}:{startPosition}-{endPosition}.");
                    }

                    EdgeState edgeState = null;
                    var extraBins = 0;
                    if (edgeStates.ContainsKey(prevBlockStart))
                    {
                        edgeStates.Remove(prevBlockStart, out edgeState);
                        if (edgeState.EdgeIndels.Any() || edgeState.EdgeAlignments.Any())
                        {
                            var newAdjustedStartPosition = RoundedStartPosition(Math.Min(adjustedStartPosition, edgeState.EffectiveMinPosition), messySiteWidth);
                            extraBins = (adjustedStartPosition - newAdjustedStartPosition) / messySiteWidth;
                            adjustedStartPosition = newAdjustedStartPosition;
                        }
                    }
                    allToWaitFor.Clear();
                    allTasks.Clear();


                    if (_lightDebug)
                    {
                        var totalReadsInRegion = _categoryLookup.Values.Sum();
                        Console.WriteLine($"STILL TO PROCESS IN REGION ({startPosition}-{endPosition} (eff:{effectiveMax})): {numStillToProcess}");
                        Console.WriteLine(
                            $"READS IN REGION ({startPosition}-{endPosition} (eff:{effectiveMax})): {totalReadsInRegion}");
                        foreach (var kvp in _categoryLookup)
                        {
                            Console.WriteLine(
                                $"CATEGORYCOUNT ({startPosition}-{endPosition} (eff:{effectiveMax})): {kvp.Key}: {kvp.Value} ({Math.Round(kvp.Value * 100 / (float)totalReadsInRegion)}%)");
                        }
                    }

                    var totalNumBins = numBins + extraBins;
                    var allHits = new uint[totalNumBins];
                    var singleMismatchHits = new uint[totalNumBins];

                    for (var i = 0; i < totalNumBins; i++)
                    {
                        var newBin = i + extraBins;
                        if (newBin >= totalNumBins)
                        {
                            break;
                        }

                        if (totalBinCounts[i] > 0)
                        {
                            allHits[newBin] = totalBinCounts[i];
                            singleMismatchHits[newBin] = singleMismatchBinCounts[i];
                        }
                    }


                    if (_lightDebug)
                    {
                        Logger.WriteToLog(
                            $"Creating bin evidence for region {_chrom}:{startPosition}-{endPosition}.");
                    }

                    var binEvidence = _binEvidenceFactory.GetBinEvidence(totalNumBins, adjustedStartPosition);
                    binEvidence.SetSingleMismatchHits(singleMismatchHits);
                    binEvidence.AddAllHits(allHits);
                    if (_lightDebug)
                    {
                        Logger.WriteToLog($"Adding edge hits for region {_chrom}:{startPosition}-{endPosition}.");
                    }

                    if (edgeState != null)
                    {
                        var edgeBinInNew = binEvidence.GetBinId(edgeState.EffectiveMinPosition);
                        var edgeBinInOld = edgeState.BinEvidence.GetBinId(edgeState.EffectiveMinPosition);
                        AddEdgeHits(edgeState, binEvidence, edgeBinInOld, edgeBinInOld - edgeBinInNew);
                    }
                    if (_lightDebug)
                    {
                        Logger.WriteToLog($"Done adding edge hits for region {_chrom}:{startPosition}-{endPosition}.");
                    }

                    var finalState = new RegionDataForAggregation()
                    {
                        BinEvidence = binEvidence,
                        PairResultLookup = pairResultLookup,
                        EdgeState = edgeState,
                        EffectiveMaxPosition = effectiveMax,
                        EffectiveMinPosition = adjustedStartPosition
                    };

                    finalTask.Post(finalState);
                    finalTask.Complete();
                });

            return new[] { t, finalWriteTask.Completion };


        }

        private static TransformBlock<RegionDataForAggregation, AggregateRegionResults> ForceFailFinalTask(TransformBlock<AggregateRegionResults, List<BamAlignment>> intermediateWriterTask, Exception e)
        {
            // Force fail and re-link
            var finalTask = new TransformBlock<RegionDataForAggregation, AggregateRegionResults>((x) =>
            {
                Logger.WriteToLog("Error in classification processing.");
                Logger.WriteExceptionToLog(e);
                throw new Exception("Force-failing final processing task due to failure in classification processing subtasks.", e);
                // Need this return statement here to prevent ambiguous constructor for some reason, despite always throwing exception above...
                return new AggregateRegionResults();
            });

            finalTask.LinkTo(intermediateWriterTask, new DataflowLinkOptions() {PropagateCompletion = true});
            return finalTask;
        }

        private static ConcurrentDictionary<int, uint> InitializeSingleMismatchBinCounts(int numBins)
        {
            var singleMismatchBinCounts = new ConcurrentDictionary<int, uint>();
            for (var i = 0; i < numBins; i++)
            {
                singleMismatchBinCounts[i] = 0;
            }

            return singleMismatchBinCounts;
        }

        private static ConcurrentDictionary<int, uint> InitializeTotalBinCounts(int numBins)
        {
            var totalBinCounts = new ConcurrentDictionary<int, uint>();
            for (var i = 0; i < numBins; i++)
            {
                totalBinCounts[i] = 0;
            }

            return totalBinCounts;
        }


        private static void AddEdgeHits(EdgeState edgeState, IBinEvidence binEvidence2, int offset, int startInOld)
        {
            binEvidence2.CombineBinEvidence(edgeState.BinEvidence, offset, startInOld, edgeState.BinEvidence.NumBins);
        }


        private int RoundedStartPosition(int rawStartPosition, int binWidth)
        {
            return (rawStartPosition / binWidth) * binWidth;
        }

        public List<TransformBlock<PairResult[], PairResult[]>> GetAndLinkPerClassificationBlocksWithEcFinalization(ISourceBlock<PairResult> pairClassifierBlock,
            PairClassification classification, 
            ConcurrentDictionary<string, IndelEvidence> indelLookup)
        {
            var writerBuffer = _batchBlockFactory.GetBlock();
            pairClassifierBlock.LinkTo(writerBuffer,
                new DataflowLinkOptions { PropagateCompletion = true, Append = true }, (p) => p.Classification == classification);

            var passThruBlock = EcPassThruBlock(_targetFinder, _chrom, indelLookup);

            writerBuffer.LinkTo(passThruBlock, new DataflowLinkOptions { PropagateCompletion = true, Append = true });

            return new List<TransformBlock<PairResult[], PairResult[]>>() {passThruBlock};
        }

     

        private TransformBlock<PairResult[], PairResult[]> EcPassThruBlock(IndelTargetFinder targetFinder, string chrom, ConcurrentDictionary<string, IndelEvidence> indelLookup)
        {
            var ecFinalBlock = new TransformBlock<PairResult[], PairResult[]>((pairs) =>
            {
                return IndelEvidenceCollector.CollectIndelEvidence(targetFinder, chrom, indelLookup, pairs);
            }, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism
            });
            return ecFinalBlock;
        }

        private TransformBlock<RegionDataForAggregation, AggregateRegionResults>
            AggregateTask(
                ConcurrentDictionary<string, IndelEvidence> indelLookup, int startPosition, int endPosition, bool isFinalTask, ConcurrentDictionary<string, int> progressTracker)
        {
            var finalTask = new TransformBlock<RegionDataForAggregation, AggregateRegionResults>((regionData) =>
            {
                return _aggregateRegionProcessor.GetAggregateRegionResults(indelLookup, 
                    startPosition, endPosition, isFinalTask, regionData);
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = _maxDegreeOfParallelism, EnsureOrdered = true });

            return finalTask;
        }




    }
}