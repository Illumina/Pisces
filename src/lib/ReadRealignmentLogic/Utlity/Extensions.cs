using System;
using System.Collections.Generic;
using System.IO;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;

namespace ReadRealignmentLogic.Utlity
{
    public static class Extensions
    {
        public static AlignmentSummary GetAlignmentSummary(this Read read, string refSequence, bool trackActualMismatches = true, bool checkSoftclipMismatches = true, int startIndexInRefSequence = 0)
        {
            var startIndexInReference = read.Position - 1 - startIndexInRefSequence;
            return GetAlignmentSummary(startIndexInReference, read.CigarData, refSequence, read.BamAlignment.Bases, trackActualMismatches, checkSoftclipMismatches);
        }

        public static AlignmentSummary GetAlignmentSummary(int startIndexInReference, CigarAlignment cigarData, string refSequence, string readSequence, bool trackActualMismatches = true, bool checkSoftclipsForMismatches = true, int probeSoftclipPrefix = 0, int probeSoftclipSuffix = 0)
        {
            var summary = new AlignmentSummary();
            summary.Cigar = cigarData;

            if (checkSoftclipsForMismatches)
            {
                startIndexInReference = startIndexInReference - (int)cigarData.GetPrefixClip();
            }

            var startIndexInRead = 0;
            var anchorLength = 0;
            var endAnchorLength = 0;
            var hasHitNonMatch = false;

            for (var cigarOpIndex = 0; cigarOpIndex < cigarData.Count; cigarOpIndex++)
            {
                var operation = cigarData[cigarOpIndex];
                switch (operation.Type)
                {
                    case 'S': // soft-clip
                        for (var i = 0; i < operation.Length; i++)
                        {
                            summary.NumSoftclips++;

                            if (readSequence[startIndexInRead + i] != 'N')
                            {
                                summary.NumNonNSoftclips++;

                                if (checkSoftclipsForMismatches)
                                {
                                    if (startIndexInReference + i < 0 ||
                                        startIndexInReference + i >= refSequence.Length)
                                    {
                                        summary.NumMismatchesIncludeSoftclip++;
                                    }
                                    else if (readSequence[startIndexInRead + i] !=
                                                refSequence[startIndexInReference + i])
                                    {
                                        summary.NumMismatchesIncludeSoftclip++;

                                        if (trackActualMismatches)
                                        {
                                            if (summary.MismatchesIncludeSoftclip == null)
                                            {
                                                summary.MismatchesIncludeSoftclip = new List<string> { };
                                            }

                                            var mismatch = string.Format("{0}_{1}_{2}",
                                                startIndexInReference + i,
                                                refSequence[startIndexInReference + i],
                                                readSequence[startIndexInRead + i]);
                                            summary.MismatchesIncludeSoftclip.Add(mismatch);
                                        }
                                    }

                                }
                            }

                        }
                        break;
                    case 'M': // match or mismatch
                        for (var i = 0; i < operation.Length; i++)
                        {
                            if (startIndexInReference + i > refSequence.Length - 1)
                            {
                                return null;
                                throw new InvalidDataException(
                                    "Read goes off the end of the genome: " + startIndexInReference + ":" +
                                    cigarData.ToString() + " vs " + startIndexInReference + " + " + refSequence.Length);
                            }

                            var baseAtIndex = readSequence[startIndexInRead + i];
                            if (baseAtIndex != 'N' && baseAtIndex !=
                                refSequence[startIndexInReference + i])
                            {
                                summary.NumMismatches++;
                                summary.NumMismatchesIncludeSoftclip++;

                                if (trackActualMismatches)
                                {
                                    if (summary.MismatchesIncludeSoftclip == null)
                                    {
                                        summary.MismatchesIncludeSoftclip = new List<string> { };
                                    }

                                    var mismatch = string.Format("{0}_{1}_{2}", startIndexInReference + i,
                                        refSequence[startIndexInReference + i], readSequence[startIndexInRead + i]);
                                    summary.MismatchesIncludeSoftclip.Add(mismatch);
                                }

                                hasHitNonMatch = true;
                                endAnchorLength = 0;
                            }
                            else
                            {
                                if (baseAtIndex != 'N')
                                {
                                    summary.NumMatches++;
                                }

                                if (!hasHitNonMatch)
                                {
                                    anchorLength++;
                                }
                                endAnchorLength++;
                            }
                        }
                        break;
                    case 'I': // insertion
                        hasHitNonMatch = true;
                        endAnchorLength = 0;
                        summary.NumIndels++;
                        summary.NumIndelBases += (int)operation.Length;
                        summary.NumInsertedBases += (int)operation.Length;
                        break;
                    case 'D': // deletion
                        hasHitNonMatch = true;
                        endAnchorLength = 0;
                        summary.NumIndels++;
                        summary.NumIndelBases += (int)operation.Length;
                        summary.NumDeletedBases += (int)operation.Length;
                        break;
                }


                if (operation.IsReadSpan())
                    startIndexInRead += (int)operation.Length;

                if (operation.IsReferenceSpan())
                {
                    startIndexInReference += (int)operation.Length;
                }
                if (checkSoftclipsForMismatches && operation.Type == 'S') { startIndexInReference += (int)operation.Length; }

            }

            summary.AnchorLength = Math.Min(anchorLength, endAnchorLength);

            return summary;
        }

