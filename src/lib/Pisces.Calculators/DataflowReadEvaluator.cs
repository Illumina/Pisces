using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Common.IO.Utility;
using Gemini.Interfaces;
using Gemini.Types;
using Gemini.Utility;
using StitchingLogic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ReadRealignmentLogic.Models;
using IndelEvidence = Gemini.IndelCollection.IndelEvidence;

namespace Gemini
{
    public class DataflowReadEvaluator
    {
        private readonly GeminiOptions _geminiOptions;
        private readonly IGeminiDataSourceFactory _dataSourceFactory;
        private readonly GeminiSampleOptions _geminiSampleOptions;
        private readonly IGeminiDataOutputFactory _dataOutputFactory;
        private readonly IBlockFactorySource _blockFactorySource;

        public DataflowReadEvaluator(GeminiOptions geminiOptions, 
            IGeminiDataSourceFactory dataSourceFactory, GeminiSampleOptions geminiSampleOptions, IGeminiDataOutputFactory dataOutputFactory, IBlockFactorySource blockFactorySource)
        {
            _geminiOptions = geminiOptions;
            _dataSourceFactory = dataSourceFactory;
            _geminiSampleOptions = geminiSampleOptions;
            _dataOutputFactory = dataOutputFactory;
            _blockFactorySource = blockFactorySource;
        }


