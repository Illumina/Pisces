using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Common.IO.Utility;
using Gemini.BinSignalCollection;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.IO;
using Gemini.Logic;
using Gemini.Models;
using Gemini.Realignment;
using Gemini.Types;
using Gemini.Utility;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using StitchingLogic;

namespace Gemini
{
    public class AggregateRegionProcessor : IAggregateRegionProcessor
    {
        private readonly IGeminiDataSourceFactory _dataSourceFactory;
        private readonly RealignmentOptions _realignmentOptions;
        private readonly ConcurrentDictionary<string, IndelEvidence> _masterIndelLookup;
        private readonly ConcurrentDictionary<HashableIndel, int[]> _masterOutcomesLookup;
        private readonly ConcurrentDictionary<HashableIndel, int> _masterFinalIndels;
        private readonly List<PairClassification> _categoriesForRealignment;
        private readonly ConcurrentDictionary<string, int> _progressTracker;
        private readonly IGeminiFactory _geminiFactory;
        private readonly string _chrom;
        private readonly GeminiOptions _geminiOptions;
        private readonly ChrReference _chrReference;
        private readonly BamRealignmentFactory _bamRealignmentFactory;
        private readonly Dictionary<int, string> _refIdMapping;

        public AggregateRegionProcessor(ChrReference chrReference, Dictionary<int, string> refIdMapping,
            BamRealignmentFactory bamRealignmentFactory, GeminiOptions geminiOptions, IGeminiFactory geminiFactory, 
            string chrom, IGeminiDataSourceFactory dataSourceFactory, RealignmentOptions realignmentOptions,
            ConcurrentDictionary<string, IndelEvidence> masterIndelLookup,
            ConcurrentDictionary<HashableIndel, int[]> masterOutcomesLookup,
            ConcurrentDictionary<HashableIndel, int> masterFinalIndels, List<PairClassification> categoriesForRealignment, ConcurrentDictionary<string, int> progressTracker)
        {
            _chrReference = chrReference;
            _refIdMapping = refIdMapping;
            _bamRealignmentFactory = bamRealignmentFactory;
            _geminiOptions = geminiOptions;
            _geminiFactory = geminiFactory;
            _chrom = chrom;
            _dataSourceFactory = dataSourceFactory;
            _realignmentOptions = realignmentOptions;
            _masterIndelLookup = masterIndelLookup;
            _masterOutcomesLookup = masterOutcomesLookup;
            _masterFinalIndels = masterFinalIndels;
            _categoriesForRealignment = categoriesForRealignment;
            _progressTracker = progressTracker;
        }

        private static bool ClassificationIsStitched(PairClassification classification)
        {
            return classification == PairClassification.ImperfectStitched ||
                   classification == PairClassification.PerfectStitched ||
                   classification == PairClassification.MessyStitched || classification == PairClassification.SingleMismatchStitched;
        }

