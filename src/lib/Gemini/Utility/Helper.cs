using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alignment.Domain.Sequencing;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.Models;
using Gemini.Types;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using ReadRealignmentLogic.Models;

namespace Gemini.Utility
{
    public static class Helper
    {
        public static bool MultiIndelContainsIndel(PreIndel multiIndel, PreIndel singleIndel)
        {
            if (!multiIndel.InMulti || singleIndel.InMulti)
            {
                throw new ArgumentException("Not looking at a single and a multi.");
            }

            var singleToString = CandidateToString(singleIndel);
            if (multiIndel.OtherIndel == singleToString || CandidateToString(multiIndel) == singleToString)
            {
                return true;
            }

            return false;
        }

        public static string CandidateToString(PreIndel indel)
        {
            return indel.Chromosome + ":" + indel.ReferencePosition + " " + indel.ReferenceAllele + ">" + indel.AlternateAllele;
        }

        public static string HashableToString(HashableIndel indel)
        {
            return indel.Chromosome + ":" + indel.ReferencePosition + " " + indel.ReferenceAllele + ">" + indel.AlternateAllele;
        }

        public static HashableIndel CopyHashable(HashableIndel indel1, string otherIndel = null)
        {
            var indel1New = new HashableIndel()
            {
                AllowMismatchingInsertions = indel1.AllowMismatchingInsertions,
                AlternateAllele = indel1.AlternateAllele,
                Chromosome = indel1.Chromosome,
                InMulti = !string.IsNullOrEmpty(otherIndel) || indel1.InMulti,
                IsDuplication = indel1.IsDuplication,
                IsRepeat = indel1.IsRepeat,
                IsUntrustworthyInRepeatRegion = indel1.IsUntrustworthyInRepeatRegion,
                Length = indel1.Length,
                NumBasesInReferenceSuffixBeforeUnique = indel1.NumBasesInReferenceSuffixBeforeUnique,
                ReferencePosition = indel1.ReferencePosition,
                StringRepresentation = HashableToString(indel1),
                Score = indel1.Score,
                Type = indel1.Type,
                RefPrefix = indel1.RefPrefix,
                RefSuffix = indel1.RefSuffix,
                OtherIndel = string.IsNullOrEmpty(otherIndel) ? indel1.OtherIndel : otherIndel,
                ReferenceAllele = indel1.ReferenceAllele,
                RepeatUnit = indel1.RepeatUnit,
                NumRepeatsNearby = indel1.NumRepeatsNearby,
                NumApproxDupsLeft = indel1.NumApproxDupsLeft,
                NumApproxDupsRight = indel1.NumApproxDupsRight
            };
            return indel1New;
        }

     
        public static bool IsValidMap(int[] positionMap)
        {
            var hasAnchor = false;
            foreach (var position in positionMap)
            {
                // TODO make this a constant or something. Or null. -1 is confusing once we start subtracting stuff...
                if (position == -1) continue;

                hasAnchor = true;
                // TODO candidate for opt
            }

            return hasAnchor;
        }

        public static bool CompareSubstring(string str1, string str2, int startPosIn2)
        {
            for (int i = 0; i < str1.Length; ++i)
            {
                if (str1[i] != str2[startPosIn2 + i])
                {
                    return false;
                }
            }

            return true;
        }

