using System;
using System.Linq;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;

namespace Gemini.FromHygea
{
    public class GemBasicAlignmentComparer : AlignmentComparer
    {
        private readonly bool _trackActualMismatches;
        private readonly bool _trustSoftclips;

        public GemBasicAlignmentComparer(bool trackActualMismatches = false, 
            bool trustSoftclips = false)
        {
            _trackActualMismatches = trackActualMismatches;
            _trustSoftclips = trustSoftclips;
        }
        
        public override int CompareAlignments(AlignmentSummary original, AlignmentSummary other, bool penalizeIndelCount = true)
        {
            if (other == null) return 1;

            // Original was much better
            if (other.NumMismatches > original.NumMismatches + 5)
            {
                return 1;
            }

            if (original.NumMismatches == 1 && original.NumIndels == 0 && other.NumIndels > 1)
            {
                return 1;
            }

            if (other.NumMismatches == 1 && other.NumIndels == 0 && original.NumIndels > 1)
            {
                return -1;
            }

            // Original wasn't that bad, and it's better than new
            if (original.NumMismatchesIncludeSoftclip < 5 &&
                original.NumMismatchesIncludeSoftclip < other.NumMismatchesIncludeSoftclip)
            {
                return 1;
            }
            // Original was bad, but is reasonably better than new
            if (original.NumMismatchesIncludeSoftclip >= 5 &&
                original.NumMismatchesIncludeSoftclip < other.NumMismatchesIncludeSoftclip * 0.8)
            {
                return 1;
            }

            // New is reasonably better than original
            if (original.NumMismatchesIncludeSoftclip > other.NumMismatchesIncludeSoftclip + 1)
            {
                return -1;
            }

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
                        return 1;
                    }

                    if (original.SumOfMismatchingQualities > other.SumOfMismatchingQualities)
                    {
                        return -1;
                    }
                }
            }

            if (original.NumMismatchesIncludeSoftclip > 0 && other.NumMismatchesIncludeSoftclip == 0)
            {
                return -1;
            }

            if (penalizeIndelCount)
            {
                if (original.NumIndels < other.NumIndels)
                {
                    return 1;
                }

                if (original.NumIndels > other.NumIndels)
                {
                    return -1;
                }
            }

            return 0;
        }

        public override int CompareAlignmentsWithOriginal(AlignmentSummary other, AlignmentSummary original,
            bool treatKindly = false)
        {
            if (treatKindly)
            {
                if (other.NumMismatches <= 1 &&
                    other.NumMismatchesIncludeSoftclip <= original.NumMismatchesIncludeSoftclip)
                {
                    return 1;
                }
            }
            return CompareAlignmentsWithOriginal2(other, original);
        }

        public int CompareAlignmentsWithOriginal2(AlignmentSummary other, AlignmentSummary original)
        {
            if (original == null) return 1;

            // Looks a lot worse
            if (
                other.NumMismatches > original.NumMismatches + 6 || 
                (other.NumMismatches > original.NumMismatches + 3 && other.NumMatches - original.NumMatches <= 10))
            {
                return -1;
            }

            if (other.NumMismatches + other.NumSoftclips + other.NumIndelBases ==
                original.NumMismatches + original.NumSoftclips + original.NumIndelBases)
            {
                // Haven't moved the needle much, and for a short indel(s) that probably would have been called originally.
                if (other.NumDeletedBases < 3 && other.NumInsertedBases == 0)
                {
                    return -1;
                }
            }

            // TODO consider re-instating?
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
                if (other.NumIndels == 1 && other.NumIndelBases == 1 && original.NumMismatchesIncludeSoftclip == 1 &&
                    original.NumIndels == 0)
                {
                    return -1;
                }

                if (original.NumIndels > 0)
                {
                    return 1;
                }

                if (original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= 1)
                {
                    return 1;
                }

                // TODO Commented this out because it did not seem to make sense upon some troubleshooting I was doing 7/3. Why would we want to ding perfect results other than the special rule?
                //return -1;
            }

            // Be nice to large indels, if they fit well in the new read and the old read was messy to begin with
            // It has to actually look at least a little better, though.
            // There may be some philosophy here with original gap penalties and indel size and placement... TBD
            if (original.NumMismatches > 2 && (other.NumMismatches - original.NumMismatches <= 2) && other.NumIndels - original.NumIndels <=2 && other.NumIndelBases > 10 && (other.NumMismatches < original.NumMismatches || other.NumMismatchesIncludeSoftclip < (original.NumMismatchesIncludeSoftclip * 0.9) || other.NumSoftclips < original.NumSoftclips))
            {
                return 1;
            }

            if (other.NumIndelBases <= 2 && other.NumIndelBases > original.NumIndelBases &&
                other.NumMismatches >= original.NumMismatches - 1 && (original.NumMismatchesIncludeSoftclip > 10 &&
                ((!_trustSoftclips && original.NumSoftclips * 0.8 <= other.NumSoftclips) || original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip <= original.NumMismatchesIncludeSoftclip / 5)))
            {
                //Short indel introduced where there were a lot of softclips and didn't improve a lot
                return -1;
            }

            // If original had tons of mismatches/softclips, and realign is better but only a little, this may just be chance (ex: polyT) -> don't accept realignment
            if (original.NumMismatchesIncludeSoftclip > 10 &&
                original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip <= original.NumMismatchesIncludeSoftclip / 10)
            {
                return -1;
            }

            // Super long original softclip and num mismatches
            // Better be a lot shorter softclip and not add mismatches, or have a bunch more matches from softclips being unmasked.
            // ?Need to have added at least 1 match for every 2 softclips removed.
            const int numSoftclipsToBeConsideredSuperLong = 20;
            // TODO un-magic these numbers
            if (original.NumSoftclips > numSoftclipsToBeConsideredSuperLong && ((other.NumSoftclips/(float)original.NumSoftclips >= 0.75 && other.NumMismatches >= original.NumMismatches)|| 
                                               (other.NumMatches - original.NumMatches) < (original.NumSoftclips - other.NumSoftclips)/2f))
            {
                return -1;
            }

            // Really doesn't look better
            if (original.NumMismatches - other.NumMismatches <= 0 && other.NumMatches - original.NumMatches <= 2 && other.NumIndels >= original.NumIndels && original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip <= 2)
            {
                return -1;
            }

            var benefitOfDoubtForOrigScMismatches = 0.75;
            if (other.NumMismatches > original.NumMismatches && (other.NumMismatchesIncludeSoftclip > (original.NumMismatchesIncludeSoftclip * benefitOfDoubtForOrigScMismatches)) && other.AnchorLength < 3)
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
                    //wary of shorter indel and shared mismatches
                    return 1;
                }

                if (other.NumMismatchesIncludeSoftclip - original.NumMismatchesIncludeSoftclip <= 1)
                {
                    return 1;
                }
                return -1;
            }


            if (other.NumMismatchesIncludeSoftclip - numSharedMismatch <= threshnumNotSharedMismatch )
            {
                // most of the mismatches are shared and num mismatches is small

                if (original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= threshReductionInMismatches)
                {
                    // fewer mismatches than original
                    return 1; 
                }
            }

            return -1*CompareAlignments(original, other);
        }
    }
}