        public AggregateRegionResults GetAggregateRegionResults(ConcurrentDictionary<string, IndelEvidence> indelLookup,
            int startPosition,
            int endPosition, bool isFinalTask, RegionDataForAggregation regionData)
        {
            if (_geminiOptions.LightDebug)
            {
                Logger.WriteToLog(
                    $"Started processing for region {_chrom}:{startPosition}-{endPosition}.");
            }

            var adjustedStartPosition = regionData.EffectiveMinPosition;
            var edgeThresholdOrig = Math.Max(1, regionData.EffectiveMaxPosition - 5000);
            var finalIndelLookup = GetAndSyncFinalIndelLookup(indelLookup, _masterIndelLookup);
            var edgeState = regionData.EdgeState;
            var nextEdgeMinPosition = int.MaxValue;

            var finalizedIndels = FinalizeIndels(finalIndelLookup, _chrReference, regionData.EffectiveMaxPosition);
            var finalizedIndelsForChrom = GetFinalizedIndelsForChrom(_chrom, finalizedIndels, edgeState);

            IChromosomeIndelSource indelSource = null;

            var messySiteWidth = _geminiOptions.MessySiteWidth;
            const int binsToExtendTo = 2; // Treated as <, so 2 means we get to extend status to one on either side

            var binEvidence = regionData.BinEvidence;
            var binConclusions = new BinConclusions(binEvidence, _geminiOptions.CollectDepth, trackDirectionalMess: _geminiOptions.SilenceDirectionalMessReads, trackMapqMess: _geminiOptions.SilenceMessyMapMessReads);
            var numBins = binEvidence.NumBins;

            bool shouldRealignAtAll = finalizedIndelsForChrom.Any();

            var imperfectFreqThreshold = _geminiOptions.ImperfectFreqThreshold;
            var indelRegionfreqThreshold = _geminiOptions.IndelRegionFreqThreshold;
            var messySiteThreshold = _geminiOptions.MessySiteThreshold;

            var numRetrievedFromLastBlock = 0;
            var numPairsSentToNextBlock = 0;
            var pairResultsForNextBlock = new Dictionary<PairClassification, List<PairResult>>();

            var pairResultLookup = new Dictionary<PairClassification, List<PairResult>>();
            foreach (var key in regionData.PairResultLookup.Keys)
            {
                if (!pairResultLookup.ContainsKey(key))
                {
                    pairResultLookup.Add(key, new List<PairResult>());
                }

                pairResultLookup[key].AddRange(regionData.PairResultLookup[key]);
            }

            foreach (var category in pairResultLookup)
            {
                var isMessy = TypeClassifier.MessyTypes.Contains(category.Key);
                var isIndel = TypeClassifier._indelTypes.Contains(category.Key);
                var isSingleMismatch = _geminiOptions.AvoidLikelySnvs &&
                                       (category.Key == PairClassification.SingleMismatchStitched ||
                                        category.Key == PairClassification.UnstitchSingleMismatch);
                var isForwardOnlyMessy = IsForwardMessy(category.Key);
                var isReverseOnlyMessy = IsReverseMessy(category.Key);
                var isMapMessy = IsSuspiciousMapping(category.Key);
                foreach (var pairResult in category.Value)
                {
                    // If on the edge, kick it over to the edge lookup.
                    if (!isFinalTask && pairResult.ReadPair.MaxPosition > edgeThresholdOrig)
                    {
                        numPairsSentToNextBlock++;
                        if (!pairResultsForNextBlock.ContainsKey(category.Key))
                        {
                            pairResultsForNextBlock.Add(category.Key, new List<PairResult>());
                        }

                        pairResultsForNextBlock[category.Key].Add(pairResult);

                        nextEdgeMinPosition = Math.Min(nextEdgeMinPosition, pairResult.ReadPair.MinPosition);
                    }
                    // Still collect evidence even if it's edge, because that could impact this block as well as next block.

                    binEvidence.AddMessEvidence(isMessy, pairResult, isIndel, isSingleMismatch, isForwardOnlyMessy,
                        isReverseOnlyMessy, isMapMessy);
                }
            }

            numRetrievedFromLastBlock = AddAlignmentsFromEdgeState(edgeState, pairResultLookup, numRetrievedFromLastBlock);

            var finalizedBins = new UsableBins(binConclusions);

            if (shouldRealignAtAll)
            {
                binConclusions.AddIndelEvidence(finalizedIndelsForChrom, binsToExtendTo);
                binConclusions.ProcessRegions(messySiteThreshold, imperfectFreqThreshold,
                    _geminiOptions.RegionDepthThreshold, indelRegionfreqThreshold, binsToExtendTo, _geminiOptions.DirectionalMessThreshold);
                finalizedBins.FinalizeConclusions(binsToExtendTo);
            }

            using (var snippetSource = _dataSourceFactory.CreateGenomeSnippetSource(_chrom, _chrReference))
            {
                indelSource =
                    _dataSourceFactory.GetChromosomeIndelSource(finalizedIndelsForChrom, snippetSource);
            }


            foreach (var kvp in pairResultsForNextBlock)
            {
                foreach (var pairResult in kvp.Value)
                {
                    pairResultLookup[kvp.Key].Remove(pairResult);
                }
            }

            var allAlignments = new List<BamAlignment>();
            var outcomesLookup = new Dictionary<HashableIndel, int[]>();

            var numSkippedDueToSites = 0;
            var numKept = 0;
            var numRealigned = 0;
            var numSilenced = 0;

            var snowballCategories = _realignmentOptions.CategoriesForSnowballing;
            var doSnowball = snowballCategories.Any();


            foreach (var category in snowballCategories)
            {
                if (pairResultLookup.ContainsKey(category))
                {
                    pairResultLookup.Remove(category, out var categoryReads);
                    allAlignments.AddRange(ProcessCategory(_categoriesForRealignment,
                        indelSource, shouldRealignAtAll,
                        outcomesLookup, ref numSkippedDueToSites, ref numKept, ref numRealigned, ref numSilenced,
                        categoryReads, category, binEvidence, _progressTracker, binConclusions, finalizedBins, startPosition, endPosition));
                }
            }

            List<HashableIndel> superFinalizedIndels;

            if (doSnowball)
            {
                superFinalizedIndels = GetSuperFinalizedIndelsAfterSnowball(finalizedIndelsForChrom, outcomesLookup);

                if (_geminiOptions.Debug)
                {
                    Logger.WriteToLog(
                        $"After snowballing for region {_chrom}:{startPosition}-{endPosition}, filtered down to {superFinalizedIndels.Count} indels from {finalizedIndelsForChrom.Count} ({finalIndelLookup.Count} preliminary indels).");
                }

                using (var snippetSource = _dataSourceFactory.CreateGenomeSnippetSource(_chrom, _chrReference))
                {
                    indelSource =
                        _dataSourceFactory.GetChromosomeIndelSource(superFinalizedIndels, snippetSource);
                }

                if (_geminiOptions.RecalculateUsableSitesAfterSnowball)
                {
                    binConclusions.ResetIndelRegions();

                    foreach (var indel in superFinalizedIndels)
                    {
                        var bin = (indel.ReferencePosition - adjustedStartPosition) / messySiteWidth;
                        binConclusions.SetIndelRegionTrue(bin);

                        for (int j = 0; j < binsToExtendTo; j++)
                        {
                            var binIndex = bin - j;
                            if (binIndex >= 0)
                            {
                                binConclusions.SetIndelRegionTrue(binIndex);
                            }
                            else
                            {
                                break;
                            }
                        }

                        for (int j = 0; j < binsToExtendTo; j++)
                        {
                            var binIndex = bin + j;
                            if (!binConclusions.SetIndelRegionTrue(binIndex))
                            {
                                break;
                            }
                        }
                    }

                    finalizedBins.FinalizeConclusions(binsToExtendTo);
                }
            }
            else
            {
                superFinalizedIndels = finalizedIndelsForChrom;
            }

            // TODO pull out the allocs below, or ideally actually remove them from realign pair handler or use something different altogether
            foreach (var category in pairResultLookup)
            {
                if (snowballCategories.Contains(category.Key))
                {
                    continue;
                }

                allAlignments.AddRange(ProcessCategory(_categoriesForRealignment, indelSource,
                    shouldRealignAtAll,
                    outcomesLookup, ref numSkippedDueToSites, ref numKept, ref numRealigned, ref numSilenced, category.Value,
                    category.Key, binEvidence, _progressTracker, binConclusions, finalizedBins, startPosition, endPosition));
            }

            var edgeHits = new Dictionary<int, int>();
            var edgeSingleMismatchHits = new Dictionary<int, int>();
            var edgeIndelHits = new Dictionary<int, int>();
            var edgeMessyHits = new Dictionary<int, int>();

            PopulateEdgeHitsAndLogBins(numBins, adjustedStartPosition, messySiteWidth, nextEdgeMinPosition, binEvidence,
                edgeHits, edgeSingleMismatchHits, edgeIndelHits, edgeMessyHits, startPosition, binConclusions, finalizedBins);

            UpdateMasterOutcomes(_masterOutcomesLookup, outcomesLookup);

            foreach (var hashableIndel in superFinalizedIndels)
            {
                _masterFinalIndels.AddOrUpdate(hashableIndel, 1, (h, n) => { return n + 1; });
            }

            _progressTracker.AddOrUpdate("Flushed", allAlignments.Count(),
                (x, currentCount) => { return currentCount + allAlignments.Count(); });
            _progressTracker.AddOrUpdate("Sent To Next Block", numPairsSentToNextBlock,
                (x, currentCount) => { return currentCount + numPairsSentToNextBlock; });
            _progressTracker.AddOrUpdate("Retrieved from Past Block", numRetrievedFromLastBlock,
                (x, currentCount) => { return currentCount + numRetrievedFromLastBlock; });
            _progressTracker.AddOrUpdate("Realigned", numRealigned,
                (x, currentCount) => { return currentCount + numRealigned; });
            _progressTracker.AddOrUpdate("Attempts", numKept,
                (x, currentCount) => { return currentCount + numKept; });
            _progressTracker.AddOrUpdate("Skipped", numSkippedDueToSites,
                (x, currentCount) => { return currentCount + numSkippedDueToSites; });
            _progressTracker.AddOrUpdate("Silenced", numSilenced,
                (x, currentCount) => { return currentCount + numSilenced; });

            pairResultLookup.Clear();
            Logger.WriteToLog(
                $"Finished processing for region {_chrom}:{startPosition}-{endPosition}. {allAlignments.Count()} alignments flushed, " +
                $"{numPairsSentToNextBlock} sent to next block, {numRetrievedFromLastBlock} retrieved from {regionData.EdgeState?.Name}. " +
                $"Realigned {numRealigned}/{numKept} attempts ({numSkippedDueToSites} pairs skipped realignment), silenced {numSilenced} messy mates.");


            return new AggregateRegionResults()
            {
                EdgeState = isFinalTask
                    ? new EdgeState() { Name = "Final" }
                    : new EdgeState()
                    {
                        EdgeAlignments = pairResultsForNextBlock,
                        EdgeIndels = finalizedIndelsForChrom.Where(y => y.ReferencePosition > nextEdgeMinPosition)
                            .ToList(),
                        EffectiveMinPosition = nextEdgeMinPosition,
                        Name = $"{startPosition}-{endPosition}",
                        BinEvidence = binEvidence
                    },
                AlignmentsReadyToBeFlushed = allAlignments
            };
        }


