using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class AlignmentScorerTests
    {
        [Fact]
        public void AlignmentScorer()
        {
            var scorer = new AlignmentScorer();

            var perfect = new AlignmentSummary();
            var oneIndel = new AlignmentSummary()
            {
                NumIndels = 1
            };
            var twoIndels = new AlignmentSummary()
            {
                NumIndels = 2,
            };
            var oneMismatch = new AlignmentSummary()
            {
                NumMismatches = 1
            };
            var twoMismatches = new AlignmentSummary()
            {
                NumMismatches = 2,
            };
            var oneIndelOneMismatch = new AlignmentSummary()
            {
                NumIndels = 1,
                NumMismatches = 1
            };
            var everything = new AlignmentSummary()
            {
                NumIndels = 1,
                NumMismatches = 1,
                NumIndelBases = 1,
                NumNonNSoftclips = 1,
                AnchorLength = 1
            };

            // By default, everything is 0
            Assert.Equal(0, scorer.GetAlignmentScore(perfect));
            Assert.Equal(0, scorer.GetAlignmentScore(oneIndel));
            Assert.Equal(0, scorer.GetAlignmentScore(twoIndels));
            Assert.Equal(0, scorer.GetAlignmentScore(oneIndelOneMismatch));

            // Count against mismatches: -1 score for each
            scorer = new AlignmentScorer() {MismatchCoefficient = -1};
            Assert.Equal(-1, scorer.GetAlignmentScore(oneMismatch));
            Assert.Equal(-1, scorer.GetAlignmentScore(oneIndelOneMismatch));
            Assert.Equal(-2, scorer.GetAlignmentScore(twoMismatches));
            Assert.Equal(0, scorer.GetAlignmentScore(oneIndel));
            Assert.Equal(0, scorer.GetAlignmentScore(twoIndels));

            // Count against indels: -1 score for each
            scorer = new AlignmentScorer() { IndelCoefficient = -1 };
            Assert.Equal(0, scorer.GetAlignmentScore(oneMismatch));
            Assert.Equal(-1, scorer.GetAlignmentScore(oneIndelOneMismatch));
            Assert.Equal(0, scorer.GetAlignmentScore(twoMismatches));
            Assert.Equal(-1, scorer.GetAlignmentScore(oneIndel));
            Assert.Equal(-2, scorer.GetAlignmentScore(twoIndels));

            // Count against indels and mismatches
            scorer = new AlignmentScorer() { IndelCoefficient = -3, MismatchCoefficient = -1};
            Assert.Equal(-1, scorer.GetAlignmentScore(oneMismatch));
            Assert.Equal(-4, scorer.GetAlignmentScore(oneIndelOneMismatch));
            Assert.Equal(-2, scorer.GetAlignmentScore(twoMismatches));
            Assert.Equal(-3, scorer.GetAlignmentScore(oneIndel));
            Assert.Equal(-6, scorer.GetAlignmentScore(twoIndels));

            // Make sure the other stuff is working
            scorer = new AlignmentScorer() {IndelLengthCoefficient = 1};
            Assert.Equal(1, scorer.GetAlignmentScore(everything));
            scorer.IndelCoefficient = 1;
            Assert.Equal(2, scorer.GetAlignmentScore(everything));
            scorer.MismatchCoefficient = 1;
            Assert.Equal(3, scorer.GetAlignmentScore(everything));
            scorer.NonNSoftclipCoefficient = 1;
            Assert.Equal(4, scorer.GetAlignmentScore(everything));
            scorer.AnchorLengthCoefficient = 1;
            Assert.Equal(5, scorer.GetAlignmentScore(everything));

        }
    }   
}