        // TODO combine with RMxN logic
        public static int ComputeRMxNLengthForIndel(int variantPosition, string variantBases, string referenceBases, int maxRepeatUnitLength, out string repeatUnit)
        {
            repeatUnit = "";
            var maxRepeatsFound = 0;
            var prefixes = new List<string>();
            var suffixes = new List<string>();
            var length = variantBases.Length;

            for (var i = length - Math.Min(maxRepeatUnitLength, length); i < length; i++)
            {
                prefixes.Add(variantBases.Substring(0, length - i));
                suffixes.Add(variantBases.Substring(i, length - i));
            }
            var bookends = prefixes.Concat(suffixes);

            foreach (var bookend in bookends)
            {
                var backPeekPosition = variantPosition;

                // Keep ratcheting backward as long as this motif is repeating
                while (true)
                {
                    var newBackPeekPosition = backPeekPosition - bookend.Length;
                    if (newBackPeekPosition < 0) break;

                    if (!CompareSubstring(bookend, referenceBases, newBackPeekPosition)) break;

                    backPeekPosition = newBackPeekPosition;
                }

                // Read forward from first instance of motif, counting consecutive repeats
                var repeatCount = 0;
                var currentPosition = backPeekPosition;
                while (true)
                {
                    if (currentPosition + bookend.Length > referenceBases.Length) break;

                    if (!CompareSubstring(bookend, referenceBases, currentPosition)) break;

                    repeatCount++;
                    currentPosition += bookend.Length;
                }

                if (repeatCount > maxRepeatsFound)
                {
                    repeatUnit = bookend;
                    maxRepeatsFound = repeatCount;
                }
            }

            return maxRepeatsFound;
        }

        public static bool IsDuplication(string referenceSequence, int refPosition, bool isRepeat,
            string repeatUnit, string actualAltAllele, int minAlleleLength = 4)
        {
            bool isDuplication = false;
            // TODO - why the threshold on the allele length? Should it not be on the num effective repeats?
            if (actualAltAllele.Length < minAlleleLength)
            {
                return false;
            }

            // TODO I don't like this logic, it's too lenient
            if (isRepeat)
            {
                if (referenceSequence.Substring(refPosition - repeatUnit.Length, repeatUnit.Length) ==
                    repeatUnit ||
                    referenceSequence.Substring(refPosition + 1, repeatUnit.Length) == repeatUnit)
                {
                    isDuplication = true;
                }
            }
            else
            {
                var alleleSequence = actualAltAllele.Substring(1);

                for (int i = -2; i <= 2; i++)
                {
                    var startIndexInRef = refPosition + i;
                    if (startIndexInRef < 0)
                    {
                        continue;
                    }
                    var refSequence = referenceSequence.Substring(startIndexInRef, actualAltAllele.Length - 1);
                    if ( refSequence == alleleSequence )
                    {
                        isDuplication = true;
                        break;
                    }
                }
            }

            return isDuplication;
        }

        public static bool IsInHomopolymerStretch(string sequence, int refPosition, float thresholdProportion = 0.66f)
        {
            var aCount = 0;
            var tCount = 0;
            var cCount = 0;
            var gCount = 0;
            var nCount = 0;

            var windowSize = 10;
            for (var i = -1 * windowSize; i <= windowSize; i++)
            {
                var startIndexInRef = refPosition + i;
                if (startIndexInRef < 0)
                {
                    continue;
                }

                if (startIndexInRef > sequence.Length)
                {
                    break;
                }

                var refAllele = sequence[startIndexInRef];
                switch (refAllele)
                {
                    case 'A':
                    case 'a':
                    {
                        aCount++;
                        break;
                    }
                    case 'T':
                    case 't':
                    {
                        tCount++;
                        break;
                    }
                    case 'C':
                    case 'c':
                    {
                        cCount++;
                        break;
                    }
                    case 'G':
                    case 'g':
                    {
                        gCount++;
                        break;
                    }
                    case 'N':
                    {
                        nCount++;
                        break;
                    }
                    default:
                    {
                        // Don't care about other alleles
                        break;
                    }

                }
            }

            var totalWindowedBases = 2 * windowSize;
            var thresholdNumSingleBase = (totalWindowedBases * thresholdProportion) - nCount;
            return aCount > thresholdNumSingleBase || cCount > thresholdNumSingleBase || tCount > thresholdNumSingleBase ||
                   gCount > thresholdNumSingleBase;
            // TODO can actually do this while we're counting rather than counting everything, save a little time? Or not
        }