        private static bool IsSuspiciousMapping(PairClassification category)
        {
            return category == PairClassification.UnstitchMessySuspiciousRead ||
                   category == PairClassification.UnstitchMessyIndelSuspiciousRead;
        }

        private static bool IsReverseMessy(PairClassification category)
        {
            return category == PairClassification.UnstitchReverseMessy ||
                   category == PairClassification.UnstitchReverseMessyIndel;
        }

        private static bool IsForwardMessy(PairClassification category)
        {
            return category == PairClassification.UnstitchForwardMessy ||
                   category == PairClassification.UnstitchForwardMessyIndel;
        }



        private int ReadsToSilence(PairClassification classification, BinConclusions binEvidence, PairResult pairResult)
        {
            if (_geminiOptions.SilenceSuspiciousMdReads && classification == PairClassification.UnstitchMessySuspiciousMd)
            {
                return 3;
            }

            var isForwardMessy = IsForwardMessy(classification);
            var isReverseMessy = IsReverseMessy(classification);
            var isSuspiciousMapping = IsSuspiciousMapping(classification);

            if (!isForwardMessy && !isReverseMessy && !isSuspiciousMapping)
            {
                return 0;
            }

            var silenced = 0;
            var doSilenceFwd = false;
            var doSilenceRev = false;

            var r1IsReverse = pairResult.ReadPair.Read1.IsReverseStrand();

            // This assumes that there is exactly one forward and one reverse read. 
            var fwdRead = r1IsReverse ? pairResult.ReadPair.Read2 : pairResult.ReadPair.Read1;
            var revRead = r1IsReverse ? pairResult.ReadPair.Read1 : pairResult.ReadPair.Read2;

            if (isForwardMessy)
            {
                var binId = binEvidence.GetBinId(fwdRead.Position);
                doSilenceFwd = binEvidence.GetFwdMessyStatus(binId) || binEvidence.GetMapqMessyStatus(binId);
            }
            else if (isReverseMessy)
            {
                var binId = binEvidence.GetBinId(revRead.Position);
                doSilenceRev = binEvidence.GetRevMessyStatus(binId) || binEvidence.GetMapqMessyStatus(binId);
            }
            else if (isSuspiciousMapping)
            {
                var binId = binEvidence.GetBinId(revRead.Position);
                var isMapqMessy = binEvidence.GetMapqMessyStatus(binId);

                doSilenceFwd = isMapqMessy;
                doSilenceRev = isMapqMessy;

            }

            if (doSilenceFwd)
            {
                silenced = r1IsReverse ? 2 : 1;
            }

            if (doSilenceRev)
            {
                silenced = r1IsReverse ? 1 : 2;
            }

            if (doSilenceFwd && doSilenceRev)
            {
                silenced = 3;
            }

            return silenced;
        }

