using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alignment.Domain.Sequencing;
using Common.IO.Utility;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using ReadRealignmentLogic.Interfaces;
using ReadRealignmentLogic.Models;
using ReadRealignmentLogic.Utlity;

namespace ReadRealignmentLogic
{
    public class ReadRealigner
    {
        private readonly CandidateIndel[] _oneIndelTargets = new CandidateIndel[1];
        private readonly CandidateIndel[] _twoIndelTargets = new CandidateIndel[2];
        private readonly CandidateIndel[] _threeIndelTargets = new CandidateIndel[3];
        private readonly List<CandidateIndel> _proximalTargets = new List<CandidateIndel>(200);
        private readonly bool _tryThree;
        private readonly bool _remaskSoftclips;
        private readonly bool _maskPartialInsertion;
        private readonly int _minimumUnanchoredInsertionLength;
        private readonly AlignmentComparer _comparer;
        public const float HighFrequencyIndelCutoff = 0.3f;

        public ReadRealigner(AlignmentComparer comparer, bool tryThree = false, bool remaskSoftclips = true, bool maskPartialInsertion = false, int minimumUnanchoredInsertionLength = 0)
        {
            _tryThree = tryThree;
            _remaskSoftclips = remaskSoftclips;
            _maskPartialInsertion = maskPartialInsertion;
            _minimumUnanchoredInsertionLength = minimumUnanchoredInsertionLength;
            _comparer = comparer;
        }

        private List<CandidateIndel> GetProximalTargets(Read read, List<CandidateIndel> allTargets, int maxIndelSize)
        {
            var readLeftPosition = read.GetAdjustedPosition(true);
            var readRightPosition = read.GetAdjustedPosition(false);

            var readStartBoundary = Math.Min(readLeftPosition, readRightPosition) - (maxIndelSize * 3);
            var readEndBoundary = Math.Max(readLeftPosition, readRightPosition) + read.ReadLength + (maxIndelSize * 3);

            _proximalTargets.Clear();
            foreach (var target in allTargets)
            {
                if (target.ReferencePosition > readEndBoundary) break;
                if (target.ReferencePosition >= readStartBoundary)
                    _proximalTargets.Add(target);
            }

            return _proximalTargets;
        }

        public RealignmentResult Realign(Read read, List<CandidateIndel> allTargets, string refSequence,
            IIndelRanker ranker, HashSet<Tuple<string, string, string>> indelCandidateGroups = null, int maxIndelSize = 50)
        {
            try
            {
                // get targets near read
                var readTargets = GetProximalTargets(read, allTargets, maxIndelSize);
                ranker.Rank(readTargets);

                var attempted = 0;
                var result = GetBestAlignment(readTargets, read, refSequence, indelCandidateGroups, out attempted);
#if false
                Console.WriteLine("{0}: Realigning {1} proximal targets, made {2} attempts.  Best alignment has {3} mismatches {4} indels.",
                    read.Position, readTargets.Count(), attempted, result == null ? -1 : result.NumMismatches, result == null ? -1 : result.NumIndels);
#endif
                return result;

            }
            catch (Exception ex)
            {
                Logger.WriteExceptionToLog(new Exception(string.Format("Error aligning read '{0}'", read.Name), ex));
                return null;
            }
        }