        public static bool RepeatDeletionFlankedByRepeats(string readSequence, HashableIndel priorIndel, int deletionPositionInRead)
        {
            var leftIsSketchy = false;
            var rightIsSketchy = false;

            var repeatUnitLength = priorIndel.RepeatUnit.Length;
            if (deletionPositionInRead >= repeatUnitLength)
            {
                var leftFlankingBases =
                    readSequence.Substring(deletionPositionInRead + 1 - repeatUnitLength, repeatUnitLength);
                if (leftFlankingBases == priorIndel.RepeatUnit)
                {
                    leftIsSketchy = true;
                }
            }

            if (readSequence.Length - deletionPositionInRead >= repeatUnitLength)
            {
                var rightFlankingBases =
                    readSequence.Substring(deletionPositionInRead + 1, repeatUnitLength);
                if (rightFlankingBases == priorIndel.RepeatUnit)
                {
                    rightIsSketchy = true;
                }
            }

            return leftIsSketchy && rightIsSketchy;
        }

        public static bool DeletionHasSketchyAnchor(string readSequence, HashableIndel priorIndel, int deletionPositionInRead)
        {
            var anyNonRepeatInLeftAnchor = false;
            var anyNonRepeatInRightAnchor = false;
            var assessedLeftAnchor = false;
            var assessedRightAnchor = false;


            for (int i = deletionPositionInRead + 1; i < readSequence.Length; i += priorIndel.RepeatUnit.Length)
            {
                var basesLeft = readSequence.Length - i;
                if (basesLeft < 0)
                {
                    break;
                }

                assessedRightAnchor = true;

                var numBasesToCompare = priorIndel.RepeatUnit.Length;
                var basesToCompare = priorIndel.RepeatUnit;
                if (basesLeft < numBasesToCompare)
                {
                    numBasesToCompare = basesLeft;
                    basesToCompare = basesToCompare.Substring(0, numBasesToCompare);
                }
 
                // TODO go back and get this logic from Hubble?
                var seqHere = readSequence.Substring(i, numBasesToCompare);
                if (seqHere != basesToCompare)
                {
                    // TODO PERF can we break here to save time?
                    anyNonRepeatInRightAnchor = true;
                }
            }

            for (int i = deletionPositionInRead + 1; i >= 0; i -= priorIndel.RepeatUnit.Length)
            {
                var basesLeft = i;
                if (basesLeft -1 < 0)
                {
                    break;
                }

                if (i + priorIndel.RepeatUnit.Length >= readSequence.Length)
                {
                    continue;
                }

                assessedLeftAnchor = true;
                var numBasesToCompare = priorIndel.RepeatUnit.Length;
                var basesToCompare = priorIndel.RepeatUnit;
                if (basesLeft < numBasesToCompare)
                {
                    numBasesToCompare = basesLeft;
                    basesToCompare = basesToCompare.Substring(priorIndel.RepeatUnit.Length - numBasesToCompare);
                }

                // TODO go back and get this logic from Hubble?
                var seqHere = readSequence.Substring(i - 1, numBasesToCompare);
                if (seqHere != basesToCompare)
                {
                    // TODO PERF can we break here to save time?
                    anyNonRepeatInLeftAnchor = true;
                }
            }

            if ((assessedLeftAnchor && !anyNonRepeatInLeftAnchor) || (assessedRightAnchor && !anyNonRepeatInRightAnchor))
            {
                return true;
            }

            return false;
        }