        private List<BamAlignment> ProcessCategory(
            List<PairClassification> categoriesForRealignment, IChromosomeIndelSource indelSource,
            bool shouldRealignAtAll, Dictionary<HashableIndel, int[]> outcomesLookup, ref int numSkippedDueToSites,
            ref int numKept, ref int numRealigned, ref int numSilenced,
            List<PairResult> pairResults, PairClassification classification, IBinEvidence binEvidence,
            ConcurrentDictionary<string, int> progressTracker, BinConclusions binConclusions, UsableBins usableBins, int startPosition, int endPosition)
        {
            var allAlignments = new List<BamAlignment>();
            var isHighLikelihoodForRealign = false;

            if (_geminiOptions.ForceHighLikelihoodRealigners)
            {
                var highLikelihoodCategories = new List<PairClassification>()
                {
                    PairClassification.Disagree,
                    PairClassification.MessyStitched,
                    PairClassification.MessySplit,
                    PairClassification.UnstitchMessy,
                    PairClassification.UnstitchIndel
                };
                isHighLikelihoodForRealign = highLikelihoodCategories.Contains(classification);
            }

            int alignmentsCount = 0;

            var doRealign = false;
            ReadPairRealignerAndCombiner realignHandler = null;
            var alreadyStitched = ClassificationIsStitched(classification);
            var doStitch = !_geminiOptions.SkipStitching && TypeClassifier.ClassificationIsStitchable(classification);
            var categoryIsRealignable = categoriesForRealignment.Contains(classification);

            if (categoryIsRealignable || doStitch)
            {
                doRealign = true;

                realignHandler = _bamRealignmentFactory.GetRealignPairHandler(doStitch,
                    alreadyStitched,
                    _realignmentOptions.PairAwareEverything ||
                    ClassificationIsPairAwareRealignable(classification),
                    _refIdMapping,
                    new ReadStatusCounter(), false, indelSource, _chrom, new Dictionary<string, IndelEvidence>(),
                    ClassificationHasIndels(classification), outcomesLookup
                    , SkipRestitchIfUnchanged(classification));
            }

            using (var snippetSource = _dataSourceFactory.CreateGenomeSnippetSource(_chrom, _chrReference))
            using (var singleSnippetSource = new ReusableSnippetSource(snippetSource))
            {
                var nmCalculator = new NmCalculator(singleSnippetSource);
                
                var classificationString = classification.ToString();
                foreach (var pairResult in pairResults)
                {
                    int toSilence = 0;

                    IEnumerable<BamAlignment> alignments;
                    if (!doRealign)
                    {
                        alignments = pairResult.Alignments;
                    }
                    else
                    {
                        bool doRealignPair =
                            shouldRealignAtAll && (isHighLikelihoodForRealign ||
                                                   (categoryIsRealignable &&
                                                    (usableBins.IsPositionUsable(pairResult.ReadPair.MinPosition) ||
                                                     usableBins.IsPositionUsable(pairResult.ReadPair.MaxPosition))));


                        if (!doRealignPair)
                        {
                            numSkippedDueToSites++;
                        }
                        else
                        {
                            numKept++;
                        }

                        toSilence = ReadsToSilence(classification, binConclusions, pairResult);
                        if (toSilence > 0)
                        {
                            numSilenced++;
                        }

                        alignments = realignHandler.ExtractReads(pairResult, nmCalculator, doRealignPair, toSilence);

                        if (pairResult.ReadPair.Realigned || pairResult.ReadPair.RealignedR1 ||
                            pairResult.ReadPair.RealignedR2)
                        {
                            numRealigned++;
                        }
                    }

                    var silencedR1 = (toSilence == 1 || toSilence == 3) && !pairResult.ReadPair.RealignedR1;
                    var silencedR2 = (toSilence == 2 || toSilence == 3) && !pairResult.ReadPair.RealignedR2;
                    var readTreatment = ReadTreatment(silencedR1, silencedR2, pairResult);

                    progressTracker.AddOrUpdate(classificationString + ":" + readTreatment, 1,
                        (x, currentCount) => { return currentCount + 1; });

                    var alignmentsList = alignments.ToList();
                    foreach (var bamAlignment in alignmentsList)
                    {
                        if (_geminiOptions.LightDebug)
                        {
                            AddMdTagCountsTags(bamAlignment, pairResult);
                        }

                        bamAlignment.ReplaceOrAddStringTag("XT", readTreatment);
                        bamAlignment.ReplaceOrAddStringTag("XP", classificationString);
                    }

                    alignmentsCount += alignmentsList.Count();
                    allAlignments.AddRange(alignmentsList);
                }
            }

            if (realignHandler != null)
            {
                realignHandler.Finish();
            }

            pairResults.Clear();
            return allAlignments;
        }

