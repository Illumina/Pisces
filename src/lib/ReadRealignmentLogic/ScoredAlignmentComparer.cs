using System;
using ReadRealignmentLogic.Models;

namespace ReadRealignmentLogic
{
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
        public override int CompareAlignmentsWithOriginal(AlignmentSummary realignResult, AlignmentSummary originalAlignmentSummary, bool treatKindly = false)
        {
            var originalScore = _alignmentScorer.GetAlignmentScore(originalAlignmentSummary);
            var realignedScore = _alignmentScorer.GetAlignmentScore(realignResult);

            if (realignedScore > originalScore) return 1;
            if (originalScore > realignedScore) return -1;
            return 0;
        }
    }
}