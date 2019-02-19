using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alignment.Domain.Sequencing;
using Common.IO.Utility;
using Gemini.Interfaces;
using Gemini.Models;
using Gemini.Types;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;
using ReadRealignmentLogic.Utlity;
using Helper = Gemini.Utility.Helper;

namespace Gemini.FromHygea
{
    public class GeminiReadRealigner : IReadRealigner
    {
        private readonly bool _debug;
        private readonly HashableIndel[] _oneIndelSimpleTargets = new HashableIndel[1];
        private readonly HashableIndel[] _twoIndelSimpleTargets = new HashableIndel[2];

        private readonly bool _remaskSoftclips;
        private readonly bool _maskPartialInsertion;
        private readonly bool _keepProbeSoftclips;
        private readonly bool _keepBothSideSoftclips;
        private readonly bool _trackActualMismatches;
        private readonly bool _checkSoftclipsForMismatches;
        private readonly AlignmentComparer _comparer;
        private readonly bool _maskNsOnly = false;
        private const int VeryMessyThreshold = 20;

        public GeminiReadRealigner(AlignmentComparer comparer, bool remaskSoftclips = true, bool maskPartialInsertion = true, bool keepProbeSoftclips = false, bool keepBothSideSoftclips = false, bool trackActualMismatches = false, bool checkSoftclipsForMismatches = false, bool debug = false)
        {
            _remaskSoftclips = remaskSoftclips;
            _maskPartialInsertion = maskPartialInsertion;
            _keepProbeSoftclips = keepProbeSoftclips;
            _keepBothSideSoftclips = keepBothSideSoftclips;
            _trackActualMismatches = trackActualMismatches;
            _checkSoftclipsForMismatches = checkSoftclipsForMismatches;
            _comparer = comparer;
            if (_keepProbeSoftclips || _keepBothSideSoftclips)
            {
                _checkSoftclipsForMismatches = false;
                _maskNsOnly = false;
            }

            _debug = debug;
        }

        public RealignmentResult Realign(Read read, List<HashableIndel> allTargets, Dictionary<HashableIndel, GenomeSnippet> indelContexts,
            IIndelRanker ranker, bool pairSpecific, int maxIndelSize = 50)
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
                }