        public static CigarAlignment SoftclipCigar(CigarAlignment rawCigar, MatchType[] mismatchMap, uint originalSoftclipPrefix,
            uint originalSoftclipSuffix, bool rescueEdgeMatches = true, bool maskNsOnly = false, int prefixNs = 0, int suffixNs = 0,
            bool softclipEvenIfMatch = false, bool softclipRepresentsMess = true, float allowOneSoftclipMismatchPer = 12)
        {
            // If realignment creates a bunch of mismatches at beginning where it was once softclipped, 
            // can we softclip them?
            // Which bases should be softclipped?
            // - Things that were softclipped before and are mismatches? Or are Ms? 
            // - Things that were softclipped before and are Ns
            // Softclips in new alignment can be shorter than before, but not longer
            // Softclips should be terminal 
            // This is rooted in an assumption that the original softclips are terminal

            if (originalSoftclipPrefix == 0 && originalSoftclipSuffix == 0) return rawCigar;

            var expandedCigar = rawCigar.Expand();
            var changed = false;

            // Start at end of potential prefix softclip region and work backwards. This way we can rescue things that were matches previously sandwiched in softclips and now freed up by realignment.
            var mismatchMapIndex = (int)originalSoftclipPrefix;
            var startedSoftclip = false;

            var maxSoftclipPrefixLength = Math.Min(expandedCigar.FindIndex(x => x.Type != 'M' && x.Type != 'S') + 1, originalSoftclipPrefix);
            var maxSoftclipSuffixLength = Math.Min(expandedCigar.Count - expandedCigar.FindLastIndex(x => x.Type != 'M' && x.Type != 'S'), originalSoftclipSuffix);

            var minMismatchesToSoftclipPrefix = originalSoftclipPrefix / allowOneSoftclipMismatchPer;

            var minMismatchesToSoftclipSuffix = originalSoftclipSuffix / allowOneSoftclipMismatchPer;

            var numMismatchesInOrigPrefixClip = 0;
            var tmpMismatchMapIndex = mismatchMapIndex;
            for (var i = 0; i < maxSoftclipPrefixLength; i++)
            {
                tmpMismatchMapIndex--;
                var foundMismatch = (mismatchMap[tmpMismatchMapIndex] == MatchType.Mismatch || mismatchMap[tmpMismatchMapIndex] == MatchType.NMismatch);

                if (foundMismatch)
                {
                    numMismatchesInOrigPrefixClip++;
                }
            }

            var prefixTooMessyToRescue = numMismatchesInOrigPrefixClip > minMismatchesToSoftclipPrefix;

            var previousOp = 'N';
            var previousPreviousOp = 'N';
            for (var i = 0; i < maxSoftclipPrefixLength; i++)
            {
                var index = (int)maxSoftclipPrefixLength - 1 - i;

                mismatchMapIndex--;

                var opAtIndex = expandedCigar[index].Type;
                if (opAtIndex != 'M')
                {
                    previousOp = opAtIndex;
                    continue;
                }

                bool shouldSoftclip;

                if (maskNsOnly)
                {
                    shouldSoftclip = index < prefixNs;
                }
                else
                {
                    shouldSoftclip = softclipEvenIfMatch || !rescueEdgeMatches || startedSoftclip || prefixTooMessyToRescue;
                    // Rescue edge matches if we haven't seen any mismatches yet
                    if (!shouldSoftclip)
                    {
                        var foundMismatch = (mismatchMap[mismatchMapIndex] == MatchType.Mismatch || mismatchMap[mismatchMapIndex] == MatchType.NMismatch);
                        if (foundMismatch)
                        {
                            shouldSoftclip = true;
                        }
                    }

                    // Don't resoftclip if we are <1 base from the end.
                    if (previousOp == 'D' || previousOp == 'I' || (softclipRepresentsMess && (previousPreviousOp == 'D' || previousPreviousOp == 'I')))
                    {
                        // Always provide an anchor
                        shouldSoftclip = false;
                    }

                }

                if (shouldSoftclip)
                {
                    changed = true;
                    startedSoftclip = true;
                    expandedCigar[index] = new CigarOp('S', 1);
                }

                previousPreviousOp = previousOp;
                previousOp = opAtIndex;
            }

            // Start at beginning of potential suffix softclip region and work forwards
            startedSoftclip = false;
            mismatchMapIndex = mismatchMap.Length - (int)maxSoftclipSuffixLength - 1;

            var numMismatchesInOrigSuffixClip = 0;
            tmpMismatchMapIndex = mismatchMapIndex;
            for (var i = 0; i < maxSoftclipSuffixLength; i++)
            {
                tmpMismatchMapIndex++;
                var foundMismatch = (mismatchMap[tmpMismatchMapIndex] == MatchType.Mismatch || mismatchMap[tmpMismatchMapIndex] == MatchType.NMismatch);
                if (foundMismatch)
                {
                    numMismatchesInOrigSuffixClip++;
                }
            }

            var suffixTooMessyToRescue = numMismatchesInOrigSuffixClip > minMismatchesToSoftclipSuffix;
            previousOp = 'N';
            for (var i = 0; i < maxSoftclipSuffixLength; i++)
            {
                var index = expandedCigar.Count() - ((int)maxSoftclipSuffixLength - i);
                mismatchMapIndex++;

                var opAtIndex = expandedCigar[index].Type;

                if (opAtIndex != 'M')
                {
                    previousOp = opAtIndex;
                    continue;
                }
                bool shouldSoftclip;
                if (maskNsOnly)
                {
                    shouldSoftclip = suffixNs > 0 && mismatchMapIndex >= rawCigar.GetReadSpan() - suffixNs;
                }
                else
                {
                    shouldSoftclip = !rescueEdgeMatches || startedSoftclip || suffixTooMessyToRescue;

                    // Rescue edge matches if we haven't seen any mismatches yet
                    if (!shouldSoftclip)
                    {
                        var foundMismatch = (mismatchMap[mismatchMapIndex] == MatchType.Mismatch || mismatchMap[mismatchMapIndex] == MatchType.NMismatch);
                        if (foundMismatch)
                        {
                            shouldSoftclip = true;
                        }
                    }
                    if (previousOp == 'D' || previousOp == 'I')
                    {
                        // Always provide an anchor
                        shouldSoftclip = false;
                    }


                }
                if (shouldSoftclip)
                {
                    changed = true;
                    startedSoftclip = true;
                    expandedCigar[index] = new CigarOp('S', 1);
                }

                previousOp = opAtIndex;
            }

            // We can only anchor a read on an M, so if we've softclipped everything away we're in trouble! Add back one.
            if (!expandedCigar.Any(o => o.Type == 'M'))
            {
                var hasAnyNonSoftclipPos = expandedCigar.Any(o => o.Type != 'S');
                var firstNonSoftclipPos = hasAnyNonSoftclipPos
                    ? expandedCigar.FindIndex(o => o.Type != 'S')
                    : (expandedCigar.Count);
                // Set the last position of softclip to M.
                expandedCigar[firstNonSoftclipPos - 1] = new CigarOp('M', expandedCigar[firstNonSoftclipPos - 1].Length);
            }

            if (!changed)
            {
                return rawCigar;
            }

            // Re-compile back into a revised cigar.
            var revisedCigar = new CigarAlignment();
            foreach (var cigarOp in expandedCigar)
            {
                revisedCigar.Add(cigarOp);
            }
            revisedCigar.Compress();

            return revisedCigar;
        }

