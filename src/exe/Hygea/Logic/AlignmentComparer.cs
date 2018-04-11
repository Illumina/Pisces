using System;
using RealignIndels.Models;

namespace Hygea.Logic
{
    public abstract class AlignmentComparer
    {
        public abstract int CompareAlignments(AlignmentSummary preferred, AlignmentSummary other);

        public RealignmentResult GetBetterResult(RealignmentResult preferred, RealignmentResult other)
        {
            if (preferred != null && other != null)
            {
                return CompareAlignments(preferred, other) >= 0 ? preferred : other;  // prefer first if equal
            }

            if (preferred != null)
                return preferred;

            if (other != null)
                return other;

            return null;

        }
    }


    public class BasicAlignmentComparer : AlignmentComparer
    {
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
        public override int CompareAlignments(AlignmentSummary original, AlignmentSummary other)
        {
            if (other == null) return 1;

            if (original.NumMismatches == 1 && original.NumIndels == 0 && other.NumIndels > 1) return 1;
            if (other.NumMismatches == 1 && other.NumIndels == 0 && original.NumIndels > 1) return -1;

            if (original.NumMismatches < other.NumMismatches) return 1;
            if (original.NumMismatches > other.NumMismatches) return -1;

            if (original.NumNonNSoftclips < other.NumNonNSoftclips) return 1;
            if (original.NumNonNSoftclips > other.NumNonNSoftclips) return -1;

            if (original.NumIndels < other.NumIndels) return 1;
            if (original.NumIndels > other.NumIndels) return -1;
            return 0;
        }
    }

    public class ScoredAlignmentComparer : AlignmentComparer
    {
        private AlignmentScorer _alignmentScorer;
        private bool _debug;

        public ScoredAlignmentComparer(AlignmentScorer alignmentScorer, bool debug = false)
        {
            _alignmentScorer = alignmentScorer;
            _debug = debug;
        }

        public override int CompareAlignments(AlignmentSummary originalAlignmentSummary, AlignmentSummary realignResult)
        {
            var originalScore = _alignmentScorer.GetAlignmentScore(originalAlignmentSummary);
            var realignedScore = _alignmentScorer.GetAlignmentScore(realignResult);

            if (_debug)
            {
                var origScoreString = originalAlignmentSummary.Cigar + "," + originalAlignmentSummary.NumMismatches + "," +
                                      originalScore;

                var realignedScoreString = realignResult.Cigar + "," + realignResult.NumMismatches + "," +
                                           realignedScore;
                Console.WriteLine(origScoreString + "," + realignedScoreString + "," + (realignedScore > originalScore));
            }

            if (originalScore > realignedScore) return 1;
            if (realignedScore > originalScore) return -1;
            return 0;
        }
    }
}