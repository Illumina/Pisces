using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alignment.Domain.Sequencing;
using Common.IO.Utility;
using Gemini.Interfaces;
using Gemini.Types;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;
using ReadRealignmentLogic.Utlity;
using Helper = Gemini.Utility.Helper;

namespace Gemini.FromHygea
{
    public partial class GeminiReadRealigner : IReadRealigner
    {
        private readonly bool _debug;
        private readonly HashableIndel[] _oneIndelSimpleTargets = new HashableIndel[1];
        private readonly HashableIndel[] _twoIndelSimpleTargets = new HashableIndel[2];

        private readonly bool _maskPartialInsertion;
        private readonly bool _keepProbeSoftclips;
        private readonly bool _keepBothSideSoftclips;
        private readonly bool _trackActualMismatches;
        private readonly bool _checkSoftclipsForMismatches;
        private readonly AlignmentComparer _comparer;
        private readonly int _minInsertionSizeToAllowMismatchingBases;
        private readonly double _maxProportionInsertSequenceMismatch;
        private readonly int _minimumUnanchoredInsertionLength;
        private SoftclipReapplier _softclipReapplier;
        private const int VeryMessyThreshold = 20;

        public GeminiReadRealigner(AlignmentComparer comparer, bool remaskSoftclips = true, bool maskPartialInsertion = true, 
            bool keepProbeSoftclips = false, bool keepBothSideSoftclips = false, bool trackActualMismatches = false, 
            bool checkSoftclipsForMismatches = false, bool debug = false, bool maskNsOnly = true, 
            int minInsertionSizeToAllowMismatchingBases = 5, double maxProportionInsertSequenceMismatch = 0.2, 
            int minimumUnanchoredInsertionLength = 0)
        {
            
            _maskPartialInsertion = maskPartialInsertion;
            _keepProbeSoftclips = keepProbeSoftclips;
            _keepBothSideSoftclips = keepBothSideSoftclips;
            _trackActualMismatches = trackActualMismatches;
            _checkSoftclipsForMismatches = checkSoftclipsForMismatches;
            _comparer = comparer;
            _minInsertionSizeToAllowMismatchingBases = minInsertionSizeToAllowMismatchingBases;
            _maxProportionInsertSequenceMismatch = maxProportionInsertSequenceMismatch;
            _minimumUnanchoredInsertionLength = minimumUnanchoredInsertionLength;
            if (_keepProbeSoftclips || _keepBothSideSoftclips)
            {
                _checkSoftclipsForMismatches = false;
                maskNsOnly = false;
            }

            _softclipReapplier = new SoftclipReapplier(remaskSoftclips, maskNsOnly, keepProbeSoftclips, keepBothSideSoftclips, trackActualMismatches, checkSoftclipsForMismatches);
            _debug = debug;
        }

        // TODO remove ranker
        public RealignmentResult Realign(Read read, List<HashableIndel> allTargets, Dictionary<HashableIndel, GenomeSnippet> indelContexts,
            bool pairSpecific, int maxIndelSize = 50)
        {
            try
            {
                var attempted = 0;
                var result = GetBestAlignment(allTargets, indelContexts, read, out attempted, pairSpecific);
#if false
                Console.WriteLine("{0}: Realigning {1} proximal targets, made {2} attempts.  Best alignment has {3} mismatches {4} indels.",
                    read.Position, readTargets.Count(), attempted, result == null ? -1 : result.NumMismatches, result == null ? -1 : result.NumIndels);
#endif

                if (result != null && result.NumMismatches >= VeryMessyThreshold)
                {
                    result = null;
                }
                if (result != null)
                {
                    var context = indelContexts[allTargets.First()];
                    var newSummary = Extensions.GetAlignmentSummary(result.Position - 1 - context.StartPosition,
                        result.Cigar,
                        context.Sequence,
                        read.Sequence, _trackActualMismatches, _checkSoftclipsForMismatches);

                    result.MismatchesIncludeSoftclip = newSummary.MismatchesIncludeSoftclip;
                    result.NumMismatchesIncludeSoftclip = newSummary.NumMismatchesIncludeSoftclip;

                    // These should already be there but....
                    result.NumMismatches = newSummary.NumMismatches;
                    result.NumInsertedBases = newSummary.NumInsertedBases;
                    result.NumIndelBases = newSummary.NumIndelBases;
                    result.NumNonNSoftclips = newSummary.NumNonNSoftclips;
                    result.NumIndels = newSummary.NumIndels;
                    result.NumMatches = newSummary.NumMatches;
                    result.Attempts = attempted;
                    result.AnchorLength = newSummary.AnchorLength;
                    result.NumMismatchesIncludeSoftclip = newSummary.NumMismatchesIncludeSoftclip;

                    if (AttemptedAddingIndelInUnanchoredRepeat(read, result, allTargets))
                    {
                        if (pairSpecific)
                        {
                            result.IsSketchy = true;
                        }
                        else
                        {
                            return null;
                        }

                        // TODO consider reinstating the logic below
                        //if (pairSpecific)
                        //{
                        //    // Allow to pass if it's a pair-specific (confirmed) indel and a perfect result
                        //    if (result.NumMismatchesIncludeSoftclip > 0)
                        //    {
                        //        return null;
                        //    }   
                        //}
                        //else
                        //{
                        //    return null;
                        //}
                    }
                }

                return result;

            }
            catch (Exception ex)
            {
                Logger.WriteExceptionToLog(new Exception(string.Format("Error aligning read '{0}'", read.Name), ex));
                return null;
            }
        }