        public static CigarAlignment ConstructCigar(int[] positionMap, bool softClip = false)
        {
            var cigarBuilder = new StringBuilder();

            var lastRefPosition = -1;

            var lastOperation = String.Empty;
            var lastOperationLength = 0;

            for (var i = 0; i < positionMap.Length; i++)
            {
                var position = positionMap[i];
                var myOperation = position == -1 ? "I" : "M";

                if (myOperation == "M")
                {
                    // check if we need to write a deletion
                    if (lastRefPosition != -1 && position > lastRefPosition + 1)
                    {
                        cigarBuilder.Append(lastOperationLength + lastOperation);  // dump out last op
                        cigarBuilder.Append((position - lastRefPosition - 1) + "D");

                        lastOperation = "D";
                        lastOperationLength = 0;
                    }

                    lastRefPosition = position;
                }

                if (myOperation != lastOperation)
                {
                    if (!string.IsNullOrEmpty(lastOperation) && lastOperation != "D")
                        cigarBuilder.Append(lastOperationLength + lastOperation);  // dump out last op

                    lastOperation = myOperation;
                    lastOperationLength = 1;
                }
                else
                {
                    lastOperationLength++;
                }
            }

            cigarBuilder.Append(lastOperationLength + lastOperation);

            var cigar = new CigarAlignment(cigarBuilder.ToString());
            if (softClip)
            {
                if (cigar[0].Type != 'M')
                {
                    cigar[0] = new CigarOp('S', cigar[0].Length);
                }

                if (cigar[cigar.Count - 1].Type != 'M')
                {
                    cigar[cigar.Count - 1] = new CigarOp('S', cigar[cigar.Count - 1].Length);
                }
            }

            return cigar;
        }

