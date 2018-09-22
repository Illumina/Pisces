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

        public static bool IsRepeat(string overlapBases)
        {
            var windowsize = 1;
            var maxWindowSize = Math.Min(overlapBases.Length - 1, 3);

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
                    if (overlapBases == baseSeq) return true;
                    else if (overlapBases.Contains(baseSeq))
                    {
                        if (overlapBases.IndexOf(baseSeq) == 0)
                        {
                            string remainingSeq = overlapBases.Substring(baseSeq.Length, overlapBases.Length - baseSeq.Length);
                            if (unit.Substring(0, remainingSeq.Length) == remainingSeq)
                            {
                                return true;
                            }
                        }
                    }
                }
                windowsize++;
            }
            return false;
        }

        public static bool BridgeAnchored(Read mergedRead)
        {
            var stitchedReadBases = mergedRead.BamAlignment.Bases;
            var directions = mergedRead.SequencedBaseDirectionMap;
            var stitchedBases = new StringBuilder();
            for (int i = 0; i < directions.Length; i++)
            {
                if (directions[i] == DirectionType.Stitched)
                {
                    stitchedBases.Append(stitchedReadBases[i]);
                }
            }

            var overlapBases = stitchedBases.ToString();
            if (overlapBases.Length <= 3) return true;
            if (IsRepeat(overlapBases)) return false;

            return true;

        } 
    }
}


    
           
