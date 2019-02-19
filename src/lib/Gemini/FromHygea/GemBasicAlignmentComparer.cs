using System;
using System.Linq;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;

namespace Gemini.FromHygea
{
    public class GemBasicAlignmentComparer : AlignmentComparer
    {
        private readonly bool _trackActualMismatches;

        public GemBasicAlignmentComparer(bool trackActualMismatches = false)
        {
            _trackActualMismatches = trackActualMismatches;
        }

        /// <summary>
        /// When comparing results:
        /// - Always minimize number of mismatches, regardless of number of indels
        /// - Given same number of mismatches, prefer fewer non-N softclips (0 better than 1, 1 better than 2)
        /// - Given same number of mismatches, prefer fewer indels (0 better than 1, 1 better than 2)
        /// 
        /// This maps to the following scenarios (written out to be explicit)
        /// indels =, mismatch =, 0
        /// indels =, mismatch <, 1
        /// indels =, mismatch >, -1
        /// indels <, mismatch =, 1
        /// indels <, mismatch <, 1
        /// indels <, mismatch >, -1
        /// indels >, mismatch =, -1
        /// indels >, mismatch <, 1
        /// indels >, mismatch >, -1
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        //public override int CompareAlignments(AlignmentSummary original, AlignmentSummary other)
        //{
        //    if (other == null) return 1;

        //    if (original.NumMismatches == 1 && original.NumIndels == 0 && other.NumIndels > 1) return 1;
        //    if (other.NumMismatches == 1 && other.NumIndels == 0 && original.NumIndels > 1) return -1;

        //    if (original.NumMismatches <= 1 && original.NumIndels == 1 && (other.NumMismatches + other.NumIndels > 1))
        //    {
        //        return 1;
        //    }
        //    if (other.NumMismatches <= 1 && other.NumIndels == 1 && (original.NumMismatches + original.NumIndels > 1))
        //    {
        //        return -1;
        //    }

        //    if (original.NumIndelBases == other.NumIndelBases)
        //    {
        //        if (original.NumIndels == 1 && other.NumIndels > 1 && original.NumMismatches <= 2)
        //        {
        //            return 1;
        //        }
        //        if (other.NumIndels == 1 && original.NumIndels > 1 && other.NumMismatches <= 2)
        //        {
        //            return -1;
        //        }

        //        if (original.NumMismatches > 0 && other.NumMismatches > 0)
        //        {
        //            // Rather have extra mismatches be low quality, as it's more likely they are illegitmate and this is in the right place
        //            if (original.SumOfMismatchingQualities <= other.SumOfMismatchingQualities)
        //            {
        //                //Console.WriteLine("Returning 1 due to original has lower mismatching qualities than new");
        //                return 1;
        //            }

        //            if (original.SumOfMismatchingQualities > other.SumOfMismatchingQualities)
        //            {
        //                //Console.WriteLine("Returning -1 due to new has lower mismatching qualities than original");
        //                return -1;
        //            }
        //        }
        //    }




        //    if (original.NumMatches - (original.NumMismatches + original.NumIndelBases) > other.NumMatches - (other.NumMismatches + other.NumIndelBases)) return 1;
        //    if (original.NumMatches - (original.NumMismatches + original.NumIndelBases) < other.NumMatches - (other.NumMatches - other.NumMismatches)) return -1;

        //    if (original.NumMismatches + original.NumInsertedBases < other.NumMismatches + other.NumInsertedBases) return 1;
        //    if (original.NumMismatches + original.NumInsertedBases > other.NumMismatches + other.NumInsertedBases) return -1;

        //    //if (original.NumNonNSoftclips < other.NumNonNSoftclips) return 1;
        //    //if (original.NumNonNSoftclips > other.NumNonNSoftclips) return -1;

        //    if (original.NumIndels < other.NumIndels && original.NumMismatches == other.NumMismatches) return 1;
        //    if (original.NumIndels > other.NumIndels && original.NumMismatches == other.NumMismatches) return -1;
        //    return 0;
        //}