        public EvidenceAndClassificationResults ProcessBam()
        {
            var refIdMapping = _dataSourceFactory.GetRefIdMapping(_geminiSampleOptions.InputBam);
            var chrom = _geminiSampleOptions.RefId == null
                ? "Unk"
                : refIdMapping[_geminiSampleOptions.RefId.Value];

            var indelLookup = new ConcurrentDictionary<string, IndelEvidence>();
            var classifications1 = new ConcurrentDictionary<PairClassification, int>();
            foreach (var value in Enum.GetValues(typeof(PairClassification)))
            {
                classifications1.AddOrUpdate((PairClassification)value, 0, (x, y) => 0);
            }

            var progressTracker = new ConcurrentDictionary<string, int>();

            var masterOutcomesLookup = new ConcurrentDictionary<HashableIndel, int[]>();
            var masterFinalIndels = new ConcurrentDictionary<HashableIndel, int>();
            var categorizedAlignments = new Dictionary<string, Dictionary<PairClassification, List<string>>>();
            var borderlinePairs = new ConcurrentDictionary<string, ReadPair>();
            var categoryLookup = new ConcurrentDictionary<PairClassification, int>();


            using (var bamReader = _dataSourceFactory.CreateBamReader(_geminiSampleOptions.InputBam))
            {
                using (var readPairSource = _dataSourceFactory.CreateReadPairSource(bamReader, new ReadStatusCounter()))
                {

                    var pairBatchBlockFactory = _blockFactorySource.GetBatchBlockFactory();
                    var classifierBlockFactory = _blockFactorySource.GetClassifierBlockFactory();


                    ReadPair readPairEntry;
                    var edgeStates = new ConcurrentDictionary<int, EdgeState>();
                    var edgeStatesTasks = new ConcurrentDictionary<int, Task>();
                    var writerSource = _dataOutputFactory.GetWriterSource(_geminiSampleOptions.InputBam,
                        _geminiSampleOptions.OutputBam);
                    var tasksToWaitOn = new List<Task>();
 
                    var blockSize = _geminiOptions.RegionSize;
                    var currentBlockEnd = blockSize;
                    var currentBlockStart = 0;
                    var prevBlockStart = -1;

                    var chrReference = _dataSourceFactory.GetChrReference(chrom);


                    var classificationBlockProvider = _blockFactorySource.GetBlockProvider(refIdMapping, chrom, writerSource,
                        progressTracker, categoryLookup, indelLookup, masterOutcomesLookup, masterFinalIndels, chrReference);

                    var lineBuffer = pairBatchBlockFactory.GetBlock();
                    var pairClassifierBlock = classifierBlockFactory.GetClassifierBlock("0");
                    lineBuffer.LinkTo(pairClassifierBlock, new DataflowLinkOptions { PropagateCompletion = true });

                    var currentTask = classificationBlockProvider.GetAndLinkAllClassificationBlocksWithEcFinalization(
                        pairClassifierBlock, currentBlockStart, currentBlockEnd, edgeStates, edgeStatesTasks, 
                        prevBlockStart);
                    tasksToWaitOn.AddRange(currentTask);

                    var numReadsRead = 0;
                    var readsSinceLastTime = 0;
                    var numReadsFlushedAsSingles = 0;

                    while ((readPairEntry = readPairSource.GetNextEntryUntilNull()) != null)
                    {
                        numReadsRead++;
                        readsSinceLastTime++;
                        var pastCurrentBlock = readPairEntry.MinPosition > currentBlockEnd;

                        if (pastCurrentBlock)
                        {
                            var readPosition = readPairEntry.MinPosition;

                            var waiting = readPairSource.GetWaitingEntries(currentBlockEnd);
                            foreach (var rp in waiting)
                            {
                                numReadsFlushedAsSingles = NumReadsFlushedAsSingles(rp, borderlinePairs, numReadsFlushedAsSingles, lineBuffer);

                                numReadsRead++;
                                readsSinceLastTime++;
                            }

                            lineBuffer.TriggerBatch();
                            lineBuffer.Complete();

                            if (_geminiOptions.LightDebug)
                            {
                                Logger.WriteToLog(
                                    $"Processing block {currentBlockStart}-{currentBlockEnd}. Next block will start at {readPosition}. Currently processing " +
                                    tasksToWaitOn.Count +
                                    " tasks " + 
                                    $"({tasksToWaitOn.Count(x => x.IsCompleted)} completed)");
                            }

                            while (true)
                            {
                                tasksToWaitOn = CheckAndClearTasks(tasksToWaitOn, null);
                                if (tasksToWaitOn.Count <= _geminiOptions.NumConcurrentRegions * 3)
                                {
                                    break;
                                }
                            }

                            prevBlockStart = currentBlockStart;
                            currentBlockStart = currentBlockEnd + 1;
                            currentBlockEnd = readPosition + blockSize;

                            var lineBufferNew = pairBatchBlockFactory.GetBlock();
                            var newPairClassifierBlock = classifierBlockFactory.GetClassifierBlock(currentBlockStart.ToString());
                            lineBufferNew.LinkTo(newPairClassifierBlock, new DataflowLinkOptions { PropagateCompletion = true });

                            var newTasks = classificationBlockProvider.GetAndLinkAllClassificationBlocksWithEcFinalization(
                                newPairClassifierBlock, 
                                currentBlockStart, currentBlockEnd, edgeStates, edgeStatesTasks, prevBlockStart);
                            tasksToWaitOn.AddRange(newTasks);

                            readsSinceLastTime = 0;

                            lineBuffer = lineBufferNew;
                        }

                        lineBuffer.Post(readPairEntry);
                    }

                    tasksToWaitOn = ClearCompletedTasks(tasksToWaitOn);

                    var finalEntries = readPairSource.GetWaitingEntries().ToList();
                    Console.WriteLine($"Got {finalEntries.Count()} final entries.");
                    foreach (var rp in finalEntries)
                    {
                        numReadsFlushedAsSingles = NumReadsFlushedAsSingles(rp, borderlinePairs, numReadsFlushedAsSingles, lineBuffer);

                        numReadsRead++;
                        readsSinceLastTime++;

                        //lineBuffer.Post(rp);
                    }

                    Logger.WriteToLog($"Borderline pairs left: {borderlinePairs.Count}");
                    var writerHandle = writerSource.BamWriterHandle(chrom, PairClassification.Unknown, 0);
                    var missingMatesWritten = 0;
                    foreach (var borderlinePair in borderlinePairs.Values)
                    {
                        // These pairs (claim to have a mate nearby but the mate is not found in the bam) shouldn't exist in a real, well-formed bam.
                        // They do come up if we're dealing with subsetted bams (e.g. for testing purposes).
                        // Instead of passing these reads through the normal processing, pass them straight to the bam. We don't really know what else to do with them.
                        Logger.WriteWarningToLog($"WARNING: Unable to properly process pair: {borderlinePair.Name} - never found mate. Writing to bam as-is.");
                        foreach (var alignment in borderlinePair.GetAlignments())
                        {
                            writerHandle.WriteAlignment(alignment);
                            missingMatesWritten++;
                        }
                    }
                    writerSource.DoneWithWriter(chrom, PairClassification.Unknown, 0, missingMatesWritten,
                        writerHandle);
                    Logger.WriteToLog($"Wrote {missingMatesWritten} reads with missing mates to bam.");

                    lineBuffer.TriggerBatch();
                    lineBuffer.Complete();

                    prevBlockStart = currentBlockStart;
                    currentBlockStart = currentBlockEnd + 1;
                    currentBlockEnd = currentBlockEnd + blockSize;

                    var lineBufferFinal = pairBatchBlockFactory.GetBlock();
                    var finalPairClassifierBlock = classifierBlockFactory.GetClassifierBlock(currentBlockStart.ToString());
                    lineBufferFinal.LinkTo(finalPairClassifierBlock, new DataflowLinkOptions { PropagateCompletion = true });


                    var finalTasks = classificationBlockProvider.GetAndLinkAllClassificationBlocksWithEcFinalization(
                        finalPairClassifierBlock, 
                        currentBlockStart, currentBlockEnd, edgeStates, edgeStatesTasks, prevBlockStart, true);
                    tasksToWaitOn.AddRange(finalTasks);

                    lineBuffer = lineBufferFinal;


                    Logger.WriteToLog($"Triggering last buffer batch. Read {numReadsRead} read pairs. Flushed {numReadsFlushedAsSingles} singles.");
                    lineBuffer.TriggerBatch();
                    Logger.WriteToLog("Completing buffer");
                    lineBuffer.Complete();

                    tasksToWaitOn = tasksToWaitOn.Where(x => !x.IsCompleted).ToList();
                    Logger.WriteToLog($"Now waiting on {tasksToWaitOn.Count} tasks.");
                    try
                    {
                        Task.WaitAll(tasksToWaitOn.ToArray());
                        tasksToWaitOn = tasksToWaitOn.Where(x => !x.IsCompleted).ToList();
                        if (tasksToWaitOn.Any())
                        {
                            throw new Exception($"Some tasks did not complete");
                        }
                    }
                    catch (AggregateException e)
                    {
                        Logger.WriteExceptionToLog(e);
                        Logger.WriteToLog("Status of tasks:\n");
                        foreach (var t in tasksToWaitOn)
                        {
                            Logger.WriteToLog(t.Status.ToString());
                        }

                        throw;
                    }
                    Logger.WriteToLog("Done waiting on tasks.");

                    writerSource.Finish();
                    categorizedAlignments[chrom] = new Dictionary<PairClassification, List<string>>();
                    categorizedAlignments[chrom][PairClassification.Unknown] = writerSource.GetBamFiles();
                }


            }

            // Force GC because we're about to hand off to samtools, which doesn't fall under our purview
            GC.Collect();

            var indelEvidence = new Dictionary<string, IndelEvidence>();

            foreach (var kvp in indelLookup)
            {
                indelEvidence.Add(kvp.Key, kvp.Value);
            }

            Logger.WriteToLog(
                $"Found {indelLookup.Keys.Count} total indels, and {masterFinalIndels.Count} eligible for realignment.");
            var outcomesWriter = new OutcomesWriter(_geminiSampleOptions.OutputFolder, _dataOutputFactory);
            outcomesWriter.CategorizeProgressTrackerAndWriteCategoryOutcomesFile(progressTracker);


            foreach (var item in categoryLookup.Keys.OrderBy(x => x.ToString()))
            {
                Logger.WriteToLog($"CATEGORY {item}: {categoryLookup[item]}");
            }

            outcomesWriter.WriteIndelOutcomesFile(masterOutcomesLookup);
            outcomesWriter.WriteIndelsFile(masterFinalIndels);

            return new EvidenceAndClassificationResults()
            {
                IndelEvidence = indelEvidence,
                CategorizedBams = categorizedAlignments
            };
        }