        private RealignmentResult AddIndelAndGetResult(string readSequence, CandidateIndel priorIndel,
            string refSequence, bool anchorLeft, PositionMap positionMap)
        {
            var foundIndel = false;
            var insertionPostionInReadStart = -1;
            var insertionPositionInReadEnd = -1;

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
                            // offset positions after deletion
                            for (var j = i + 1; j < positionMap.Length; j++)
                            {
                                if (positionMap.GetPositionAtIndex(j) != -1)  // preserve existing insertions
                                    positionMap.UpdatePositionAtIndex(j, positionMap.GetPositionAtIndex(j) + priorIndel.Length);
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
                                    positionMap.UpdatePositionAtIndex(j,-1, true);
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

                            // offset positions after deletion
                            for (var j = i - 1; j >= 0; j--)
                            {
                                if (positionMap.GetPositionAtIndex(j) != -1) // preserve existing insertions
                                    positionMap.UpdatePositionAtIndex(j, positionMap.GetPositionAtIndex(j) - priorIndel.Length);
                            }

                            break;
                        }
                    }
                }
            }

            if (!foundIndel || !Helper.IsValidMap(positionMap.Map, refSequence))
                return null;

            // verify insertion matches
            if (priorIndel.Type == AlleleCategory.Insertion)
            {
                if (insertionPostionInReadStart == -1 || insertionPositionInReadEnd == -1)
                    return null; // weird, this shouldnt ever happen

                var readInsertedSequence = readSequence.Substring(insertionPostionInReadStart,
                    insertionPositionInReadEnd - insertionPostionInReadStart + 1);

                var indelSequence = priorIndel.AlternateAllele.Substring(1);

                var clippedPriorSequence = anchorLeft
                    ? indelSequence.Substring(0, readInsertedSequence.Length)
                    : indelSequence.Substring(indelSequence.Length - readInsertedSequence.Length);

                var mismatches = Helper.GetNumMismatches(readInsertedSequence, clippedPriorSequence);
                if (mismatches == null || mismatches > 0)
                {
                    return null; // inserted sequence doesn't match read
                }
            }

            var newCigar = Helper.ConstructCigar(positionMap.Map);

            var newSummary = Extensions.GetAlignmentSummary(positionMap.FirstMappableBase() - 1, newCigar, refSequence,
                readSequence);

            if (newSummary == null)
            return null;

            var readHasPosition = positionMap.HasAnyMappableBases();
            if (!readHasPosition)
            {
                throw new InvalidDataException(string.Format("Trying to generate result and read does not have any alignable bases. ({0}, {1})", newCigar, string.Join(",", positionMap)));
            }
            return new RealignmentResult()
            {
                Cigar = newCigar,
                NumIndels = newCigar.NumIndels(),
                Position = positionMap.FirstMappableBase(),
                NumMismatches = newSummary.NumMismatches,
                NumNonNMismatches = newSummary.NumNonNMismatches,
                NumMismatchesIncludeSoftclip = newSummary.NumMismatchesIncludeSoftclip,
                NumSoftclips = newSummary.NumSoftclips,
                NumNonNSoftclips = newSummary.NumNonNSoftclips,
                NumIndelBases = newSummary.NumIndelBases,
                FirstMismatchPosition = newSummary.FirstMismatchPosition,
                LastMismatchPosition = newSummary.LastMismatchPosition

            };
        }

        public RealignmentResult RealignToTargets(Read read, CandidateIndel[] targets, string refSequence,
            bool skipLeftAnchored = false, bool skipRightAnchored = false)
        {
            // when aligning with left anchor, if there's an insertion and a deletion at the same position
            // we need to process the insertion first.  this is an artifact of how we adjust positions after an insertion
            // luckily this is already how they are sorted in the default sort function
            var resultLeftAnchored = skipLeftAnchored ? null : RealignForAnchor(targets, read, refSequence, true);

            // when aligning with right anchor, if there's an insertion and a deletion at the same position
            // we need to process the deletion first.  
            // this is because the position of indels are reported on the left side of the indel, and the deletion
            // could have adjusted the other side positions such that an insertion comes into view (which otherwise might not)
            var resultRightAnchored = skipRightAnchored ? null : RealignForAnchor(targets, read, refSequence, false);

            var betterResult = _comparer.GetBetterResult(resultLeftAnchored, resultRightAnchored);
            if (betterResult != null)
            {
                betterResult.FailedForLeftAnchor = resultLeftAnchored == null;
                betterResult.FailedForRightAnchor = resultRightAnchored == null;
            }

            return betterResult;
        }

        private RealignmentResult RealignForAnchor(CandidateIndel[] indels, Read read, string refSequence, bool anchorOnLeft)
        {
            var position = read.GetAdjustedPosition(anchorOnLeft);
            var freshCigarWithoutTerminalNs = new CigarAlignment();

            var nPrefixLength = read.GetNPrefix();
            var nSuffixLength = read.GetNSuffix();

            // Only build up the cigar for the non-N middle. Add the N prefix back on after the realignment attempts.
            freshCigarWithoutTerminalNs.Add(new CigarOp('M', (uint)(read.Sequence.Length - nPrefixLength - nSuffixLength)));
            freshCigarWithoutTerminalNs.Compress();

            // start with fresh position map
            //var positionMapWithoutTerminalNsArray = new int[read.ReadLength - nPrefixLength - nSuffixLength];
            var positionMapWithoutTerminalNs = new PositionMap(read.ReadLength - nPrefixLength - nSuffixLength);
            Read.UpdatePositionMap(position, freshCigarWithoutTerminalNs, positionMapWithoutTerminalNs);
            var prefixSoftclip = read.CigarData.GetPrefixClip();
            var suffixSoftclip = read.CigarData.GetSuffixClip();    

            RealignmentResult result = null;
            var sequenceWithoutTerminalNs = read.Sequence.Substring(nPrefixLength, read.Sequence.Length - nPrefixLength - nSuffixLength);

            // layer on indels one by one, indels already sorted by ascending position
            if (anchorOnLeft)
            {
                for (var i = 0; i < indels.Length; i++)
                {
                    result = AddIndelAndGetResult(sequenceWithoutTerminalNs, indels[i], refSequence, true, positionMapWithoutTerminalNs);

                    if (result == null) return null;
                }
            }
            else
            {
                for (var i = indels.Length - 1; i >= 0; i--)
                {
                    result = AddIndelAndGetResult(sequenceWithoutTerminalNs, indels[i], refSequence, false, positionMapWithoutTerminalNs);

                    if (result == null) return null;
                }
            }


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
                        else if (i == 0 && Helper.EvaluateInsertionAtReadEnds(result.Cigar[i], firstIndel, _minimumUnanchoredInsertionLength, _maskPartialInsertion))
                        {
                            newCigar.Add(new CigarOp('S', result.Cigar[i].Length));
                        }
                        else if (i == result.Cigar.Count - 1 && Helper.EvaluateInsertionAtReadEnds(result.Cigar[i], lastIndel, _minimumUnanchoredInsertionLength, _maskPartialInsertion))
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
            

            // Re-append the N-prefix
            var nPrefixPositionMap = Enumerable.Repeat(-1, nPrefixLength);
            var nSuffixPositionMap = Enumerable.Repeat(-1, nSuffixLength);
            var finalPositionMap = new PositionMap(nPrefixPositionMap.Concat(positionMapWithoutTerminalNs.Map).Concat(nSuffixPositionMap).ToArray());

            var finalCigar = new CigarAlignment {new CigarOp('S', (uint) nPrefixLength)};
            foreach (CigarOp op in result.Cigar)
            {
                finalCigar.Add(op);
            }
            finalCigar.Add(new CigarOp('S', (uint)nSuffixLength));
            finalCigar.Compress();
            result.Cigar = finalCigar;

            var UpdatedSummary = Extensions.GetAlignmentSummary(result.Position - 1, result.Cigar, refSequence, read.Sequence);

            result.NumIndels = UpdatedSummary.NumIndels;
            result.NumNonNMismatches = UpdatedSummary.NumNonNMismatches;
            result.NumMismatchesIncludeSoftclip = UpdatedSummary.NumMismatchesIncludeSoftclip;
            result.NumNonNSoftclips = UpdatedSummary.NumNonNSoftclips;
            result.NumSoftclips = UpdatedSummary.NumSoftclips;
            result.NumIndelBases = UpdatedSummary.NumIndelBases;
            result.MismatchesIncludeSoftclip = UpdatedSummary.MismatchesIncludeSoftclip;
            result.HasHighFrequencyIndel = indels.Any(t => t.Frequency > HighFrequencyIndelCutoff);


            // In case realignment introduced a bunch of mismatch-Ms where there was previously softclipping, optionally re-mask them.
            if (result!=null && _remaskSoftclips)
            {
                var mismatchMap = Helper.GetMismatchMap(read.Sequence, finalPositionMap.Map, refSequence);

                var softclipAdjustedCigar = Helper.SoftclipCigar(result.Cigar, mismatchMap, prefixSoftclip, suffixSoftclip, maskNsOnly: true, prefixNs: Helper.GetCharacterBookendLength(read.Sequence, 'N', false), suffixNs: Helper.GetCharacterBookendLength(read.Sequence, 'N', true));

                // Update position map to account for any softclipping added
                var adjustedPrefixClip = softclipAdjustedCigar.GetPrefixClip();
                for (var i = 0; i < adjustedPrefixClip; i++)
                {
                    finalPositionMap.UpdatePositionAtIndex(i,-2, true);
                }
                var adjustedSuffixClip = softclipAdjustedCigar.GetSuffixClip();
                for (var i = 0; i < adjustedSuffixClip; i++)
                {
                    finalPositionMap.UpdatePositionAtIndex(finalPositionMap.Length - 1 - i, -2, true);
                }

                var editDistance = Helper.GetEditDistance(read.Sequence, finalPositionMap.Map, refSequence);
                if (editDistance == null)
                {
                    // This shouldn't happen at this point - we already have a successful result
                    throw new InvalidDataException("Edit distance is null for :" + read.Name + " with position map " +
                                        string.Join(",", finalPositionMap) + " and CIGAR " + softclipAdjustedCigar);
                }

                var readHasPosition = finalPositionMap.HasAnyMappableBases();
                if (!readHasPosition)
                {
                    throw new InvalidDataException(string.Format("Read does not have any alignable bases. ({2} --> {0} --> {3}, {1})", freshCigarWithoutTerminalNs, string.Join(",", finalPositionMap), read.CigarData, softclipAdjustedCigar));
                }

                result.Position = finalPositionMap.FirstMappableBase();
                result.Cigar = softclipAdjustedCigar;
                result.NumMismatches = editDistance.Value;


                var newSummary = Extensions.GetAlignmentSummary(result.Position - 1, result.Cigar, refSequence,
                    read.Sequence);

                result.NumNonNMismatches = newSummary.NumNonNMismatches;
                result.NumMismatchesIncludeSoftclip = newSummary.NumMismatchesIncludeSoftclip;
                result.NumNonNSoftclips = newSummary.NumNonNSoftclips;
                result.NumSoftclips = newSummary.NumSoftclips;
                result.NumIndelBases = newSummary.NumIndelBases;
                result.MismatchesIncludeSoftclip = newSummary.MismatchesIncludeSoftclip;
                result.HasHighFrequencyIndel = indels.Any(t => t.Frequency > HighFrequencyIndelCutoff);                
                result.NumIndelBases = UpdatedSummary.NumIndelBases;
            }

            return result;
        }

        private RealignmentResult GetBestAlignment(List<CandidateIndel> rankedIndels, Read read, string refSequence, HashSet<Tuple<string, string, string>> indelCandidateGroups, out int attemptedTargetSides)
        {
            RealignmentResult bestResultSoFar = null;

            attemptedTargetSides = 0;

            // align to all permutations of one indel, two indels, and three indels
            // try to skip alignment if we know it will fail 
            for (var i = 0; i < rankedIndels.Count; i++)
            {
                var indel1 = rankedIndels[i];

                // try aligning to one indel
                _oneIndelTargets[0] = rankedIndels[i];
                var indel1Result = RealignToTargets(read, _oneIndelTargets, refSequence);
                attemptedTargetSides += 2;

                // update best result so far for one indel
                bestResultSoFar = _comparer.GetBetterResult(bestResultSoFar, indel1Result);
                if (bestResultSoFar != null && bestResultSoFar.NumIndels == 1 && bestResultSoFar.NumMismatches == 0)
                {
                    return bestResultSoFar; // can't beat this
                }

                // Do not realign to >1 indels if we haven't seen any coexisting indels.
                if (indelCandidateGroups == null) continue;
                if (indelCandidateGroups.Count == 0) continue;

                for (var j = i + 1; j < rankedIndels.Count; j++)
                {
                    var indel2 = rankedIndels[j];
                    var indelPair = new List<CandidateIndel> { indel1, indel2 }.OrderBy(g => g.ReferencePosition).ThenBy(t => t.ReferenceAllele).Select(x => x.ToString()).ToList();
                    if ( indelCandidateGroups.Contains(new Tuple<string, string, string>(indelPair[0], indelPair[1], null)))
                    {
                        if (!CanCoexist(indel1, indel2)) continue;

                        _twoIndelTargets[0] = indel1;
                        _twoIndelTargets[1] = indel2;
                        Array.Sort(_twoIndelTargets, Compare);  // need to sort by position

                        // for optimization, don't try to align from a given side if we already failed aligning the indel on that side
                        var alreadyFailedFromLeft = indel1Result == null && _twoIndelTargets[0] == indel1;
                        var alreadyFailedFromRight = indel1Result == null && _twoIndelTargets[1] == indel1;
                        if (!alreadyFailedFromLeft) attemptedTargetSides++;
                        if (!alreadyFailedFromRight) attemptedTargetSides++;

                        var indel2Result = RealignToTargets(read, _twoIndelTargets, refSequence, alreadyFailedFromLeft, alreadyFailedFromRight);
                        bestResultSoFar = _comparer.GetBetterResult(bestResultSoFar, indel2Result);
                    }                    

                    if (_tryThree)
                    {
                        for (var k = j + 1; k < rankedIndels.Count; k++)
                        {
                            var indel3 = rankedIndels[k];
                            var indelList = new List<CandidateIndel> { indel1, indel2, indel3 }.OrderBy(g => g.ReferencePosition).ThenBy(t => t.ReferenceAllele).Select(x => x.ToString()).ToList();
                            bool groupCoexist = indelCandidateGroups.Contains(new Tuple<string, string, string>(indelList[0], indelList[1], indelList[2]));                            
                            if (!groupCoexist) continue;
                            if (!(CanCoexist(indel1, indel3) && CanCoexist(indel2, indel3))) continue;

                            // only try to realign to three indels if bestResultSoFar is not good enough
                            if (NeedBetter(bestResultSoFar))
                            {
                                _threeIndelTargets[0] = indel1;
                                _threeIndelTargets[1] = indel2;
                                _threeIndelTargets[2] = indel3;
                                Array.Sort(_threeIndelTargets, Compare); // need to sort by position
                               
                                var indel3Result = RealignToTargets(read, _threeIndelTargets, refSequence);
                                bestResultSoFar = _comparer.GetBetterResult(bestResultSoFar, indel3Result);
                            }
                        }
                    }
                }
            }

            return bestResultSoFar;
        }

        private bool NeedBetter(RealignmentResult bestResultSoFar)
        {
            return bestResultSoFar == null || bestResultSoFar.NumMismatches > 0;
        }

        public int Compare(CandidateIndel c1, CandidateIndel c2)
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

        public int CompareRightAnchor(CandidateIndel c1, CandidateIndel c2)
        {
            var coordinateResult = c1.ReferencePosition.CompareTo(c2.ReferencePosition);
            if (coordinateResult == 0)
            {
                if (c1.Type == AlleleCategory.Insertion)  // return insertions first
                    return 1;
                return -1;
            }
            return coordinateResult;
        }

        public bool CanCoexist(CandidateIndel indel1, CandidateIndel indel2)
        {
            if (indel1.ReferencePosition - indel2.ReferencePosition == 0 && indel1.Type == indel2.Type) return false;

            // Assumption is that we are dealing with simple insertions & deletions. i.e. either ref or alt will have single base, and the other will have that single base + the varying bases.
            var indel1Bases = indel1.Type == AlleleCategory.Insertion ? indel1.AlternateAllele : indel1.ReferenceAllele;
            var indel2Bases = indel2.Type == AlleleCategory.Insertion ? indel2.AlternateAllele : indel2.ReferenceAllele;
            if (indel1.ReferencePosition - indel2.ReferencePosition == 0 && indel1Bases == indel2Bases) return false;

            // Note that the "End" only makes sense here if we are talking about deletions. Appropriately, it is not used in any of the insertion cases below.
            var indel1Start = indel1.ReferencePosition + 1;
            var indel1End = indel1.ReferencePosition + indel1.Length;
            var indel2Start = indel2.ReferencePosition + 1;
            var indel2End = indel2.ReferencePosition + indel2.Length;

            if (indel1.Type == AlleleCategory.Deletion)
            {
                if (indel2.Type == AlleleCategory.Deletion)
                {
                    // no overlapping deletions
                    if ((indel1Start >= indel2Start && indel1Start <= indel2End) ||
                        (indel2Start >= indel1Start && indel2Start <= indel1End))
                        return false;
                }
                else
                {
                    // insertion cannot start within deletion 
                    if (indel2Start > indel1Start && indel2Start <= indel1End)
                        return false;
                }
            }
            else if (indel2.Type == AlleleCategory.Deletion)
            {
                // insertion cannot start within deletion 
                if (indel1Start > indel2Start && indel1Start <= indel2End)
                    return false;
            }

            return true;
        }
    }
}