        public static MatchType[] GetMismatchMap(string readSequence, PositionMap positionMap, string refSequence, int startIndexInRefSequence = 0)
        {
            var mismatchMap = new MatchType[readSequence.Length];

            for (var i = 0; i < positionMap.Length; i++)
            {
                var position = positionMap.GetPositionAtIndex(i) - startIndexInRefSequence;
                if (position < 0) {


                    if (readSequence[i] == 'N')
                    {
                        mismatchMap[i] = MatchType.NMismatch;
                    }
                    else
                    {
                        mismatchMap[i] = MatchType.Unmapped;
                    }

                    continue; // Skip insertions. This also skips softclips when they have already been marked as such (<0 in the position map), since they don't map to the reference anymore.
                }

                if (position > refSequence.Length)
                {
                    return null; // flag not valid
}

                if (position - 1 >= 0)
                {
                    if (refSequence[position - 1] != 'N' && readSequence[i] != 'N' &&
                        refSequence[position - 1] != readSequence[i])
                    {
                        mismatchMap[i] = MatchType.Mismatch;
                    }
                    else if (refSequence[position - 1] == 'N' || readSequence[i] == 'N')
                    {
                        mismatchMap[i] = MatchType.NMismatch;
                    }
                }
                else
                {
                    // TODO or should we return null/flag invalid?
                    mismatchMap[i] = MatchType.Unmapped;
                }
            }

            return mismatchMap;

        }

        public static int? GetNumMismatches(string readSequence, PositionMap positionMap, string refSequence, int startIndexInRefSequence = 0)
        {
            // Consolidating these two for maintainability. If it impacts performance significantly, revisit.
            var matchMap = GetMismatchMap(readSequence, positionMap, refSequence, startIndexInRefSequence);
            return matchMap?.Count(x => x == MatchType.Mismatch);
        }