        private static string ReadTreatment(bool silencedR1, bool silencedR2, PairResult pairResult)
        {
            var silencedStatus = silencedR1 || silencedR2
                ? ($"_Silenced{(silencedR1 ? "R1" : "")}{(silencedR2 ? "R2" : "")}")
                : "";

            var readTreatment =
                (pairResult.ReadPair.RealignedR1 ? "R1Realigned" :
                    pairResult.R1Confirmed ? "R1Confirmed" : "R1Untouched") + "_" +
                (pairResult.ReadPair.RealignedR2 ? "R2Realigned" :
                    pairResult.R2Confirmed ? "R2Confirmed" : "R2Untouched") + "_" +
                (pairResult.ReadPair.Stitched ? "Stitched" :
                    pairResult.ReadPair.BadRestitch ? "BadRestitch" :
                    pairResult.ReadPair.FailForOtherReason ? "FailStitch" :
                    pairResult.ReadPair.Disagree ? "Disagree" :
                    pairResult.TriedStitching ? "GenericFailStitch" : "Unstitch") + silencedStatus;
            return readTreatment;
        }

        private static void AddMdTagCountsTags(BamAlignment bamAlignment, PairResult pairResult)
        {
            if (pairResult.md1.IsSet)
            {
                bamAlignment.ReplaceOrAddStringTag("XM",
                    $"A: {pairResult.md1.A},{pairResult.md2.A}; T: {pairResult.md1.T},{pairResult.md2.T}; C: {pairResult.md1.C},{pairResult.md2.C}; G: {pairResult.md1.G},{pairResult.md2.G}; subA: {pairResult.md1.SubA},{pairResult.md2.SubA}; subT: {pairResult.md1.SubT},{pairResult.md2.SubT}; subC: {pairResult.md1.SubC},{pairResult.md2.SubC}; subG: {pairResult.md1.SubG},{pairResult.md2.SubG}; subN: {pairResult.md1.SubN},{pairResult.md2.SubN}; maxRun: {pairResult.md1.RunLength},{pairResult.md2.RunLength}; numRuns: {pairResult.md1.NumInRuns},{pairResult.md2.NumInRuns}");
            }
            else
            {
                bamAlignment.ReplaceOrAddStringTag("XM", $"Not set");
            }
        }