        public static bool AttemptedAddingIndelInUnanchoredRepeat(Read read, RealignmentResult result, List<HashableIndel> indels)
        {
            {
                // TODO OBO should this be checking <= or < ? 
                var rptPrefix = read.GetMonoRepeatPrefix();
                if (rptPrefix > 3 && result.IndelsAddedAt.Min() <= rptPrefix)
                {
                    return true;
                }

                var rptSuffix = read.GetMonoRepeatSuffix();
                var lastIndel = indels[result.AcceptedIndels.Last()];
                
                if (rptSuffix > 3)
                {
                    if (lastIndel.Type == AlleleCategory.Insertion)
                    {
                        if (read.ReadLength - result.IndelsAddedAt.Max() <= rptSuffix && rptSuffix <= lastIndel.NumBasesInReferenceSuffixBeforeUnique &&
                            read.Sequence.Substring(read.Sequence.Length - rptSuffix, rptSuffix) ==
                            lastIndel.RefSuffix.Substring(0, rptSuffix))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (read.ReadLength - result.IndelsAddedAt.Max() - 1 <= rptSuffix
                            && rptSuffix <= lastIndel.NumBasesInReferenceSuffixBeforeUnique &&
                            read.Sequence.Substring(read.Sequence.Length - rptSuffix, rptSuffix) ==
                            lastIndel.RefSuffix.Substring(0, rptSuffix)
                            )
                        {
                            return true;
                        }
                    }

                    //return true;
                }
            }

            return false;
        }

