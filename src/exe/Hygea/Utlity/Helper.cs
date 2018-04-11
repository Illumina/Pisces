using System;
using System.Linq;
using System.Text;
using Pisces.Domain.Utility;
using Alignment.Domain.Sequencing;
using RealignIndels.Models;
using Alignment.IO.Sequencing;
using Hygea.Logic;

namespace RealignIndels.Utlity
{
    public enum MatchType
    {
        Match,
        Mismatch,
        NMismatch,
        Unmapped
    }

    public static class Helper
    {
        public static bool IsValidMap(int[] positionMap, string refSequence)
        {
            var hasAnchor = false;
            foreach (var position in positionMap)
            {
                if (position == -1) continue;

                hasAnchor = true;
                if (position < 1 || position > refSequence.Length)
                    return false;
            }

            return hasAnchor;
        }

        public static CigarAlignment SoftclipCigar(CigarAlignment rawCigar, MatchType[] mismatchMap, uint originalSoftclipPrefix,
            uint originalSoftclipSuffix, bool rescueEdgeMatches = true, bool maskNsOnly = false, int prefixNs = 0, int suffixNs = 0)
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

            // Start at end of potential prefix softclip region and work backwards. This way we can rescue things that were matches previously sandwiched in softclips and now freed up by realignment.
            var mismatchMapIndex = (int)originalSoftclipPrefix;
            var startedSoftclip = false;

            var maxSoftclipPrefixLength = Math.Min(expandedCigar.FindIndex(x => x.Type != 'M') + 1, originalSoftclipPrefix);
            var maxSoftclipSuffixLength = Math.Min(expandedCigar.Count - expandedCigar.FindLastIndex(x => x.Type != 'M'), originalSoftclipSuffix);
            
            for (var i = 0; i < maxSoftclipPrefixLength; i++)
            {
                var index = (int)maxSoftclipPrefixLength - 1 - i;

                mismatchMapIndex--;

                if (expandedCigar[index].Type != 'M')
                {
                    continue;
                }

                bool shouldSoftclip;

                if (maskNsOnly)
                {
                    shouldSoftclip = index < prefixNs;
                }
                else
                {
                    shouldSoftclip = !rescueEdgeMatches || startedSoftclip || mismatchMap[mismatchMapIndex] != MatchType.Match;
                }

                if (shouldSoftclip)
                {
                    startedSoftclip = true;
                    expandedCigar[index] = new CigarOp('S', 1);
                }
            }

            // Start at beginning of potential suffix softclip region and work forwards
            startedSoftclip = false;
            mismatchMapIndex = mismatchMap.Length - (int)maxSoftclipSuffixLength - 1;
            for (var i = 0; i < maxSoftclipSuffixLength; i++)
            {
                var index = expandedCigar.Count() - ((int)maxSoftclipSuffixLength - i);
                mismatchMapIndex ++;

                if (expandedCigar[index].Type != 'M')
                {
                    continue;
                }
                bool shouldSoftclip;
                if (maskNsOnly)
                {
                    shouldSoftclip = suffixNs > 0 && mismatchMapIndex >= rawCigar.GetReadSpan() - suffixNs;
                }
                else
                {
                    shouldSoftclip = !rescueEdgeMatches || startedSoftclip || mismatchMap[mismatchMapIndex] != MatchType.Match;
                }
                if (shouldSoftclip)
                {
                    startedSoftclip = true;
                    expandedCigar[index] = new CigarOp('S', 1);
                }
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

            var lastOperation = string.Empty;
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

        public static MatchType[] GetMismatchMap(string readSequence, int[] positionMap, string refSequence)
        {
            var mismatchMap = new MatchType[readSequence.Length];

            for (var i = 0; i < positionMap.Length; i++)
            {
                var position = positionMap[i];
                if (position < 0) continue; // Skip insertions. This also skips softclips when they have already been marked as such (<0 in the position map), since they don't map to the reference anymore.

                if (position > refSequence.Length)
                    return null; // flag not valid

                if (refSequence[position - 1] != 'N' && readSequence[i] != 'N' && refSequence[position - 1] != readSequence[i])
                {
                    mismatchMap[i] = MatchType.Mismatch;
                }
                else if (refSequence[position - 1] == 'N' || readSequence[i] == 'N')
                {
                    mismatchMap[i] = MatchType.NMismatch;
                }
            }

            return mismatchMap;

        }

        public static int? GetEditDistance(string readSequence, int[] positionMap, string refSequence)
        {
            // Consolidating these two for maintainability. If it impacts performance significantly, revisit.
            var matchMap = GetMismatchMap(readSequence, positionMap, refSequence);
            return matchMap?.Count(x => x == MatchType.Mismatch);
        }
        
        public static int? GetNumMismatches(string sequence, string otherSequence, bool includeMismatchNs = false)
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
            for (int i= 0; i < sequence.Length; i++)
            {
                var nucleotide = sequence[fromEnd? sequence.Length - i - 1 : i];
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
    }
}