        public static MdCounts GetMdCountsWithSubstitutions(string mdString, string readSequence, int softclipLength, int softclipEndLength = 0)
        {
            int head = 0;

            var subA = 0;
            var subT = 0;
            var subC = 0;
            var subG = 0;
            var subN = 0;

            var numA = 0;
            var numT = 0;
            var numC = 0;
            var numG = 0;

            var maxRunLength = 0;
            var runLength = 1;
            var numInRuns = 0;

            var currentIndexInRead = softclipLength;
            bool pastFirstLength = false;
            bool inRun = false;
            bool hasIndels = false;
            bool badCharacter = false;

            for (int i = 0; i < mdString.Length; ++i)
            {
                if (char.IsDigit(mdString, i)) continue;
                if (mdString[i] == '^')
                {
                    badCharacter = true;
                    hasIndels = true;
                    break;
                }

                var referenceChar = mdString[i];

                switch (referenceChar)
                {
                    case 'A':
                        numA++;
                        break;
                    case 'T':
                        numT++;
                        break;
                    case 'C':
                        numC++;
                        break;
                    case 'G':
                        numG++;
                        break;
                    default:
                        break;
                }


                var length = int.Parse(mdString.Substring(head, i - head));

                if (pastFirstLength)
                {
                    if (length < 1)
                    {
                        inRun = true;
                        runLength++;
                    }
                    else
                    {
                        if (runLength > 1)
                        {
                            numInRuns += runLength;
                        }

                        maxRunLength = Math.Max(runLength, maxRunLength);
                        runLength = 1;
                        inRun = false;
                    }
                }

                pastFirstLength = true;

                currentIndexInRead += length;
                var substitutionChar = readSequence[currentIndexInRead];
                switch (substitutionChar)
                {
                    case 'A':
                        subA++;
                        break;
                    case 'T':
                        subT++;
                        break;
                    case 'C':
                        subC++;
                        break;
                    case 'G':
                        subG++;
                        break;
                    case 'N':
                        subN++;
                        break;
                    default:

                        break;
                }

                currentIndexInRead++;

                head = i + 1;
            }

            if (inRun)
            {
                if (runLength > 1)
                {
                    numInRuns += runLength;
                }
                maxRunLength = Math.Max(runLength, maxRunLength);
            }

            if (!hasIndels)
            {
                if (currentIndexInRead + softclipEndLength != readSequence.Length)
                {
                    var length = int.Parse(mdString.Substring(head, mdString.Length - head));
                    currentIndexInRead += length;

                    if (currentIndexInRead + softclipEndLength != readSequence.Length)
                    {
                        hasIndels = true;
                    }
                }
            }

            if (hasIndels)
            {
                var indelEvidenceDetails = badCharacter
                    ? "had an unexpected character"
                    : $"total bases covered by tag: {currentIndexInRead}, read sequence length: {readSequence.Length}, softclip end: {softclipEndLength}";
                throw new ArgumentException($"MD parsing is not intended to be used on indel-containing reads. Found evidence for indels in this MD tag: {mdString} ({indelEvidenceDetails}).");
            }


            return new MdCounts(numA, numT, numC, numG, maxRunLength, numInRuns, subA, subT, subC, subG, subN);
        }

        public static MdCounts GetMdCounts(string mdString)
        {
            var numA = 0;
            var numT = 0;
            var numC = 0;
            var numG = 0;

            var maxRunLength = 0;
            var runLength = 0;
            var numInRuns = 0;
            foreach (var item in mdString)
            {
                switch (item)
                {
                    case 'A':
                        runLength++;
                        numA++;
                        break;
                    case 'T':
                        runLength++;
                        numT++;
                        break;
                    case 'C':
                        runLength++;
                        numC++;
                        break;
                    case 'G':
                        runLength++;
                        numG++;
                        break;
                    case '0':
                        break;
                    default:
                        if (runLength > 1)
                        {
                            numInRuns += runLength;
                        }
                        maxRunLength = Math.Max(runLength, maxRunLength);
                        runLength = 0;
                        break;
                }
            }
            return new MdCounts(numA, numT, numC, numG, maxRunLength,  numInRuns);
        }

        public static int GetSumOfMismatchQualities(byte[] quals, string readSequence, PositionMap positionMap, string refSequence,
            int startIndexInRefSequence = 0)
        {
            var matchMap = GetMismatchMap(readSequence, positionMap, refSequence, startIndexInRefSequence);
            return GetSumOfMismatchQualities(matchMap, quals);
        }

        public static int GetSumOfMismatchQualities(MatchType[] mismatchMap, byte[] quals)
        {
            var sum = 0;
            for (int i = 0; i < mismatchMap.Length; i++)
            {
                if (mismatchMap[i] == MatchType.Mismatch)
                {
                    sum += quals[i];
                }
            }

            return sum;
        }
        