        private RealignmentResult AddIndelAndGetResult(string readSequence, HashableIndel priorIndel,
            string refSequence, bool anchorLeft, PositionMap positionMap, int refSequenceStartIndex, bool pairSpecific)
        {
            var foundIndel = false;
            var insertionPostionInReadStart = -1;
            var insertionPositionInReadEnd = -1;
            var deletionPositionInRead = -1;
            bool anyPositionsAfterDeletionMapped = false;

            // TODO PERF can we bail out early if it's not possible that the indel could be inserted in the read, based on position?

            if (anchorLeft)
            {
                // move along position map to see if we can insert indel
                for (var i = 0; i < positionMap.Length; i++)
                {
                    if (positionMap.GetPositionAtIndex(i) == priorIndel.ReferencePosition && i != positionMap.Length - 1)  // make sure we dont end right before indel
                    {
                        foundIndel = true;

                        if (priorIndel.Type == AlleleCategory.Insertion)
                        {
                            insertionPostionInReadStart = i + 1;

                            // stick in -1 for insertion length, then adjust positions after
                            for (var j = i + 1; j < positionMap.Length; j++)
                            {
                                if (j - i <= priorIndel.Length)
                                {
                                    positionMap.UpdatePositionAtIndex(j, -1, true);
                                    if (j - i == priorIndel.Length || j == positionMap.Length - 1)
                                        insertionPositionInReadEnd = j;
                                }
                                else
                                {
                                    if (positionMap.GetPositionAtIndex(j) != -1) // preserve existing insertions
                                        positionMap.UpdatePositionAtIndex(j, positionMap.GetPositionAtIndex(j) - priorIndel.Length);
                                }
                            }
                            break;
                        }

                        if (priorIndel.Type == AlleleCategory.Deletion)
                        {
                            deletionPositionInRead = i;
                            // offset positions after deletion
                            for (var j = i + 1; j < positionMap.Length; j++)
                            {
                                if (positionMap.GetPositionAtIndex(j) != -1) // preserve existing insertions
                                {
                                    anyPositionsAfterDeletionMapped = true;
                                    positionMap.UpdatePositionAtIndex(j, positionMap.GetPositionAtIndex(j) + priorIndel.Length);
                                }
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                // walk backwards along position map to see if we can insert indel
                if (priorIndel.Type == AlleleCategory.Insertion)
                {
                    for (var i = positionMap.Length - 1; i >= 0; i--)
                    {
                        if (positionMap.GetPositionAtIndex(i) == priorIndel.ReferencePosition + 1 && i != 0)
                        {
                            foundIndel = true;
                            insertionPositionInReadEnd = i - 1;
                        }
                        else if (positionMap.GetPositionAtIndex(i) == priorIndel.ReferencePosition && i != positionMap.Length - 1)
                        {
                            foundIndel = true;
                            insertionPositionInReadEnd = i;
                        }

                        if (foundIndel)
                        {
                            // stick in -1 for insertion length, then adjust positions 
                            for (var j = insertionPositionInReadEnd; j >= 0; j--)
                            {
                                if (insertionPositionInReadEnd - j + 1 <= priorIndel.Length)
                                {
                                    positionMap.UpdatePositionAtIndex(j, -1, true);
                                    if (insertionPositionInReadEnd - j + 1 == priorIndel.Length || j == 0)
                                        insertionPostionInReadStart = j;
                                }
                                else
                                {
                                    if (positionMap.GetPositionAtIndex(j) != -1) // Don't update position map for things that were already -1
                                        positionMap.UpdatePositionAtIndex(j, positionMap.GetPositionAtIndex(j) + priorIndel.Length);
                                }
                            }

                            break;
                        }
                    }
                }
                else if (priorIndel.Type == AlleleCategory.Deletion)
                {
                    for (var i = positionMap.Length - 1; i >= 1; i--)
                    {
                        if (positionMap.GetPositionAtIndex(i) == priorIndel.ReferencePosition + priorIndel.Length + 1) //deletions must be fully anchored to be observed
                        {
                            foundIndel = true;

                            deletionPositionInRead = i - 1;
                            // offset positions after deletion
                            for (var j = i - 1; j >= 0; j--)
                            {
                                if (positionMap.GetPositionAtIndex(j) != -1) // preserve existing insertions
                                {
                                    anyPositionsAfterDeletionMapped = true;
                                    positionMap.UpdatePositionAtIndex(j, positionMap.GetPositionAtIndex(j) - priorIndel.Length);
                                }
                            }

                            break;
                        }
                    }
                }
            }

            //if (!foundIndel || !Helper.IsValidMap(positionMap, refSequence))
            //TODO changed this just for tailor
            if (!foundIndel || (priorIndel.Type == AlleleCategory.Deletion && !anyPositionsAfterDeletionMapped) || !Helper.IsValidMap(positionMap.Map))
                return null;

            var isSketchy = false;
            if (priorIndel.IsRepeat)
            {
                //if (priorIndel.Type == AlleleCategory.Deletion)
                //{
                //    if (Helper.RepeatDeletionFlankedByRepeats(readSequence, priorIndel, deletionPositionInRead))
                //    {
                //        return null;
                //    }
                //}

                //// TODO in the case of using sketchy anchor test:
                //// Ideally, we'd check the anchor length against how many repeats are in the reference vs the variant,
                //// ... Or maybe just always check the whole anchor if it's a repeat.
                var anchorLength = priorIndel.Type == AlleleCategory.Insertion ? Math.Min(insertionPostionInReadStart, readSequence.Length - insertionPositionInReadEnd) : Math.Min(deletionPositionInRead, readSequence.Length - deletionPositionInRead);
                if (anchorLength >= readSequence.Length)
                {
                    throw new Exception("Anchor should never be longer than read length."); // TODO remove after dev.
                }
                if (anchorLength < Math.Max(10, priorIndel.Length))
                {
                    if (priorIndel.Type == AlleleCategory.Deletion)
                    {
                        if (Helper.DeletionHasSketchyAnchor(readSequence, priorIndel, deletionPositionInRead))
                        {
                            if (pairSpecific)
                            {
                                isSketchy = true;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                    else
                    {
                        if (priorIndel.NumBasesInReferenceSuffixBeforeUnique >= anchorLength)
                        {
                            if (pairSpecific)
                            {
                                isSketchy = true;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }
            }

            // TODO do we need to be more nuanced about this and only do it in duplication areas?
            if (priorIndel.Type == AlleleCategory.Deletion)
            {
                var anchorStart = deletionPositionInRead + 1;
                var rightAnchorLength = readSequence.Length - anchorStart;
                if (rightAnchorLength < priorIndel.Length)
                {
                    if (anchorStart < readSequence.Length)
                    {
                        if (readSequence.Substring(anchorStart) ==
                            priorIndel.ReferenceAllele.Substring(1, rightAnchorLength))
                        {
                            return null;
                        }
                    }
                }
            }

            if (priorIndel.IsDuplication && priorIndel.Type == AlleleCategory.Insertion)
            {
                // TODO return to this - I think the thought was to prevent FP dups, but the implementation may have been wrong
                // No partial duplications?
                //if (readSequence.Length - insertionPositionInReadEnd <= priorIndel.Length)

                if (readSequence.Length - insertionPositionInReadEnd <= 3)
                {
                        // Assumes priors are left-aligned
                        return null;
                }
            }

            //verify insertion matches
            var newReadSequence = readSequence;
            var nifiedAt = new List<int>();
            if (priorIndel.Type == AlleleCategory.Insertion)
            {
                if (insertionPostionInReadStart == -1 || insertionPositionInReadEnd == -1)
                    return null; // weird, this shouldnt ever happen

                var readInsertedSequence = readSequence.Substring(insertionPostionInReadStart,
                    insertionPositionInReadEnd - insertionPostionInReadStart + 1);

                var indelSequence = priorIndel.AlternateAllele.Substring(1);

                if (anchorLeft && readInsertedSequence.Length < indelSequence.Length && priorIndel.NumApproxDupsRight > 0)
                {
                    // Don't allow partial realignment to dups
                    return null;
                }
                if (!anchorLeft && readInsertedSequence.Length < indelSequence.Length && priorIndel.NumApproxDupsLeft > 0)
                {
                    // Don't allow partial realignment to dups
                    return null;
                }

                var clippedPriorSequence = anchorLeft
                    ? indelSequence.Substring(0, readInsertedSequence.Length)
                    : indelSequence.Substring(indelSequence.Length - readInsertedSequence.Length);

                var isMismatch = readInsertedSequence != clippedPriorSequence;
                if (isMismatch)
                {
                    int? mismatches = null;
                    var mismatchesToDq = 0d;
                    if (priorIndel.Length >= _minInsertionSizeToAllowMismatchingBases && !(priorIndel.NumApproxDupsLeft + priorIndel.NumApproxDupsRight > 0))
                    {
                        mismatches = Helper.GetHammingNumMismatches(readInsertedSequence, clippedPriorSequence);

                        mismatchesToDq = priorIndel.Length * _maxProportionInsertSequenceMismatch;

                        if (mismatches > mismatchesToDq)
                        {
                            //Console.WriteLine(
                            //    $"Too many mismatches between insertions: {mismatches} > {maxAllowedMismatches} ({clippedPriorSequence} vs {readInsertedSequence})");
                        }
                        else
                        {
                            //Console.WriteLine(
                            //    $"Able to Nify mismatches between insertions: {mismatches} <= {maxAllowedMismatches} ({clippedPriorSequence} vs {readInsertedSequence})");

                            var newSequence =
                                Helper.NifyMismatches(clippedPriorSequence, readInsertedSequence, nifiedAt);
                            // TODO PERF is this actually necessary now that we're not actually Nifying? We can just keep the bases that we're Nifying at.
                            newReadSequence = readSequence.Substring(0, insertionPostionInReadStart) +
                                              newSequence.ToLower() +
                                              readSequence.Substring(insertionPositionInReadEnd + 1);
                            nifiedAt = nifiedAt.Select(x => x + insertionPostionInReadStart).ToList();
                        }
                    }

                    if (mismatches == null || (mismatches > mismatchesToDq))
                    {
                        return null; // inserted sequence doesn't match read
                    }
                }

          
            }

            // TODO update to use PositionMap class
            var newCigar = Helper.ConstructCigar(positionMap.Map);

            // TODO moved this, and probably should in original Hygea too?
            // Also, can cut down the calls to positionmap.First() in the original
            //var readHasPosition = positionMap.Any(p => p > 0); // Position map is one-based, so should be >, not >= 0.
            if (!positionMap.HasAnyMappableBases())
            {
                throw new InvalidDataException(string.Format("Trying to generate result and read does not have any alignable bases. ({0}, {1})", newCigar, string.Join(",", positionMap)));
            }

            var startIndexInReference = positionMap.FirstMappableBase() - 1; // Position map is one-based, so should be >, not >= 0.
            var startIndexInRefSequenceSnippet = startIndexInReference - refSequenceStartIndex;

            var newSummary = Extensions.GetAlignmentSummary(startIndexInRefSequenceSnippet, newCigar, refSequence,
                newReadSequence, _trackActualMismatches, _checkSoftclipsForMismatches);

            if (newSummary == null)
                return null;

            return new RealignmentResult()
            {
                Cigar = newCigar,
                NumIndels = newCigar.NumIndels(),
                Position = startIndexInReference + 1,
                NumMismatches = newSummary.NumMismatches,
                NumNonNMismatches = newSummary.NumNonNMismatches,
                NumSoftclips = newSummary.NumSoftclips,
                NumNonNSoftclips = newSummary.NumNonNSoftclips,
                NumDeletedBases = newSummary.NumDeletedBases,
                NumInsertedBases = newSummary.NumInsertedBases,
                NumMatches = newSummary.NumMatches,
                NumIndelBases = newSummary.NumIndelBases,
                NumMismatchesIncludeSoftclip = newSummary.NumMismatchesIncludeSoftclip,
                MismatchesIncludeSoftclip =  newSummary.MismatchesIncludeSoftclip,
                Indels = StringifyIndel(priorIndel),
                NifiedAt = nifiedAt,
                IndelsAddedAt = new List<int>{priorIndel.Type == AlleleCategory.Insertion ? insertionPostionInReadStart : deletionPositionInRead},
                IsSketchy = isSketchy
            };
        }



        private string StringifyIndel(HashableIndel indel)
        {
            return indel.StringRepresentation;
        }
        public RealignmentResult RealignToTargets(Read read, HashableIndel[] indels,
            Dictionary<HashableIndel, GenomeSnippet> indelContexts, ReadToRealignDetails leftAnchoredDetails, ReadToRealignDetails rightAnchoredDetails, bool pairSpecific, int[] indexes,
            bool skipLeftAnchored = false, bool skipRightAnchored = false)
        {
            if (rightAnchoredDetails == null)
            {
                skipRightAnchored = true;
            }

            // when aligning with left anchor, if there's an insertion and a deletion at the same position
            // we need to process the insertion first.  this is an artifact of how we adjust positions after an insertion
            // luckily this is already how they are sorted in the default sort function
            var resultLeftAnchored = skipLeftAnchored ? null : RealignForAnchor(indels, indelContexts, read, true, leftAnchoredDetails, pairSpecific, indexes);
            if (IsUnbeatable(resultLeftAnchored))
            {
                return resultLeftAnchored;
            }

            // when aligning with right anchor, if there's an insertion and a deletion at the same position
            // we need to process the deletion first.  
            // this is because the position of indels are reported on the left side of the indel, and the deletion
            // could have adjusted the other side positions such that an insertion comes into view (which otherwise might not)
            var resultRightAnchored = skipRightAnchored ? null : RealignForAnchor(indels, indelContexts, read, false, rightAnchoredDetails, pairSpecific, indexes);

            var betterResult = _comparer.GetBetterResult(resultLeftAnchored, resultRightAnchored);
            if (betterResult != null)
            {
                betterResult.FailedForLeftAnchor = resultLeftAnchored == null;
                betterResult.FailedForRightAnchor = resultRightAnchored == null;
            }

            return betterResult;
        }


        private RealignmentResult RealignForAnchor(HashableIndel[] indels, Dictionary<HashableIndel, GenomeSnippet> indelContexts, 
            Read read, bool anchorOnLeft, ReadToRealignDetails details, bool pairSpecific, int[] indexes)
        {
            try
            {
                var freshCigarWithoutTerminalNs = new CigarAlignment(details.FreshCigarWithoutTerminalNs);
                var freshPositionMap = new PositionMap(details.PositionMapLength);

                for (int i = 0; i < details.PositionMapLength; i++)
                {
                    freshPositionMap.UpdatePositionAtIndex(i,
                        details.PositionMapWithoutTerminalNs.GetPositionAtIndex(i));
                }

                var result = new RealignmentResult();

                // layer on indels one by one, indels already sorted by ascending position

                if (LayerOnIndels(indels, indelContexts, anchorOnLeft, details.SequenceWithoutTerminalNs,
                    freshPositionMap, ref result, pairSpecific)) return null;

                var context = indelContexts[indels[0]];

                // Softclip partial insertions at read ends
                if (_maskPartialInsertion || _minimumUnanchoredInsertionLength > 0)
                {
                    MaskPartialInsertion(indels, read, context.Sequence, result, context.StartPosition);
                }

                _softclipReapplier.ReapplySoftclips(read, details.NPrefixLength, details.NSuffixLength, freshPositionMap, result, context,
                    details.PrefixSoftclip, details.SuffixSoftclip, freshCigarWithoutTerminalNs);

                result.AcceptedIndels = new List<int>();
                result.AcceptedHashableIndels = new List<HashableIndel>();
                for (int i = 0; i < result.AcceptedIndelsInSubList.Count; i++)
                {
                    // TODO do we need to be more nuanced about this and only do it in duplication areas?
                    var currentSubIndex = result.AcceptedIndelsInSubList[i];
                    result.AcceptedIndels.Add(indexes[currentSubIndex]);
                    var currentIndel = indels[currentSubIndex];
                    result.AcceptedHashableIndels.Add(currentIndel);
                    if (currentIndel.Type == AlleleCategory.Deletion)
                    {
                        var addedAt = result.IndelsAddedAt[i];
                        var anchorStart = addedAt + 1;
                        var lastOp = result.Cigar[result.Cigar.Count - 1];
                        var rightSoftclipLength = lastOp.Type == 'S' ? (int)lastOp.Length : 0;
                        var rightAnchorLength = read.Sequence.Length - anchorStart - rightSoftclipLength;
                        if (rightAnchorLength < currentIndel.Length && anchorStart < read.Sequence.Length)
                        {
                            if (read.Sequence.Substring(anchorStart, rightAnchorLength) ==
                                currentIndel.ReferenceAllele.Substring(1, rightAnchorLength))
                            {
                                return null;
                            }
                        }

                    }

                }

                if (result.SumOfMismatchingQualities == null)
                {
                    result.SumOfMismatchingQualities = Helper.GetSumOfMismatchQualities(read.Qualities, read.Sequence,
                        freshPositionMap, context.Sequence,
                        context.StartPosition);
                }


                result.Indels = string.Join("|", indels.Select(x => StringifyIndel(x)));

                return result;
            }
            catch (Exception e)
            {
                if (_debug)
                {
                    Logger.WriteExceptionToLog(new Exception($"Realign for anchor failed: read '{read.Name}' with indels {(string.Join("|", indels.Select(x => StringifyIndel(x))))}, anchoring on {(anchorOnLeft ? "left" : "right")}.", e));
                }
                return null;
            }
        }

       

        private bool LayerOnIndels(HashableIndel[] indels, Dictionary<HashableIndel, GenomeSnippet> indelContexts, bool anchorOnLeft,
            string sequenceWithoutTerminalNs, PositionMap positionMapWithoutTerminalNs, ref RealignmentResult result, bool pairSpecific)
        {
            var resultIndels = "";
            var resultIndelIndexes = new List<int>();
            var resultIndelsAddedAt = new List<int>();
            var resultNifiedAt = new List<int>();
            if (anchorOnLeft)
            {
                for (var i = 0; i < indels.Length; i++)
                {
                    var snippet = GetContext(indels[i], indelContexts);

                    result = AddIndelAndGetResult(sequenceWithoutTerminalNs, indels[i],
                        snippet.Sequence, true, positionMapWithoutTerminalNs,
                        snippet.StartPosition, pairSpecific);

                    if (result == null) return true;
                    resultIndels += result.Indels + "|";
                    resultIndelIndexes.Add(i);
                    resultIndelsAddedAt.AddRange(result.IndelsAddedAt);
                    resultNifiedAt.AddRange(result.NifiedAt);
                }
            }
            else
            {
                for (var i = indels.Length - 1; i >= 0; i--)
                {
                    var snippet = GetContext(indels[i], indelContexts);
                    result = AddIndelAndGetResult(sequenceWithoutTerminalNs, indels[i],
                        snippet.Sequence, false, positionMapWithoutTerminalNs,
                        snippet.StartPosition, pairSpecific);

                    if (result == null) return true;
                    resultIndels += result.Indels + "|";
                    resultIndelIndexes.Add(i);
                    resultIndelsAddedAt.AddRange(result.IndelsAddedAt);
                    resultNifiedAt.AddRange(result.NifiedAt);

                }
            }

            result.Indels = resultIndels; // TODO can we remove this? Think it gets overwritten later...
            result.AcceptedIndelsInSubList = resultIndelIndexes;
            result.NifiedAt = resultNifiedAt;
            result.IndelsAddedAt = resultIndelsAddedAt;
            return false;
        }

        private GenomeSnippet GetContext(HashableIndel indel, Dictionary<HashableIndel, GenomeSnippet> indelContexts)
        {
            return indelContexts[indel];
        }

        // Evaluate insertions at read ends to determine if they are partial or unanchored
        // minimumUnanchoredInsertionLength applies to the indel target that is being realigned against.
        public static bool EvaluateInsertionAtReadEnds(CigarOp cigar, HashableIndel indel, int minimumUnanchoredInsertionLength, bool maskPartialInsertion)
        {
            if (cigar.Type == 'I')
            {
                var isPartial = maskPartialInsertion && cigar.Length < indel.Length;
                var isUnanchored = indel.Length < minimumUnanchoredInsertionLength; // TODO is this really the right move? Why not count this against the observation rather than the expected?
                return isPartial || isUnanchored;
            }
            return false;
        }

        public void MaskPartialInsertion(HashableIndel[] indels, Read read, string refSequence, RealignmentResult result, int refSequenceStartIndex = 0)
        {
            // Softclip partial insertions at read ends
            // Assumption: there should be no softclips in the cigar by this time
            // Assumption: there should be exactly as many/the same indels in "indels" as are represented in the cigar in "result.Cigar".
            var firstIndel = indels[0];
            var lastIndel = indels[indels.Length - 1];
            bool hasInsertion = (firstIndel.Type == AlleleCategory.Insertion || lastIndel.Type == AlleleCategory.Insertion);
            if (hasInsertion)
            {
                if (_minimumUnanchoredInsertionLength > 0 || _maskPartialInsertion)
                {
                    var newCigar = new CigarAlignment { };
                    for (int i = 0; i < result.Cigar.Count; i++)
                    {
                        if (result.Cigar[i].Type == 'S')
                        {
                            throw new InvalidDataException(
                                string.Format(
                                    "Found an unexpected cigar type [{0}] in CIGAR string {1} before re-softclipping", result.Cigar[i].Type, result.Cigar));
                        }
                        else if (i == 0 && EvaluateInsertionAtReadEnds(result.Cigar[i], firstIndel, _minimumUnanchoredInsertionLength, _maskPartialInsertion))
                        {
                            newCigar.Add(new CigarOp('S', result.Cigar[i].Length));
                        }
                        else if (i == result.Cigar.Count - 1 && EvaluateInsertionAtReadEnds(result.Cigar[i], lastIndel, _minimumUnanchoredInsertionLength, _maskPartialInsertion))
                        {
                            newCigar.Add(new CigarOp('S', result.Cigar[i].Length));
                        }
                        else
                        {
                            newCigar.Add(result.Cigar[i]);
                        }
                    }

                    newCigar.Compress();
                    result.Cigar = newCigar;
                }

            }
           

            var newSummary = Extensions.GetAlignmentSummary(result.Position - 1 - refSequenceStartIndex, result.Cigar, refSequence,
                read.Sequence, _trackActualMismatches, _checkSoftclipsForMismatches);

            result.NumIndels = newSummary.NumIndels;
            result.NumNonNMismatches = newSummary.NumNonNMismatches;
            result.NumNonNSoftclips = newSummary.NumNonNSoftclips;
            result.NumSoftclips = newSummary.NumSoftclips;
            result.NumMismatchesIncludeSoftclip = newSummary.NumMismatchesIncludeSoftclip;
            result.NumIndelBases = newSummary.NumIndelBases;
            result.NumInsertedBases = newSummary.NumInsertedBases;
        }

        private bool IsUnbeatable(RealignmentResult bestResultSoFar)
        {
            return bestResultSoFar != null && bestResultSoFar.NumIndels == 1
                                           && bestResultSoFar.NumMismatches == 0 &&
                                           bestResultSoFar.NumMismatchesIncludeSoftclip == 0;
        }

        public RealignmentResult GetBestAlignment(List<HashableIndel> rankedIndels,
            Dictionary<HashableIndel, GenomeSnippet> indelContexts, Read read, out int attemptedTargetSides, bool fromPairSpecificIndels)
        {
            bool realign2 = true;
            RealignmentResult bestResultSoFar = null;

            attemptedTargetSides = 0;

            // Note this used to be in the loop... hopefully I'm not killing anything here...
            var nPrefixLength = read.GetNPrefix();

            if (_keepProbeSoftclips)
            {
                if ((_keepBothSideSoftclips || !read.BamAlignment.IsReverseStrand() || !read.BamAlignment.IsPaired()) && nPrefixLength == 0)
                {
                    nPrefixLength = (int)read.CigarData.GetPrefixClip();
                }
            }

            var details = new ReadToRealignDetails(read, read.GetAdjustedPosition(true, probePrefix: _keepProbeSoftclips ? 
                nPrefixLength : 0), _keepProbeSoftclips, _keepBothSideSoftclips);

            var positionFromRight =
                read.GetAdjustedPosition(false, probePrefix: _keepProbeSoftclips ? nPrefixLength : 0);
            ReadToRealignDetails rightAnchoredDetails = null;
            if (positionFromRight >= 0)
            {
                rightAnchoredDetails = new ReadToRealignDetails(read, positionFromRight, _keepProbeSoftclips, _keepBothSideSoftclips);
            }

            // align to all permutations of one indel, two indels, and three indels
            // try to skip alignment if we know it will fail 
            for (var i = 0; i < rankedIndels.Count; i++)
            {
                var indel1 = rankedIndels[i];
                var indexes = new int[]{i};

                // try aligning to one indel
                _oneIndelSimpleTargets[0] = indel1;
                var indel1Result = RealignToTargets(read, _oneIndelSimpleTargets, indelContexts, details, rightAnchoredDetails, pairSpecific: fromPairSpecificIndels, indexes: indexes);
                attemptedTargetSides += 2;

                // update best result so far for one indel
                bestResultSoFar = _comparer.GetBetterResult(bestResultSoFar, indel1Result);
                if (IsUnbeatable(bestResultSoFar))
                {
                    return bestResultSoFar;
                }
                //if (bestResultSoFar != null && bestResultSoFar.NumIndels == 1 && bestResultSoFar.NumMismatches == 0)
                //{
                //    return bestResultSoFar; // can't beat this
                //}

                if (realign2)
                {
                    var indexes2 = new int[2];
                    for (var j = i + 1; j < rankedIndels.Count; j++)
                    {
                        var indel2 = rankedIndels[j];
                        if (!CanCoexist(indel1, indel2, fromPairSpecificIndels)) continue;

                        _twoIndelSimpleTargets[0] = indel1;
                        _twoIndelSimpleTargets[1] = indel2;

                        indexes2[0] = i;
                        indexes2[1] = j;

                        Array.Sort(_twoIndelSimpleTargets, CompareSimple); // need to sort by position

                        // for optimization, don't try to align from a given side if we already failed aligning the indel on that side
                        var alreadyFailedFromLeft = indel1Result == null && _twoIndelSimpleTargets[0].Equals(indel1);
                        var alreadyFailedFromRight = indel1Result == null && _twoIndelSimpleTargets[1].Equals(indel1);
                        if (!alreadyFailedFromLeft) attemptedTargetSides++;
                        if (!alreadyFailedFromRight) attemptedTargetSides++;

                        var indel2Result = RealignToTargets(read, _twoIndelSimpleTargets, indelContexts, details, rightAnchoredDetails, pairSpecific: fromPairSpecificIndels, indexes: indexes2,
                            skipLeftAnchored: alreadyFailedFromLeft, skipRightAnchored: alreadyFailedFromRight);
                        bestResultSoFar = _comparer.GetBetterResult(bestResultSoFar, indel2Result);

                    }
                }
            }

            return bestResultSoFar;
        }


        public int CompareSimple(HashableIndel c1, HashableIndel c2)
        {
            var coordinateResult = c1.ReferencePosition.CompareTo(c2.ReferencePosition);
            if (coordinateResult == 0)
            {
                if (c1.Type == AlleleCategory.Insertion)  // return insertions first
                    return -1;
                return 1;
            }
            return coordinateResult;
        }

        public bool CanCoexist(HashableIndel indel1, HashableIndel indel2, bool pairSpecific = true)
        {
            // TODO do we really need to allow for a scenario where we let stuff coexist even though we've never seen it before? If so, need to revisit overlapping indel logic ie chr22:24037625 T>TCTGTTG,chr22:24037625 TCTG>T should not be allowed 
            {
                if (!indel1.InMulti || !indel2.InMulti)
                {
                    return false;
                }

                return indel1.OtherIndel == indel2.StringRepresentation;
            }

        }
    }
}