        public override int CompareAlignments(AlignmentSummary original, AlignmentSummary other)
        {
            if (other == null) return 1;

            if (other.NumMismatches > original.NumMismatches + 3)
            {
                return 1;
            }

            if (original.NumMismatches == 1 && original.NumIndels == 0 && other.NumIndels > 1) return 1;
            if (other.NumMismatches == 1 && other.NumIndels == 0 && original.NumIndels > 1) return -1;

            if (original.NumMismatches < other.NumMismatches) return 1;
            if (original.NumMismatches > other.NumMismatches) return -1;

            if (original.NumIndelBases == other.NumIndelBases)
            {
                if (original.NumIndels == 1 && other.NumIndels > 1 && original.NumMismatches <= 2)
                {
                    return 1;
                }
                if (other.NumIndels == 1 && original.NumIndels > 1 && other.NumMismatches <= 2)
                {
                    return -1;
                }

                if (original.NumMismatches > 0 && other.NumMismatches > 0 && original.NumMismatches <= 5 && other.NumMismatches <=5)
                {
                    // Rather have extra mismatches be low quality, as it's more likely they are illegitmate and this is in the right place
                    if (original.SumOfMismatchingQualities <= other.SumOfMismatchingQualities)
                    {
                        //Console.WriteLine("Returning 1 due to original has lower mismatching qualities than new");
                        return 1;
                    }

                    if (original.SumOfMismatchingQualities > other.SumOfMismatchingQualities)
                    {
                        //Console.WriteLine("Returning -1 due to new has lower mismatching qualities than original");
                        return -1;
                    }
                }
            }

            //if (original.NumNonNSoftclips < other.NumNonNSoftclips) return 1;
            //if (original.NumNonNSoftclips > other.NumNonNSoftclips) return -1;

            if (original.NumIndels < other.NumIndels) return 1;
            if (original.NumIndels > other.NumIndels) return -1;
            return 0;
        }

        public override int CompareAlignmentsWithOriginal(AlignmentSummary other, AlignmentSummary original)
        {
            if (original == null) return 1;

            if (other.NumMismatches > original.NumMismatches + 3)
            {
                return -1;
            }

            // Short edge insertion should not be allowed if it doesn't make the read any better (TODO play with this. commenting out for now til I give it more thought.)
            //if (other.AnchorLength == 0 && other.NumIndels == 1 && other.NumInsertedBases <= 2 &&
            //    original.NumMismatchesIncludeSoftclip < other.NumInsertedBases)
            //{
            //    return -1;
            //}

            // TODO maybe tighter restrictions if stuff is not anchored.

            if (other.NumMismatchesIncludeSoftclip == 0)
            {
                // special rule for one indel vs. one mismatch 
                // Tweaked this from Xiao's to be specific to single-base indels
                if (other.NumIndels == 1 && other.NumIndelBases == 1 && original.NumMismatchesIncludeSoftclip == 1 && original.NumIndels == 0) return -1;

                if (original.NumIndels > 0) return 1;

                if (original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= 1) return 1;

                else return -1;
            }

            // Really doesn't look better
            if (original.NumMismatches - other.NumMismatches <= 0 && other.NumMatches - original.NumMatches <= 2 && other.NumIndels >= original.NumIndels && original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip <= 2)
            {
                return -1;
            }

            if (other.NumMismatches > original.NumMismatches && other.AnchorLength < 3)
            {
                return -1;
            }

            //var threshNumSharedMismatch = 8;
            var threshnumNotSharedMismatch = 2;
            var threshReductionInMismatches = 1;
            var threshReductionInmMismatchesForSmall = 2;
            var numSharedMismatch = 0;

            if (_trackActualMismatches)
            {
                if (original.MismatchesIncludeSoftclip == null || other.MismatchesIncludeSoftclip == null)
                {
                    numSharedMismatch = 0;
                }
                else
                {
                    numSharedMismatch = original.MismatchesIncludeSoftclip.Intersect(other.MismatchesIncludeSoftclip).ToList().Count();
                }
            }
            else
            {
               numSharedMismatch = Math.Min(original.NumMismatchesIncludeSoftclip,
                    other.NumMismatchesIncludeSoftclip); // Use an approximation if we don't want to do the whole thing
            }

            // Be more wary of shorter indels
            if (other.NumIndelBases <= 3 && (original.NumIndelBases == 0 || original.NumIndelBases > 3))
            {
                if (other.NumMismatchesIncludeSoftclip - numSharedMismatch == 0 && // the only mismatches in the new one are shared
                    //numSharedMismatch <= threshNumSharedMismatch && // what was the point of this?
                    original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= threshReductionInmMismatchesForSmall) // the new one has less mismatches overall
                {
                    return 1;
                }
                else return -1;
            }


            if (other.NumMismatchesIncludeSoftclip - numSharedMismatch <= threshnumNotSharedMismatch 
                // most of the mismatches are shared
                //&&
                //other.NumMismatches <= 3) // num mismatches is small
            )
            {
                if (original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= threshReductionInMismatches) return 1; // fewer mismatches than the original

                //// give preference to long indels
                //if (other.NumIndelBases - original.NumIndelBases >= 9 && original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= 2) return 1;

                // give preference to indels that have high frequency to start with
                //if (other.HasHighFrequencyIndel && original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= 2) return 1;

            }

            return -1*CompareAlignments(original, other);
        }
    }
}