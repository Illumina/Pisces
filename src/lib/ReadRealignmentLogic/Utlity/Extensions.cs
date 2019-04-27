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
            var hasHitNonNSoftclip = false;

            for (var cigarOpIndex = 0; cigarOpIndex < cigarData.Count; cigarOpIndex++)
            {
                var operation = cigarData[cigarOpIndex];
                var opLength = (int)(operation.Length);
                switch (operation.Type)
                {
                    case 'S': // soft-clip
                        for (var i = 0; i < opLength; i++)
                        {
                            summary.NumSoftclips++;

                            // No special treatement for Ns that are inside the softclip. Because the whole N-softclip distinction was meant to deal with padding-type softclips, I think.
                            if (readSequence[startIndexInRead + i] != 'N' || hasHitNonNSoftclip)
                            {
                                hasHitNonNSoftclip = true;

                                summary.NumNonNSoftclips++;

                                if (checkSoftclipsForMismatches)
                                {
                                    if (startIndexInReference + i < 0 ||
                                        startIndexInReference + i >= refSequence.Length)
                                    {
                                        summary.NumMismatchesIncludeSoftclip++;
                                    }
                                    else if (readSequence[startIndexInRead + i] !=
                                                refSequence[startIndexInReference + i] && readSequence[startIndexInRead + i] != 'N')
                                    {
                                        summary.NumMismatchesIncludeSoftclip++;

                                        if (trackActualMismatches)
                                        {
                                            if (summary.MismatchesIncludeSoftclip == null)
                                            {
                                                summary.MismatchesIncludeSoftclip = new List<string> { };
                                            }
                                            
                                            // TODO WHEN KILL HYGEA, remove this if we're not using anymore, to save time
                                            var mismatch = string.Format("{0}_{1}_{2}",
                                                startIndexInReference + i,
                                                refSequence[startIndexInReference + i],
                                                readSequence[startIndexInRead + i]);
                                            summary.MismatchesIncludeSoftclip.Add(mismatch);
                                        }
                                    }

                                }
                            }
                            //else
                            //{
                            //    if (!hasHitNonNSoftclip)
                            //    {
                            //        nSoftclipLength++;
                            //    }
                            //}

                        }
                        break;
                    case 'M': // match or mismatch
                        for (var i = 0; i < opLength; i++)
                        {
                            if (startIndexInReference + i > refSequence.Length - 1)
                            {
                                return null;
                                throw new InvalidDataException(
                                    "Read goes off the end of the genome: " + startIndexInReference + ":" +
                                    cigarData.ToString() + " vs " + startIndexInReference + " + " + refSequence.Length);
                            }

                            if (startIndexInReference + i < 0)
                            {
                                throw new InvalidDataException(
                                    "Read would be before beginning of the chromosome: " + startIndexInReference + ":" +
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

                                    // TODO WHEN KILL HYGEA, remove this if we're not using anymore, to save time
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
                        summary.NumIndelBases += opLength;
                        summary.NumInsertedBases += opLength;
                        break;
                    case 'D': // deletion
                        hasHitNonMatch = true;
                        endAnchorLength = 0;
                        summary.NumIndels++;
                        summary.NumIndelBases += opLength;
                        summary.NumDeletedBases += opLength;
                        break;
                }


                if (operation.IsReadSpan())
                    startIndexInRead += opLength;

                if (operation.IsReferenceSpan())
                {
                    startIndexInReference += opLength;
                }
                if (checkSoftclipsForMismatches && operation.Type == 'S') { startIndexInReference += opLength; }

            }

            summary.AnchorLength = Math.Min(anchorLength, endAnchorLength);

            return summary;
        }

        private static int GetAdjustedPositionFromLeft(this Read read, bool skipNs, int probePrefix = 0)
        {
            var adjustedPosition = read.Position - (int)read.CigarData.GetPrefixClip();

            var firstCigarOpType = read.CigarData[0].Type;

            if (firstCigarOpType == 'I')
                adjustedPosition -= (int)read.CigarData[0].Length;

            if (read.CigarData.Count >= 2 && firstCigarOpType == 'S' && read.CigarData[1].Type == 'I')
                adjustedPosition -= (int)read.CigarData[1].Length;

            if (skipNs)
            {
                adjustedPosition += read.GetNPrefix(); // TODO looks like we call this a lot. Optimize.
            }

            return adjustedPosition + probePrefix;
        }

        private static int GetAdjustedPositionFromRight(this Read read, bool skipNs, int probePrefix = 0)
        {
            // account for soft clip at end or  insertion
            var maxRefPosition = -1;
            var indexOfMax = -1;
            for (var i = read.PositionMap.Length - 1; i >= 0; i--)
            {
                if (read.PositionMap.GetPositionAtIndex(i) == -1)
                    continue;

                maxRefPosition = read.PositionMap.GetPositionAtIndex(i);
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

        public static int NumIndelBases(this CigarAlignment cigar)
        {
            var numIndels = 0;
            for (var i = 0; i < cigar.Count; i++)
            {
                var op = cigar[i];
                if (op.Type == 'I' || op.Type == 'D')
                    numIndels+= (int)op.Length;
            }

            return numIndels;
        }
    }
}