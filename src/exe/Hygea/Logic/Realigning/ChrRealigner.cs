using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using RealignIndels.Interfaces;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Processing.Utility;
using Common.IO.Utility;
using Pisces.Processing.Interfaces;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Interfaces;
using ReadRealignmentLogic.Models;
using ReadRealignmentLogic.Utlity;

namespace RealignIndels.Logic
{
    public class ChrRealigner : IChrRealigner
    {
        private ChrReference _chrReference;

        private AlignmentComparer _alignmentComparer;
        private IAlignmentExtractor _extractorForCandidates;
        private IAlignmentExtractor _extractorForRealign;
        private IIndelCandidateFinder _indelFinder;
        private IIndelRanker _indelRanker;
        private ITargetCaller _caller;
        private RealignStateManager _stateManager;
        private IRealignmentWriter _writer;
        private List<CandidateIndel> _knownIndels;
        private int _maxIndelSize;
        public int TotalRealignedReads { get; private set; }
        private Read _lastExtractedRead;
        private ReadRealigner _readRealigner;
        private int _anchorSizeThreshold;
        private bool _skipDuplicates;
        private bool _skipAndRemoveDuplicates;
        private bool _allowRescoringOrig0;
        private readonly int _maxRealignShift;
        private bool _tryRealignCleanSoftclippedReads;
        private AlignmentScorer _alignmentScorer;
        private bool _debug = true;

        public ChrRealigner(ChrReference chrReference, IAlignmentExtractor extractorForCandidates,
            IAlignmentExtractor extractorForRealign, IIndelCandidateFinder indelFinder, IIndelRanker indelRanker,
            ITargetCaller caller, RealignStateManager stateManager, IRealignmentWriter writer,
            List<CandidateAllele> knownIndels = null, int maxIndelSize = 25, bool tryThree = false,
            int anchorSizeThreshold = 10, bool skipDuplicates = false, bool skipAndRemoveDuplicates = false, bool remaskSoftclips = true, bool maskPartialInsertion = false, int minimumUnanchoredInsertionLength = 0,
            bool tryRealignCleanSoftclippedReads = true, bool allowRescoringOrig0 = true, int maxRealignShift = 250, AlignmentScorer alignmentScorer = null, bool debug = false)
        {
            _chrReference = chrReference;
            _extractorForCandidates = extractorForCandidates;
            _extractorForRealign = extractorForRealign;
            _indelFinder = indelFinder;
            _indelRanker = indelRanker;
            _caller = caller;
            _stateManager = stateManager;
            _writer = writer;
            _knownIndels = knownIndels == null ? null : knownIndels.Select(i => new CandidateIndel(i)).ToList();
            _maxIndelSize = maxIndelSize;
            _anchorSizeThreshold = anchorSizeThreshold;
            _skipDuplicates = skipDuplicates;
            _skipAndRemoveDuplicates = skipAndRemoveDuplicates;
            _allowRescoringOrig0 = allowRescoringOrig0;
            _maxRealignShift = maxRealignShift;
            _tryRealignCleanSoftclippedReads = tryRealignCleanSoftclippedReads;
            _alignmentScorer = alignmentScorer;
            _debug = debug;

            if (alignmentScorer != null)
            {
                _alignmentComparer = new ScoredAlignmentComparer(alignmentScorer);
            }
            else
            {
                _alignmentComparer = new BasicAlignmentComparer();
            }

            _readRealigner = new ReadRealigner(_alignmentComparer, tryThree, remaskSoftclips, maskPartialInsertion, minimumUnanchoredInsertionLength);

        }

        public void Execute()
        {
            if (_knownIndels != null)
                _stateManager.AddCandidates(_knownIndels);


            var read = new Read();
            const int chunkSize = 10000;
            var minLogThreshold = TimeSpan.FromSeconds(2);
            int lastChunk = 0;
            using (var tracker = new ElapsedTimeLogger($"Realign {_chrReference.Name}"))
                while (_extractorForCandidates.GetNextAlignment(read))
                {
                    // do not take candidates from reads with MapQ of zero, or secondary alignments
                    var doSkip = ((_skipDuplicates || _skipAndRemoveDuplicates) && read.IsPcrDuplicate) || read.MapQuality == 0  || !read.IsPrimaryAlignment;

                    if (!doSkip)
                    {
                        var candidates = _indelFinder.FindIndels(read, _chrReference.Sequence, _chrReference.Name);
                        _stateManager.AddCandidates(candidates);
                        _stateManager.AddAlleleCounts(read);

                    }

                    Realign(read.Position - 1);
                    var thisChunk = read.Position/chunkSize;
                    if (thisChunk > lastChunk)
                    {
                        tracker.LogIncrement(
                            $"Realign {_chrReference.Name}:{lastChunk*chunkSize}-{thisChunk*chunkSize}", minLogThreshold);
                    }
                    lastChunk = thisChunk;
                }

            Realign(null);


            Logger.WriteToLog("Total realigned reads: " + TotalRealignedReads);
        }

