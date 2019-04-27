using System;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Common.IO.Utility;
using Pisces.Domain.Utility;
using Alignment.Domain.Sequencing;
using System.Collections.Generic;
using System.Text;
using StitchingLogic;
using StitchingLogic.Models;

namespace StitchingLogic
{
    public class OverlapEvaluator
    {
        public static List<string> SlideSequence(string overlapBases, int windowsize)
        {
            if (windowsize >= 4)
            {
                throw new ArgumentException("Window size set above 3.");
            }

            var overlapLength = overlapBases.Length;
            List<string> possibleUnits = new List<string>();
            int i = 0;
            ///
            /// Requires at least 5bp to span all possible combinations of 3base units. (e.g. ABCAB = ABC, BCA, CAB)
            /// This would require the beginning of indexing of sequence up to 3 positions (0-based)
            /// In cases where the provided sequence is less than 5bp long, the index limit is set to be overlapLength - windowsize to prevent crashing
            /// ExampleA: 4 (overlapLength) - 3 (windowsize) = 1. so ATTT would get ATT, TTT
            /// ExampleB: 3 (overlapLength) - 3 (windowsize) = 1. So ATT would just stop at ATT
            int stringIndexLimit = (overlapLength >= 5) ? 2 : overlapLength - windowsize;
            while (i <= stringIndexLimit)
            {
                string unit = overlapBases.Substring(i, windowsize);
                if (!possibleUnits.Contains(unit))
                {
                    possibleUnits.Add(unit);
                }
                i++;
            }
            return possibleUnits;
        }

        // TODO combine with HK original
        public static bool IsRepeat(string overlapBases, int maxRepeatUnitLength, out string repeatUnit)
        {
            var windowsize = 1;
            var maxWindowSize = Math.Min(overlapBases.Length - 1, maxRepeatUnitLength);

            while (windowsize <= maxWindowSize)
            {
                var units = SlideSequence(overlapBases, windowsize);
                foreach (string unit in units)
                {
                    int multiplier = overlapBases.Length / unit.Length;
                    if (overlapBases.Length == unit.Length)
                    {
                        continue;
                    }

                    string baseSeq = String.Concat(Enumerable.Repeat(unit, multiplier));
                    if (overlapBases == baseSeq)
                    {
                        repeatUnit = unit;
                        return true;
                    }
                    else if (overlapBases.Contains(baseSeq))
                    {
                        if (overlapBases.IndexOf(baseSeq) == 0)
                        {
                            string remainingSeq = overlapBases.Substring(baseSeq.Length, overlapBases.Length - baseSeq.Length);
                            if (unit.Substring(0, remainingSeq.Length) == remainingSeq)
                            {
                                repeatUnit = unit;
                                return true;
                            }
                        }
                    }
                }
                windowsize++;
            }

            repeatUnit = null;
            return false;
        }


        public static string Repeat(string value, int count)
        {
            //var rptString = "";
            //for (int i = 0; i < count; i++)
            //{
            //    rptString += value;
            //}
            //return rptString;
            //return string.Concat(Enumerable.Repeat(value, count));
            return new StringBuilder(value.Length * count).Insert(0, value, count).ToString();
        }

        public static int GetNumberOfRepeats(string bases, string repeatUnit)
        {
            // TODO msisensor has some special logic about revcomping if not mapped?
            var startPos = 0;
            var count = 0;

            var maxCount = 0;

            while (startPos <= bases.Length)
            {
                // TODO could I make this faster by not checking indexof again until the startpos has moved past the last indexof found?
                startPos = bases.IndexOf(repeatUnit, startPos) - repeatUnit.Length;
                if (startPos < 0)
                {
                    break;
                }

                count = 0;

                var tstart0 = startPos;
                var tstart = tstart0;

                while (tstart0 == (tstart = bases.IndexOf(repeatUnit, tstart)))
                {
                    count++;
                    tstart += repeatUnit.Length;
                    tstart0 = tstart;
                }

                startPos++;

            }

            return count;
        }