        private static int GetAdjustedPositionFromLeft(this Read read, bool skipNs, int probePrefix = 0)
        {
            var adjustedPosition = read.Position - (int)read.CigarData.GetPrefixClip();

            if (read.CigarData[0].Type == 'I')
                adjustedPosition -= (int)read.CigarData[0].Length;

            if (read.CigarData.Count >= 2 && read.CigarData[0].Type == 'S' && read.CigarData[1].Type == 'I')
                adjustedPosition -= (int)read.CigarData[1].Length;

            if (skipNs)
            {
                adjustedPosition += read.GetNPrefix();
            }

            return adjustedPosition + probePrefix;
        }

        public static int GetNPrefix(this Read read)
        {
            var numPrefixNs = 0;
            foreach (var nuc in read.Sequence)
            {
                if (nuc == 'N')
                {
                    numPrefixNs++;
                }
                else break;
            }
            return numPrefixNs;

        }

        public static int GetNSuffix(this Read read)
        {
            var numPrefixNs = 0;
            for (int index = read.Sequence.Length - 1; index >= 0; index--)
            {
                var nuc = read.Sequence[index];
                if (nuc == 'N')
                {
                    numPrefixNs++;
                }
                else break;
            }
            return numPrefixNs;

        }

        private static int GetAdjustedPositionFromRight(this Read read, bool skipNs, int probePrefix = 0)
        {
            // account for soft clip at end or  insertion
            var maxRefPosition = -1;
            var indexOfMax = -1;
            for (var i = read.PositionMap.Length - 1; i >= 0; i--)
            {
                if (read.PositionMap[i] == -1)
                    continue;

                maxRefPosition = read.PositionMap[i];
                indexOfMax = i;
                break;
            }

            var adjustedMaxPosition = maxRefPosition;
            for (var i = indexOfMax + 1; i < read.PositionMap.Length - (skipNs ? read.GetNSuffix() : 0); i++)
            {
                adjustedMaxPosition++;
            }

            return adjustedMaxPosition - (read.ReadLength - (skipNs ? read.GetNPrefix() : 0) - (skipNs ? read.GetNSuffix() : 0)) + 1 + probePrefix;
        }

        public static int GetAdjustedPosition(this Read read, bool anchorLeft, bool skipNs = true, int probePrefix = 0)
        {
            var newPosition = anchorLeft
                ? read.GetAdjustedPositionFromLeft(skipNs, probePrefix)
                : read.GetAdjustedPositionFromRight(skipNs, probePrefix);

            return newPosition;
        }

        public static int NumIndels(this CigarAlignment cigar)
        {
            var numIndels = 0;
            for (var i = 0; i < cigar.Count; i++)
            {
                var op = cigar[i];
                if (op.Type == 'I' || op.Type == 'D')
                    numIndels++;
            }

            return numIndels;
        }

    }
}