        private void PopulateEdgeHitsAndLogBins(int numBins, int adjustedStartPosition, int messySiteWidth, int edgeThreshold,
            IBinEvidence binEvidence, Dictionary<int, int> edgeHits, Dictionary<int, int> edgeSingleMismatchHits, Dictionary<int, int> edgeIndelHits,
            Dictionary<int, int> edgeMessyHits, int startPosition, BinConclusions binConclusions, UsableBins usableBins)
        {
            for (int binId = 0; binId < numBins; binId++)
            {
                var inEdge = false;
                var binStart = adjustedStartPosition + (binId * messySiteWidth);

                if (_geminiOptions.LogRegionsAndRealignments)
                {
                    if (binEvidence.GetAllHits(binId) > 10 && !inEdge)
                    {
                        var binCounts =
                            $"{binId},{inEdge},{binStart},{binStart + messySiteWidth},{binEvidence.GetAllHits(binId)},{usableBins.IsPositionUsable(binStart)},{binEvidence.GetSingleMismatchHit(binId)}," +
                            $"{binConclusions.GetProbableTrueSnvRegion(binId)},{binEvidence.GetIndelHit(binId)},{binConclusions.GetIndelRegionHit(binId)}," +
                            $"{binEvidence.GetMessyHit(binId)},{binConclusions.GetIsMessyEnough(binId)},{binEvidence.GetForwardMessyRegionHit(binId)},{binConclusions.GetFwdMessyStatus(binId)},{binEvidence.GetReverseMessyRegionHit(binId)},{binConclusions.GetRevMessyStatus(binId)},{binEvidence.GetMapqMessyHit(binId)},{binConclusions.GetMapqMessyStatus(binId)}";

                        // TODO consider writing this to a proper output file
                        if (_geminiOptions.LogRegionsAndRealignments)
                        {
                            Logger.WriteToLog("BINCOUNTS\t" + binCounts);
                        }
                    }
                }
            }
        }