        // todo add documentation on what null
        private void Realign(int? upToPosition)
        {
            var batch = _stateManager.GetCandidatesToProcess(upToPosition);
            var candidateIndelGroups = _stateManager.GetCandidateGroups(upToPosition);


            if (batch == null ||
                (upToPosition.HasValue && batch.ClearedRegions == null))  // nothing to do
                return;

            // grab candidates and rank them
            var allTargets = batch.GetCandidates().Where(c => c.Length <= _maxIndelSize).Select(c => new CandidateIndel(c)).ToList();
            
            
            var goodTargets = _caller.Call(allTargets, _stateManager).OrderBy(t => t.ReferencePosition).ToList();

            var maxTargetSize = 0;
            if (goodTargets.Any())
                maxTargetSize = goodTargets.Max(t => t.Length) + 1;

            // realign reads
            do
            {
                if (_lastExtractedRead == null)  // first time around, get aligment 
                {
                    _lastExtractedRead = new Read();
                    if (!_extractorForRealign.GetNextAlignment(_lastExtractedRead))
                        break;
                }

                if (batch.MaxClearedPosition.HasValue && _lastExtractedRead.Position >= batch.MaxClearedPosition)
                    break;

                var bamAlignment = _lastExtractedRead.BamAlignment;

                // get original alignment summary
                var originalAlignmentSummary = _lastExtractedRead.GetAlignmentSummary(_chrReference.Sequence);

                if (_skipAndRemoveDuplicates && bamAlignment.IsDuplicate())
                    continue;

                if (!goodTargets.Any() || 
                    !bamAlignment.IsPrimaryAlignment() ||
                    (_skipDuplicates && bamAlignment.IsDuplicate()) ||
                    bamAlignment.IsSupplementaryAlignment() ||
                    bamAlignment.HasSupplementaryAlignment() ||
                    PassesSuspicion(originalAlignmentSummary))  // skip reads that are or have supplementary alignments
                {
                    _writer.WriteRead(ref bamAlignment, false);
                    continue;
                }

#if false
                Console.WriteLine("Original read has {0} mismatches {1} indels", originalAlignmentSummary.NumMismatches, originalAlignmentSummary.NumIndels);
#endif

                // try realigning
                // take realignment even if it's equal to original, this gives opportunity to known variants
                var realignResult = _readRealigner.Realign(_lastExtractedRead, goodTargets, _chrReference.Sequence,
                    _indelRanker, candidateIndelGroups, maxTargetSize);


                if (realignResult != null && RealignmentIsWithinRange(realignResult, bamAlignment) && 
                    !RealignmentIsUnchanged(realignResult, bamAlignment) && 
                    RealignmentBetterOrEqual(realignResult, originalAlignmentSummary)
                    ) 
                {
                    bamAlignment.Position = realignResult.Position - 1; // 0 base
                    bamAlignment.CigarData = realignResult.Cigar;
                    bamAlignment.UpdateIntTagData("NM", realignResult.NumMismatches + realignResult.NumIndelBases); // update NM tag (edit distance)

                    if (bamAlignment.MapQuality <= 20 && realignResult.NumMismatches == 0 && (_allowRescoringOrig0 || bamAlignment.MapQuality > 0))
                        bamAlignment.MapQuality = 40; // todo what to set this to?  
                    _writer.WriteRead(ref bamAlignment, true);

                    TotalRealignedReads++;
                }
                else
                {
                    if (realignResult != null && !RealignmentIsWithinRange(realignResult, bamAlignment))
                    {
                        Logger.WriteToLog(
                            string.Format(
                                "Realignment attempt resulted in an attempted shift of read '{6}' from {0}:{1}:{2} to {0}:{3}:{4}, which is larger than the max realign shift of {5}. Original read will be outputted.",
                                bamAlignment.RefID, bamAlignment.Position, bamAlignment.CigarData,
                                realignResult.Position - 1, realignResult.Cigar, _maxRealignShift, bamAlignment.Name));
                    }

                    // doesn't look any better
                    _writer.WriteRead(ref bamAlignment, false);
                }
            } while (_extractorForRealign.GetNextAlignment(_lastExtractedRead));

            _stateManager.DoneProcessing(batch);
        }

        private bool RealignmentIsUnchanged(RealignmentResult realignResult,
            BamAlignment originalAlignment)
        {
            return realignResult.Position - 1 == originalAlignment.Position &&
                   realignResult.Cigar.ToString() == originalAlignment.CigarData.ToString();
        }
        private bool RealignmentBetterOrEqual(RealignmentResult realignResult, AlignmentSummary originalAlignmentSummary)
        {
            return _alignmentComparer.CompareAlignmentsWithOriginal(realignResult, originalAlignmentSummary) >= 0;
        }

        private bool RealignmentIsWithinRange(RealignmentResult realignResult, BamAlignment bamAlignment)
        {
            return Math.Abs((realignResult.Position - 1) - bamAlignment.Position) < _maxRealignShift;
        }

        private bool PassesSuspicion(AlignmentSummary originalResult)
        {
            var isRealignableSoftclip = _tryRealignCleanSoftclippedReads && originalResult.NumNonNSoftclips > 0;

            if (isRealignableSoftclip) return false;

            if (originalResult.NumMismatchesIncludeSoftclip == 0 && originalResult.NumIndels == 0) return true;

            // need to try against one of the priors
            // if (originalResult.NumIndels > 0) return false; 

            // if there are only just mismatches and some are at the tail end of the read, flag it!
            // jg todo make this threshold configurable
            //return originalResult.MinNumAnchorMatches.HasValue 
            //    && originalResult.MinNumAnchorMatches > _anchorSizeThreshold; 

            return false;
        }
    }
}