        public static string NifyMismatches(string sequence, string otherSequence, List<int> nifiedBases)
        {
            nifiedBases.Clear();
            if (sequence.Length != otherSequence.Length)
                return null;

            var newSequence = new char[otherSequence.Length];

            for (var i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] == otherSequence[i])
                {
                    newSequence[i] = otherSequence[i];
                }
                else if (sequence[i] == 'N')
                {
                    newSequence[i] = otherSequence[i];
                }
                else
                {
                    newSequence[i] = 'N';
                    nifiedBases.Add(i);
                }
            }

            return string.Join("",newSequence);
        }

        /// <summary>
        /// Returns hamming distance, optionally adjusted for Ns.
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="otherSequence"></param>
        /// <param name="includeMismatchNs"></param>
        /// <returns></returns>
        public static int? GetHammingNumMismatches(string sequence, string otherSequence, bool includeMismatchNs = false)
        {
            if (sequence.Length != otherSequence.Length)
                return null;

            var mismatches = 0;

            for (var i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] == otherSequence[i]) continue;
                var eitherBaseIsN = sequence[i] == 'N' || otherSequence[i] == 'N';

                // If either base is N, by default, we don't count it as a mismatch. 
                // But if includeMismatchNs is true, we count all unmatching pairs as mismatches.
                if (!eitherBaseIsN || includeMismatchNs)
                {
                    mismatches++;
                }
            }

            return mismatches;
        }

        public static int GetCharacterBookendLength(string sequence, char character, bool fromEnd)
        {
            var characterCount = 0;
            for (int i = 0; i < sequence.Length; i++)
            {
                var nucleotide = sequence[fromEnd ? sequence.Length - i - 1 : i];
                if (nucleotide == character)
                {
                    characterCount++;
                }
                else
                {
                    break;
                }
            }

            return characterCount;
        }

        public static bool IsMatch(HashableIndel hashable1, HashableIndel hashable2)
        {
            var equivPosition = hashable1.Chromosome == hashable2.Chromosome &&
                                hashable1.ReferencePosition == hashable2.ReferencePosition;

            if (!equivPosition)
            {
                return false;

            }

            var equivAlleles = hashable1.Type == AlleleCategory.Insertion ? InsertionsAreMatch(hashable1.AlternateAllele, hashable2.AlternateAllele) :
                hashable1.ReferenceAllele.Length == hashable2.ReferenceAllele.Length;
            return equivAlleles;
        }

        public static bool IsMatch(PreIndel pre, HashableIndel hashable)
        {
            var equivPosition = pre.Chromosome == hashable.Chromosome &&
                                pre.ReferencePosition == hashable.ReferencePosition;

            if (!equivPosition)
            {
                return false;

            }

            var equivAlleles = pre.Type == AlleleCategory.Insertion ? InsertionsAreMatch(pre.AlternateAllele, hashable.AlternateAllele):
                pre.ReferenceAllele.Length == hashable.ReferenceAllele.Length;
            return equivAlleles;
        }

        private static bool InsertionsAreMatch(string pre, string hashable)
        {
            // TODO consider using same logic in realignment assessment and relevant indel selection
            // TODO do we really need to ensure the sequence is the same when doing pair-specific relevant indel selection? How much time would we save by not?
            var numDisagreements = 0;
            var maxNumDisagreements = hashable.Length / 5;
            if (pre.Substring(1) == hashable.Substring(1))
            {
                return true;
            }

            if (pre.Length != hashable.Length)
            {
                return false;
            }

            for (int i = 0; i < pre.Length; i++)
            {
                var candidateBase = pre[i];
                var hashableBase = hashable[i];
                if (candidateBase != 'N' && hashableBase != 'N' && candidateBase != hashableBase)
                {
                    numDisagreements++;

                    if (numDisagreements > maxNumDisagreements)
                    {
                        return false;
                    }
                    return false;
                }    
            }

            return true;
        }
    }
}