        private List<HashableIndel> GetSuperFinalizedIndelsAfterSnowball(List<HashableIndel> finalizedIndelsForChrom,
            Dictionary<HashableIndel, int[]> outcomesLookup)
        {
            List<HashableIndel> superFinalizedIndels;
            superFinalizedIndels = new List<HashableIndel>();

            foreach (var item in finalizedIndelsForChrom)
            {
                if (outcomesLookup.ContainsKey(item))
                {
                    var outcome = outcomesLookup[item];
                    var confirmed = outcome[5];
                    var accepted = outcome[6];
                    var otherAccepted = outcome[7];
                    var numConfirmedOrAccepted = confirmed + accepted;
                    var notConfirmedOrAccepted = numConfirmedOrAccepted == 0;
                    var moreOtherAccepted = otherAccepted > numConfirmedOrAccepted * 1.5;
                    if (outcome[1] > 5 && (notConfirmedOrAccepted || moreOtherAccepted))
                    {
                        // Not good enough
                    }
                    else
                    {
                        superFinalizedIndels.Add(item);
                    }
                }
                else
                {
                    if (!_geminiOptions.RequirePositiveOutcomeForSnowball)
                    {
                        superFinalizedIndels.Add(item);
                    }
                }
            }

            return superFinalizedIndels;
        }


        private static bool ClassificationIsPairAwareRealignable(PairClassification classification)
        {
            return classification == PairClassification.Disagree ||
                   classification == PairClassification.FailStitch ||
                   classification == PairClassification.UnstitchIndel;
        }

        private static bool ClassificationHasIndels(PairClassification classification)
        {
            // TODO any others? What if we did already try to stitch?
            return classification == PairClassification.Disagree ||
                   classification == PairClassification.UnstitchIndel ||
                   classification == PairClassification.IndelImproper ||
                   classification == PairClassification.IndelSingleton ||
                   classification == PairClassification.IndelUnstitchable ||
                   classification == PairClassification.UnstitchForwardMessyIndel ||
                   classification == PairClassification.UnstitchReverseMessyIndel ||
                   classification == PairClassification.UnstitchMessyIndelSuspiciousRead ||
                   classification == PairClassification.UnstitchMessyIndel;
        }

        private static bool SkipRestitchIfUnchanged(PairClassification classification)
        {
            return classification == PairClassification.FailStitch ||
                   classification == PairClassification.LongFragment ||
                   classification == PairClassification.Unstitchable;
        }