        public static bool IsRepeat(string overlapBases)
        {
            var windowsize = 1;
            var maxWindowSize = Math.Min(overlapBases.Length - 1, 3);

            while (windowsize <= maxWindowSize)
            {
                var units = SlideSequence(overlapBases, windowsize);
                foreach (var unit in units)
                {
                    int multiplier = overlapBases.Length / unit.Length;
                    if (overlapBases.Length == unit.Length)
                    {
                        continue;
                    }

                    if (StringIsRepeatOfUnits(overlapBases, unit)) return true;
                    //var baseSeq = Repeat(unit, multiplier);

                    //if (overlapBases == baseSeq) return true;
                    //else if (overlapBases.Contains(baseSeq))
                    //{
                    //    if (overlapBases.IndexOf(baseSeq) == 0)
                    //    {
                    //        string remainingSeq = overlapBases.Substring(baseSeq.Length, overlapBases.Length - baseSeq.Length);
                    //        if (unit.Substring(0, remainingSeq.Length) == remainingSeq)
                    //        {
                    //            return true;
                    //        }
                    //    }
                    //}
                }
                windowsize++;
            }
            return false;
        }

        private static bool StringIsRepeatOfUnits(string overlapBases, string unit)
        {
            int partialRepeats = 0;
            int wholeRepeats = 0;
            bool isFullRepeat = true;

            var firstIndexOfRepeatInOverlap = overlapBases.IndexOf(unit, StringComparison.Ordinal);
            if (firstIndexOfRepeatInOverlap > 0)
            {
                if (firstIndexOfRepeatInOverlap >= unit.Length)
                {
                    isFullRepeat = false;
                    return false;
                }

                if (overlapBases.Substring(0, firstIndexOfRepeatInOverlap) !=
                    unit.Substring(unit.Length - firstIndexOfRepeatInOverlap))
                {
                    isFullRepeat = false;
                    return false;
                }

                partialRepeats++;
            }

            for (int i = firstIndexOfRepeatInOverlap; i < overlapBases.Length; i += unit.Length)
            {
                if (overlapBases.IndexOf(unit, i, StringComparison.Ordinal) == i)
                {
                    wholeRepeats++;
                }
                else
                {
                    var remainingLength = overlapBases.Length - i;

                    if (remainingLength <= unit.Length)
                    {
                        if (overlapBases.Substring(i, remainingLength) != unit.Substring(0, remainingLength))
                        {
                            isFullRepeat = false;
                            return false;
                        }
                        else
                        {
                            isFullRepeat = true;
                            partialRepeats++;
                        }
                    }
                    else
                    {
                        isFullRepeat = false;
                    }

                    break;
                }
            }

            if (isFullRepeat)
            {
                return true;
            }

            return false;
        }

        public static bool BridgeAnchored(Read mergedRead)
        {
            var stitchedReadBases = mergedRead.BamAlignment.Bases;
            var directions = mergedRead.SequencedBaseDirectionMap;
            var stitchedBases = new StringBuilder();

            var sameAsLast = true;
            var overlapLength = 0;
            var lastBase = '?';

            for (int i = 0; i < directions.Length; i++)
            {
                if (directions[i] == DirectionType.Stitched)
                {
                    var stitchedBase = stitchedReadBases[i];
                    if (overlapLength > 0 && sameAsLast)
                    {
                        sameAsLast = stitchedBase == lastBase;
                    }
                    lastBase = stitchedBase;
                    overlapLength++;

                    stitchedBases.Append(stitchedBase);
                }
            }

            if (overlapLength <= 3) return true;
            if (sameAsLast) return false; // All bases are the same - by definition, this is not anchored

            var overlapBases = stitchedBases.ToString();
            if (IsRepeat(overlapBases)) return false;

            return true;

        }

        public static bool BridgeAnchored(string overlapBases)
        {

            var overlapLength = overlapBases.Length;
            if (overlapLength <= 3) return true;

            if (IsRepeat(overlapBases)) return false;

            return true;

        }
    }
}


    
           