        private List<Task> CheckAndClearTasks(List<Task> tasksToWaitOn, Dictionary<int, int> readsPerTask = null)
        {
            tasksToWaitOn = ClearCompletedTasks(tasksToWaitOn, readsPerTask);

            var tooManyTasks = tasksToWaitOn.Count >= _geminiOptions.NumConcurrentRegions + 2;
            if (tooManyTasks)
            {
                if (_geminiOptions.Debug)
                {
                    Logger.WriteToLog(
                        $"Waiting for one task to complete. Too many tasks: {tooManyTasks} ({tasksToWaitOn.Count} vs {_geminiOptions.NumConcurrentRegions})");
                }

                Task.WaitAny(tasksToWaitOn.ToArray());
            }

            return tasksToWaitOn;
        }


        private static int NumReadsFlushedAsSingles(ReadPair rp, ConcurrentDictionary<string, ReadPair> borderlinePairs,
            int numReadsFlushedAsSingles, BatchBlock<ReadPair> lineBuffer)
        {
            if ((ReadIsNearby(rp.Read1) || ReadIsNearby(rp.Read2)) && !(rp.DontOverlap.HasValue && !rp.DontOverlap.Value) &&
                !rp.IsImproper)
            {
                if (!borderlinePairs.ContainsKey(rp.Name))
                {
                    borderlinePairs[rp.Name] = rp;
                }
                else
                {
                    numReadsFlushedAsSingles++;
                    // Wait til we find the mate, then post to the next block
                    // TODO should we actually post the earlier one to both blocks and let it carry itself over? Unfortunately by the time we get to edge state we've already done pair resolution, so starting with this because it's simpler and probably rare.
                    // TODO document as limitation
                    foreach (var aln in rp.GetAlignments())
                    {
                        borderlinePairs[rp.Name].AddAlignment(aln);
                    }

                    borderlinePairs.Remove(rp.Name, out var pairToPost);
                    pairToPost.PairStatus = PairStatus.Paired;

                    lineBuffer.Post(pairToPost);
                }
            }
            else
            {
                lineBuffer.Post(rp);
            }

            return numReadsFlushedAsSingles;
        }

        private static bool ReadIsNearby(BamAlignment read)
        {
            if (read == null)
            {
                return false;
            }
            return read.Position >= 0 && read.MatePosition > 0 && Math.Abs(read.MatePosition - read.Position) <= 5000;
        }

        private List<Task> ClearCompletedTasks(List<Task> tasksToWaitOn, Dictionary<int, int> readsPerTask =null)
        {
            var newTasksToWaitOnBeforeWait = new List<Task>();
            foreach (var task in tasksToWaitOn)
            {
                if (task.Status == TaskStatus.Faulted)
                {
                    Logger.WriteToLog($"TASK FAULTED!: {task.Exception.Message}");
                    Logger.WriteExceptionToLog(task.Exception);
                    throw task.Exception;
                    throw new Exception("Task faulted.", task.Exception);
                }
                if (task.IsCompleted)
                {
                    if (_geminiOptions.Debug)
                    {
                        Logger.WriteToLog($"Removing completed task: {task.Id}");
                    }
                }
                else
                {
                    newTasksToWaitOnBeforeWait.Add(task);
                }
            }

            return newTasksToWaitOnBeforeWait;
        }

    }
}