        private Dictionary<string, List<HashableIndel>> FinalizeIndels(
            Dictionary<string, IndelEvidence> indelStringLookup, ChrReference chrReference,
            int edgeThresholdForPriorSupport)
        {
            var indelsLookup = _geminiFactory.GetIndelFilterer().GetRealignablePreIndels(indelStringLookup, true, edgeThresholdForPriorSupport);

            if (_geminiOptions.Debug)
            {
                Logger.WriteToLog(
                    $"Filtered down to {indelsLookup.Values.Sum(x => x.Count)} out of {indelStringLookup.Keys.Count}.");

            }

            var chromIndelsLookup = new Dictionary<string, List<HashableIndel>>();

            foreach (var chrom in indelsLookup.Keys)
            {
                var preIndelsForChromosome = indelsLookup.ContainsKey(chrom) ? indelsLookup[chrom] : new List<PreIndel>();
                var indelsForChrom = _geminiFactory.GetIndelPruner().GetPrunedPreIndelsForChromosome(preIndelsForChromosome);

                if (_geminiOptions.LightDebug)
                {
                    foreach (var candidateIndel in indelsForChrom.OrderBy(x => x.ReferencePosition))
                    {
                        Logger.WriteToLog(candidateIndel + " " + candidateIndel.Score);
                    }
                }

                var chromIndels = _dataSourceFactory.GetHashableIndelSource()
                    .GetFinalIndelsForChromosome(chrom, indelsForChrom, chrReference);

                chromIndelsLookup.Add(chrom, chromIndels);
            }

            return chromIndelsLookup;
        }

        private static int AddAlignmentsFromEdgeState(EdgeState edgeState, Dictionary<PairClassification, List<PairResult>> pairResultLookup,
            int numRetrievedFromLastBlock)
        {
            if (edgeState != null)
            {
                foreach (var key in edgeState.EdgeAlignments.Keys)
                {
                    if (!pairResultLookup.ContainsKey(key))
                    {
                        pairResultLookup.Add(key, new List<PairResult>());
                    }

                    var alns = edgeState.EdgeAlignments[key];
                    pairResultLookup[key].AddRange(alns);
                    numRetrievedFromLastBlock += alns.Count;
                }
            }

            return numRetrievedFromLastBlock;
        }

        private static List<HashableIndel> GetFinalizedIndelsForChrom(string chrom, Dictionary<string, List<HashableIndel>> finalizedIndels, EdgeState edgeState)
        {
            var finalizedIndelsForChrom = finalizedIndels.ContainsKey(chrom)
                ? finalizedIndels[chrom]
                : new List<HashableIndel>();

            if (edgeState != null)
            {
                finalizedIndelsForChrom.AddRange(edgeState.EdgeIndels);
                finalizedIndelsForChrom = finalizedIndelsForChrom.Distinct().ToList();
            }

            return finalizedIndelsForChrom;
        }

        private static Dictionary<string, IndelEvidence> GetAndSyncFinalIndelLookup(ConcurrentDictionary<string, IndelEvidence> indelLookup,
            ConcurrentDictionary<string, IndelEvidence> masterIndelLookup)
        {
            var finalIndelLookup = new Dictionary<string, IndelEvidence>();
            foreach (var kvp in indelLookup)
            {
                if (kvp.Value.Observations > 0)
                {
                    finalIndelLookup.Add(kvp.Key, kvp.Value);

                    masterIndelLookup.AddOrUpdate(kvp.Key, kvp.Value, (k, v) =>
                    {
                        v.AddIndelEvidence(kvp.Value);
                        return v;
                    });
                }
            }

            return finalIndelLookup;
        }

        private static void UpdateMasterOutcomes(ConcurrentDictionary<HashableIndel, int[]> masterOutcomesLookup, Dictionary<HashableIndel, int[]> outcomesLookup)
        {
            foreach (var kvp in outcomesLookup)
            {
                masterOutcomesLookup.AddOrUpdate(kvp.Key, kvp.Value, (k, v) =>
                {
                    for (int index = 0; index < v.Length; index++)
                    {
                        v[index] += kvp.Value[index];
                    }

                    return v;
                });
            }
        }


    }
}