using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class ScoredComparerTests
    {
        [Fact]
        public void GetBetterResult()
        {
            var perfect = new RealignmentResult();
            var oneIndel = new RealignmentResult()
            {
                NumIndels = 1
            };
            var twoIndels = new RealignmentResult()
            {
                NumIndels = 2,
            };
            var oneMismatch = new RealignmentResult()
            {
                NumMismatches = 1
            };
            var twoMismatches = new RealignmentResult()
            {
                NumMismatches = 2,
            };
            var oneIndelOneMismatch = new RealignmentResult()
            {
                NumIndels = 1,
                NumMismatches = 1
            };

            var comparer = new ScoredAlignmentComparer(new AlignmentScorer(){MismatchCoefficient = -1, IndelCoefficient = -1});
            Assert.Equal(perfect, comparer.GetBetterResult(perfect, oneIndel));
            Assert.Equal(perfect, comparer.GetBetterResult(perfect, twoIndels));
            Assert.Equal(perfect, comparer.GetBetterResult(perfect, oneMismatch));
            Assert.Equal(perfect, comparer.GetBetterResult(perfect, twoMismatches));
            Assert.Equal(perfect, comparer.GetBetterResult(perfect, oneIndelOneMismatch));

            // For ties, prefer the first one
            Assert.Equal(oneMismatch, comparer.GetBetterResult(oneMismatch, oneIndel));
            Assert.Equal(oneIndel, comparer.GetBetterResult(oneIndel, oneMismatch));
            Assert.Equal(twoIndels, comparer.GetBetterResult(twoIndels, twoMismatches));
            Assert.Equal(twoIndels, comparer.GetBetterResult(twoIndels, oneIndelOneMismatch));
            Assert.Equal(oneIndelOneMismatch, comparer.GetBetterResult(oneIndelOneMismatch, twoIndels));

            // Prefer the less negative score
            Assert.Equal(oneIndel, comparer.GetBetterResult(twoIndels, oneIndel));
            Assert.Equal(oneMismatch, comparer.GetBetterResult(twoIndels, oneMismatch));
            Assert.Equal(oneIndel, comparer.GetBetterResult(twoMismatches, oneIndel));
            Assert.Equal(oneMismatch, comparer.GetBetterResult(twoMismatches, oneMismatch));
            Assert.Equal(oneIndel, comparer.GetBetterResult(oneIndelOneMismatch, oneIndel));
            Assert.Equal(oneMismatch, comparer.GetBetterResult(oneIndelOneMismatch, oneMismatch));

            // Weight unevenly
            comparer = new ScoredAlignmentComparer(new AlignmentScorer() { MismatchCoefficient = -2, IndelCoefficient = -1 });
            Assert.Equal(oneIndel, comparer.GetBetterResult(oneMismatch, oneIndel));
            Assert.Equal(twoIndels, comparer.GetBetterResult(twoIndels, oneMismatch));
            Assert.Equal(oneMismatch, comparer.GetBetterResult(oneMismatch, twoIndels)); // same score, take first
            Assert.Equal(oneIndel, comparer.GetBetterResult(twoMismatches, oneIndel));
            Assert.Equal(oneMismatch, comparer.GetBetterResult(twoMismatches, oneMismatch));
            Assert.Equal(oneIndel, comparer.GetBetterResult(oneIndelOneMismatch, oneIndel));
            Assert.Equal(oneMismatch, comparer.GetBetterResult(oneIndelOneMismatch, oneMismatch));

        }
    }
}