                return result;

            }
            catch (Exception ex)
            {
                Logger.WriteExceptionToLog(new Exception(string.Format("Error aligning read '{0}'", read.Name), ex));
                return null;
            }
        }

        private RealignmentResult AddIndelAndGetResult(string readSequence, HashableIndel priorIndel,
            string refSequence, bool anchorLeft, int[] positionMap, int refSequenceStartIndex)
        {
            var foundIndel = false;
            var insertionPostionInReadStart = -1;
            var insertionPositionInReadEnd = -1;

            if (anchorLeft)
            {
                // move along position map to see if we can insert indel
                for (var i = 0; i < positionMap.Length; i++)
                {
                    if (positionMap[i] == priorIndel.ReferencePosition && i != positionMap.Length - 1)  // make sure we dont end right before indel
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
                                    positionMap[j] = -1;
                                    if (j - i == priorIndel.Length || j == positionMap.Length - 1)
                                        insertionPositionInReadEnd = j;
                                }
                                else
                                {
                                    if (positionMap[j] != -1) // preserve existing insertions
                                        positionMap[j] = positionMap[j] - priorIndel.Length;
                                }
                            }
                            break;
                        }

                        if (priorIndel.Type == AlleleCategory.Deletion)
                        {
                            // offset positions after deletion
                            for (var j = i + 1; j < positionMap.Length; j++)
                            {
                                if (positionMap[j] != -1)  // preserve existing insertions
                                    positionMap[j] += priorIndel.Length;
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
                        if (positionMap[i] == priorIndel.ReferencePosition + 1 && i != 0)
                        {
                            foundIndel = true;
                            insertionPositionInReadEnd = i - 1;
                        }
                        else if (positionMap[i] == priorIndel.ReferencePosition && i != positionMap.Length - 1)
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
                                    positionMap[j] = -1;
                                    if (insertionPositionInReadEnd - j + 1 == priorIndel.Length || j == 0)
                                        insertionPostionInReadStart = j;
                                }
                                else
                                {
                                    if (positionMap[j] != -1) // Don't update position map for things that were already -1
                                        positionMap[j] = positionMap[j] + priorIndel.Length;
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
                        if (positionMap[i] == priorIndel.ReferencePosition + priorIndel.Length + 1) //deletions must be fully anchored to be observed
                        {
                            foundIndel = true;

                            // offset positions after deletion
                            for (var j = i - 1; j >= 0; j--)
                            {
                                if (positionMap[j] != -1) // preserve existing insertions
                                    positionMap[j] -= priorIndel.Length;
                            }

                            break;
                        }
                    }
                }
            }

            //if (!foundIndel || !Helper.IsValidMap(positionMap, refSequence))
            //TODO changed this just for tailor
            if (!foundIndel || !Helper.IsValidMap(positionMap))
                return null;

            //verify insertion matches
            var newReadSequence = readSequence;
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
                var maxAllowedMismatches = 0;
                if (priorIndel.Length >= 5 && mismatches > 0)
                {
                    maxAllowedMismatches = priorIndel.Length/5;

                    var newSequence = Helper.NifyMismatches(clippedPriorSequence, readInsertedSequence);
                    newReadSequence = readSequence.Substring(0, insertionPostionInReadStart) + newSequence.ToLower() +
                                          readSequence.Substring(insertionPositionInReadEnd + 1 );

                }
                if (mismatches == null || (mismatches > maxAllowedMismatches ))
                {
                    return null; // inserted sequence doesn't match read
                }
            }

            var newCigar = Helper.ConstructCigar(positionMap);

            // TODO moved this, and probably should in original Hygea too?
            // Also, can cut down the calls to positionmap.First() in the original
            var readHasPosition = positionMap.Any(p => p >= 0);
            if (!readHasPosition)
            {
                throw new InvalidDataException(string.Format("Trying to generate result and read does not have any alignable bases. ({0}, {1})", newCigar, string.Join(",", positionMap)));
            }

            var startIndexInReference = positionMap.First(p => p >= 0) - 1;
            var startIndexInRefSequenceSnippet = startIndexInReference - refSequenceStartIndex;

            var newSummary = Extensions.GetAlignmentSummary(startIndexInRefSequenceSnippet, newCigar, refSequence,
                newReadSequence, false, false);

            if (newSummary == null)
                return null;

            return new RealignmentResult()
            {
                Cigar = newCigar,
                NumIndels = newCigar.NumIndels(),
                Position = startIndexInReference,
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
                Indels = StringifyIndel(priorIndel)
            };
        }

        private string StringifyIndel(HashableIndel indel)
        {
            if (!_debug)
            {
                return "";
            }
            else
            {
                return indel.Chromosome + ":" + indel.ReferencePosition + " " + indel.ReferenceAllele + ">" +
                       indel.AlternateAllele;
            }
        }
        public RealignmentResult RealignToTargets(Read read, HashableIndel[] indels,
            Dictionary<HashableIndel, GenomeSnippet> indelContexts, ReadToRealignDetails leftAnchoredDetails, ReadToRealignDetails rightAnchoredDetails,
            bool skipLeftAnchored = false, bool skipRightAnchored = false)
        {
            // when aligning with left anchor, if there's an insertion and a deletion at the same position
            // we need to process the insertion first.  this is an artifact of how we adjust positions after an insertion
            // luckily this is already how they are sorted in the default sort function
            var resultLeftAnchored = skipLeftAnchored ? null : RealignForAnchor(indels, indelContexts, read, true, leftAnchoredDetails);
            if (IsUnbeatable(resultLeftAnchored))
            {
                return resultLeftAnchored;
            }

            // when aligning with right anchor, if there's an insertion and a deletion at the same position
            // we need to process the deletion first.  
            // this is because the position of indels are reported on the left side of the indel, and the deletion
            // could have adjusted the other side positions such that an insertion comes into view (which otherwise might not)
            var resultRightAnchored = skipRightAnchored ? null : RealignForAnchor(indels, indelContexts, read, false, rightAnchoredDetails);

            var betterResult = _comparer.GetBetterResult(resultLeftAnchored, resultRightAnchored);
            if (betterResult != null)
            {
                betterResult.FailedForLeftAnchor = resultLeftAnchored == null;
                betterResult.FailedForRightAnchor = resultRightAnchored == null;
            }

            return betterResult;
        }


        private RealignmentResult RealignForAnchor(HashableIndel[] indels, Dictionary<HashableIndel, GenomeSnippet> indelContexts, 
            Read read, bool anchorOnLeft, ReadToRealignDetails details)
        {
            var freshCigarWithoutTerminalNs = new CigarAlignment(details.FreshCigarWithoutTerminalNs);
            var freshPositionMap = new int[details.PositionMapLength];

            for (int i = 0; i < details.PositionMapLength; i++)
            {
                freshPositionMap[i] = details.PositionMapWithoutTerminalNs[i];
            }

            var result = new RealignmentResult();

            //if (read.Name == "NB551015:245:HNWHKBGX3:3:11403:12948:12457")
            //{
            //    Console.WriteLine("....");

            //    foreach (var genomeSnippet in indelContexts)
            //    {
            //        Console.WriteLine(genomeSnippet.Value.StartPosition + "..." + genomeSnippet.Value.Sequence.Length);   
            //    }
            //}
            // layer on indels one by one, indels already sorted by ascending position
            if (LayerOnIndels(indels, indelContexts, anchorOnLeft, details.SequenceWithoutTerminalNs, 
                freshPositionMap, ref result)) return null;

            var context = indelContexts[indels[0]];

            ReapplySoftclips(read, details.NPrefixLength, details.NSuffixLength, freshPositionMap, result, context, details.PrefixSoftclip, details.SuffixSoftclip, freshCigarWithoutTerminalNs);

            if (result.SumOfMismatchingQualities == null)
            {
               result.SumOfMismatchingQualities = Helper.GetSumOfMismatchQualities(read.Qualities, read.Sequence, freshPositionMap, context.Sequence,
                    context.StartPosition);
            }

            // Softclip partial insertions at read ends
            if (_maskPartialInsertion)
            {
                MaskPartialInsertion(indels, read, context.Sequence, result, context.StartPosition);
            }

            result.Indels = string.Join("|",indels.Select(x => StringifyIndel(x)));

            return result;
        }

        private void ReapplySoftclips(Read read, int nPrefixLength, int nSuffixLength, int[] positionMapWithoutTerminalNs,
            RealignmentResult result, GenomeSnippet context, uint prefixSoftclip, uint suffixSoftclip,
            CigarAlignment freshCigarWithoutTerminalNs)
        {
            // Re-append the N-prefix
            var nPrefixPositionMap = Enumerable.Repeat(-1, nPrefixLength);
            var nSuffixPositionMap = Enumerable.Repeat(-1, nSuffixLength);
            var finalPositionMap = nPrefixPositionMap.Concat(positionMapWithoutTerminalNs).Concat(nSuffixPositionMap).ToArray();

            var finalCigar = new CigarAlignment {new CigarOp('S', (uint) nPrefixLength)};
            foreach (CigarOp op in result.Cigar)
            {
                finalCigar.Add(op);
            }

            finalCigar.Add(new CigarOp('S', (uint) nSuffixLength));
            finalCigar.Compress();
            result.Cigar = finalCigar;


            // In case realignment introduced a bunch of mismatch-Ms where there was previously softclipping, optionally re-mask them.
            if (result != null && _remaskSoftclips)
            {
                var mismatchMap =
                    Helper.GetMismatchMap(read.Sequence, finalPositionMap, context.Sequence, context.StartPosition);

                var softclipAdjustedCigar = Helper.SoftclipCigar(result.Cigar, mismatchMap, prefixSoftclip, suffixSoftclip,
                    maskNsOnly: _maskNsOnly, prefixNs: Helper.GetCharacterBookendLength(read.Sequence, 'N', false),
                    suffixNs: Helper.GetCharacterBookendLength(read.Sequence, 'N', true), softclipEvenIfMatch: _keepProbeSoftclips || _keepBothSideSoftclips);

                // Update position map to account for any softclipping added
                var adjustedPrefixClip = softclipAdjustedCigar.GetPrefixClip();
                for (var i = 0; i < adjustedPrefixClip; i++)
                {
                    finalPositionMap[i] = -2;
                }

                var adjustedSuffixClip = softclipAdjustedCigar.GetSuffixClip();
                for (var i = 0; i < adjustedSuffixClip; i++)
                {
                    finalPositionMap[finalPositionMap.Length - 1 - i] = -2;
                }

                var editDistance =
                    Helper.GetEditDistance(read.Sequence, finalPositionMap, context.Sequence, context.StartPosition);
                if (editDistance == null)
                {
                    // This shouldn't happen at this point - we already have a successful result
                    throw new InvalidDataException("Edit distance is null for :" + read.Name + " with position map " +
                                                   string.Join(",", finalPositionMap) + " and CIGAR " + softclipAdjustedCigar);
                }

                var sumOfMismatching = Helper.GetSumOfMismatchQualities(mismatchMap, read.Qualities);

                var readHasPosition = finalPositionMap.Any(p => p >= 0);
                if (!readHasPosition)
                {
                    throw new InvalidDataException(string.Format(
                        "Read does not have any alignable bases. ({2} --> {0} --> {3}, {1})", freshCigarWithoutTerminalNs,
                        string.Join(",", finalPositionMap), read.CigarData, softclipAdjustedCigar));
                }

                result.Position = finalPositionMap.First(p => p >= 0);
                result.Cigar = softclipAdjustedCigar;
                result.NumMismatches = editDistance.Value;


                var newSummary = Extensions.GetAlignmentSummary(result.Position - 1 - context.StartPosition, result.Cigar,
                    context.Sequence,
                    read.Sequence, false, false);

                result.NumNonNMismatches = newSummary.NumNonNMismatches;
                result.NumNonNSoftclips = newSummary.NumNonNSoftclips;
                result.NumSoftclips = newSummary.NumSoftclips;
                result.NumInsertedBases = newSummary.NumInsertedBases;
                result.NumMismatchesIncludeSoftclip = newSummary.NumMismatchesIncludeSoftclip;
                //result.MismatchesIncludeSoftclip = newSummary.MismatchesIncludeSoftclip;
                result.SumOfMismatchingQualities = sumOfMismatching;
            }
        }

        private bool LayerOnIndels(HashableIndel[] indels, Dictionary<HashableIndel, GenomeSnippet> indelContexts, bool anchorOnLeft,
            string sequenceWithoutTerminalNs, int[] positionMapWithoutTerminalNs, ref RealignmentResult result)
        {
            var resultIndels = "";

            if (anchorOnLeft)
            {
                for (var i = 0; i < indels.Length; i++)
                {
                    var snippet = GetContext(indels[i], indelContexts);

                    result = AddIndelAndGetResult(sequenceWithoutTerminalNs, indels[i],
                        snippet.Sequence, true, positionMapWithoutTerminalNs,
                        snippet.StartPosition);

                    if (result == null) return true;
                    resultIndels += result.Indels + "|";
                }
            }
            else
            {
                for (var i = indels.Length - 1; i >= 0; i--)
                {
                    var snippet = GetContext(indels[i], indelContexts);
                    result = AddIndelAndGetResult(sequenceWithoutTerminalNs, indels[i],
                        snippet.Sequence, false, positionMapWithoutTerminalNs,
                        snippet.StartPosition);

                    if (result == null) return true;
                    resultIndels += result.Indels + "|";
                }
            }

            result.Indels = resultIndels;
            return false;
        }

        private GenomeSnippet GetContext(HashableIndel indel, Dictionary<HashableIndel, GenomeSnippet> indelContexts)
        {
            return indelContexts[indel];
        }

        public class ReadToRealignDetails
        {
            public readonly int Position;   
            public readonly int NPrefixLength;
            public readonly int NSuffixLength;
            public readonly IReadOnlyList<int> PositionMapWithoutTerminalNs;
            public readonly uint PrefixSoftclip;
            public readonly uint SuffixSoftclip;
            public readonly string SequenceWithoutTerminalNs;
            public readonly string FreshCigarWithoutTerminalNs;
            public readonly int PositionMapLength;

            public ReadToRealignDetails(Read read, int position, bool keepProbeSoftclips = false, bool keepBothSideSoftclips = false)
            {
                var freshCigarWithoutTerminalNsRaw = new CigarAlignment();
                
                NPrefixLength = read.GetNPrefix();

                NSuffixLength = read.GetNSuffix();

                if (keepProbeSoftclips)
                {
                    if (keepBothSideSoftclips || (!read.BamAlignment.IsReverseStrand() || !read.BamAlignment.IsPaired()) && NPrefixLength == 0)
                    {
                        NPrefixLength = (int)read.CigarData.GetPrefixClip();
                    }
                    if (keepBothSideSoftclips || (read.BamAlignment.IsReverseStrand() || !read.BamAlignment.IsPaired()) && NSuffixLength == 0)
                    {
                        NSuffixLength = (int)read.CigarData.GetSuffixClip();
                    }
                }

                // Only build up the cigar for the non-N middle. Add the N prefix back on after the realignment attempts.
                freshCigarWithoutTerminalNsRaw.Add(new CigarOp('M', (uint)(read.Sequence.Length - NPrefixLength - NSuffixLength)));
                freshCigarWithoutTerminalNsRaw.Compress();

                // start with fresh position map
                var positionMapWithoutTerminalNs = new int[read.ReadLength - NPrefixLength - NSuffixLength];
                Read.UpdatePositionMap(position, read.Name, freshCigarWithoutTerminalNsRaw, positionMapWithoutTerminalNs);
                PrefixSoftclip = read.CigarData.GetPrefixClip();
                SuffixSoftclip = read.CigarData.GetSuffixClip();

                SequenceWithoutTerminalNs =
                    read.Sequence.Substring(NPrefixLength, read.Sequence.Length - NPrefixLength - NSuffixLength);

                PositionMapWithoutTerminalNs = positionMapWithoutTerminalNs;
                PositionMapLength = positionMapWithoutTerminalNs.Length;
                FreshCigarWithoutTerminalNs = freshCigarWithoutTerminalNsRaw.ToString();
                Position = position;
            }
        }
        
        public static void MaskPartialInsertion(HashableIndel[] indels, Read read, string refSequence, RealignmentResult result, int refSequenceStartIndex = 0)
        {
            if (!((result.Cigar[0].Type == 'I' || (result.Cigar[0].Type == 'S' && result.Cigar.Count > 1 && result.Cigar[1].Type == 'I')) ||
                  ((result.Cigar.Count > 2 && result.Cigar[result.Cigar.Count - 1].Type == 'S' && result.Cigar[result.Cigar.Count - 2].Type == 'I') ||
                   result.Cigar[result.Cigar.Count - 1].Type == 'I')))
            {
                return;
            }
            uint prefixIncludeUnanchoredInsertion = 0;
            uint suffixIncludeUnanchoredInsertion = 0;
            int firstMatchinCigar = 0;
            int lastMatchinCigar = result.Cigar.Count - 1;

            for (int i = 0; i < result.Cigar.Count; i++)
            {
                if (result.Cigar[i].Type == 'S')
                    prefixIncludeUnanchoredInsertion += result.Cigar[i].Length;
                else if (result.Cigar[i].Type == 'M')
                {
                    firstMatchinCigar = i;
                    break;
                }
                else if (result.Cigar[i].Type == 'I')
                {
                    if (indels[0].Type == AlleleCategory.Insertion && result.Cigar[i].Length < indels[0].Length)
                    {
                        prefixIncludeUnanchoredInsertion += result.Cigar[i].Length;
                        firstMatchinCigar = i + 1;
                    }
                    else
                        firstMatchinCigar = i;

                    break;
                }
                else
                {
                    throw new InvalidDataException(
                        string.Format(
                            "Found an unexpected cigar type: [{0}] before first/after last M", result.Cigar[i].Type));
                }
            }

            for (int i = result.Cigar.Count - 1; i >= 0; i--)
            {
                if (result.Cigar[i].Type == 'S')
                    suffixIncludeUnanchoredInsertion += result.Cigar[i].Length;
                else if (result.Cigar[i].Type == 'M')
                {
                    lastMatchinCigar = i;
                    break;
                }
                else if (result.Cigar[i].Type == 'I')
                {
                    if (indels[indels.Length - 1].Type == AlleleCategory.Insertion &&
                        result.Cigar[i].Length < indels[indels.Length - 1].Length)
                    {
                        suffixIncludeUnanchoredInsertion += result.Cigar[i].Length;
                        lastMatchinCigar = i - 1;
                    }
                    else
                        lastMatchinCigar = i;

                    break;
                }
                else
                {
                    throw new InvalidDataException(
                        string.Format(
                            "Found an unexpected cigar type: [{0}] before first/after last M", result.Cigar[i].Type));
                }
            }

            var newCigar = new CigarAlignment { new CigarOp('S', prefixIncludeUnanchoredInsertion) };
            for (int i = 0; i < result.Cigar.Count; i++)
            {
                if (i < firstMatchinCigar || i > lastMatchinCigar) continue;
                newCigar.Add(result.Cigar[i]);
            }

            newCigar.Add(new CigarOp('S', suffixIncludeUnanchoredInsertion));
            newCigar.Compress();
            result.Cigar = newCigar;

            var newSummary = Extensions.GetAlignmentSummary(result.Position - 1 - refSequenceStartIndex, result.Cigar, refSequence,
                read.Sequence, false, false);

            result.NumIndels = newSummary.NumIndels;
            result.NumNonNMismatches = newSummary.NumNonNMismatches;
            result.NumNonNSoftclips = newSummary.NumNonNSoftclips;
            result.NumSoftclips = newSummary.NumSoftclips;
            result.NumMismatchesIncludeSoftclip = newSummary.NumMismatchesIncludeSoftclip;
            //result.MismatchesIncludeSoftclip = newSummary.MismatchesIncludeSoftclip;
            result.NumIndelBases = newSummary.NumIndelBases;
            result.NumInsertedBases = newSummary.NumInsertedBases;
        }

        private bool IsUnbeatable(RealignmentResult bestResultSoFar)
        {
            return bestResultSoFar != null && bestResultSoFar.NumIndels == 1 && bestResultSoFar.NumMismatches == 0;
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

            var details = new ReadToRealignDetails(read, read.GetAdjustedPosition(true, probePrefix: _keepProbeSoftclips ? (int)nPrefixLength : 0), _keepProbeSoftclips, _keepBothSideSoftclips);
            var rightAnchoredDetails = new ReadToRealignDetails(read, read.GetAdjustedPosition(false, probePrefix: _keepProbeSoftclips ? (int)nPrefixLength : 0), _keepProbeSoftclips, _keepBothSideSoftclips);
            
            // align to all permutations of one indel, two indels, and three indels
            // try to skip alignment if we know it will fail 
            for (var i = 0; i < rankedIndels.Count; i++)
            {
                var indel1 = rankedIndels[i];


                // try aligning to one indel
                _oneIndelSimpleTargets[0] = indel1;
                var indel1Result = RealignToTargets(read, _oneIndelSimpleTargets, indelContexts, details, rightAnchoredDetails);
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
                    for (var j = i + 1; j < rankedIndels.Count; j++)
                    {
                        var indel2 = rankedIndels[j];
                        if (!CanCoexist(indel1, indel2, fromPairSpecificIndels)) continue;

                        _twoIndelSimpleTargets[0] = indel1;
                        _twoIndelSimpleTargets[1] = indel2;
                        Array.Sort(_twoIndelSimpleTargets, CompareSimple); // need to sort by position

                        // for optimization, don't try to align from a given side if we already failed aligning the indel on that side
                        var alreadyFailedFromLeft = indel1Result == null && _twoIndelSimpleTargets[0].Equals(indel1);
                        var alreadyFailedFromRight = indel1Result == null && _twoIndelSimpleTargets[1].Equals(indel1);
                        if (!alreadyFailedFromLeft) attemptedTargetSides++;
                        if (!alreadyFailedFromRight) attemptedTargetSides++;

                        var indel2Result = RealignToTargets(read, _twoIndelSimpleTargets, indelContexts, details, rightAnchoredDetails,
                            alreadyFailedFromLeft, alreadyFailedFromRight);
                        bestResultSoFar = _comparer.GetBetterResult(bestResultSoFar, indel2Result);

                    }
                }
            }

            return bestResultSoFar;
        }

        private bool NeedBetter(RealignmentResult bestResultSoFar)
        {
            return bestResultSoFar == null || bestResultSoFar.NumMismatches > 0;
        }

        public int Compare(PreIndel c1, PreIndel c2)
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

        public int CompareRightAnchor(PreIndel c1, PreIndel c2)
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

        public bool CanCoexist(HashableIndel indel1, HashableIndel indel2, bool pairSpecific)
        {
            if (!pairSpecific)
            {
                if (!indel1.InMulti || !indel2.InMulti)
                {
                    return false;
                }

                return indel1.OtherIndel == Helper.HashableToString(indel2);
            }

            else
            {
                if (indel1.ReferencePosition - indel2.ReferencePosition == 0 && indel1.Type == indel2.Type)
                    return false;

                // Assumption is that we are dealing with simple insertions & deletions. i.e. either ref or alt will have single base, and the other will have that single base + the varying bases.
                var indel1Bases = indel1.Type == AlleleCategory.Insertion
                    ? indel1.AlternateAllele
                    : indel1.ReferenceAllele;
                var indel2Bases = indel2.Type == AlleleCategory.Insertion
                    ? indel2.AlternateAllele
                    : indel2.ReferenceAllele;
                if (indel1.ReferencePosition - indel2.ReferencePosition == 0 && indel1Bases == indel2Bases)
                    return false;

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
}