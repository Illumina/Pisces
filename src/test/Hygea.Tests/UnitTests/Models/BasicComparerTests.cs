using System.Runtime.InteropServices.ComTypes;
using Hygea.Logic;
using RealignIndels.Models;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{

    public class BasicComparerTests
    {
        [Fact]
        public void CompareAlignments()
        {
            var comparer = new BasicAlignmentComparer();

            var perfect = new AlignmentSummary();
            var otherPerfect = new AlignmentSummary();
            var oneMismatch = new AlignmentSummary();
            oneMismatch.NumMismatches = 1;
            var oneIndel = new AlignmentSummary();
            oneIndel.NumIndels = 1;
            var oneIndelOneMismatch = new AlignmentSummary();
            oneIndelOneMismatch.NumMismatches = 1;
            oneIndelOneMismatch.NumIndels = 1;

            Assert.Equal(1, comparer.CompareAlignments(perfect, null));

            // indels =, mismatch =, 0  
            Assert.Equal(0, comparer.CompareAlignments(perfect, otherPerfect));

            // indels =, mismatch <, 1  
            Assert.Equal(1, comparer.CompareAlignments(perfect, oneMismatch));

            // indels =, mismatch >, -1  
            Assert.Equal(-1, comparer.CompareAlignments(oneMismatch, perfect));

            // indels <, mismatch =, 1  
            Assert.Equal(1, comparer.CompareAlignments(perfect, oneIndel));

            // indels <, mismatch <, 1
            Assert.Equal(1, comparer.CompareAlignments(oneIndel, oneIndelOneMismatch));
  
            // indels <, mismatch >, -1  
            Assert.Equal(-1, comparer.CompareAlignments(oneMismatch, oneIndel));

            // indels >, mismatch =, -1  
            Assert.Equal(-1, comparer.CompareAlignments(oneIndel, perfect));

            // indels >, mismatch <, 1  
            Assert.Equal(1, comparer.CompareAlignments(oneIndel, oneIndelOneMismatch));

            // indels >, mismatch >, -1
            Assert.Equal(-1, comparer.CompareAlignments(oneIndelOneMismatch, perfect));

            var twoIndels = new AlignmentSummary() { NumIndels = 2, NumMismatches = 0 };
            var twoMismatches = new AlignmentSummary() {NumIndels = 0, NumMismatches = 2};
            
            // 1 mismatch and 0 indels in first, 2 indels and 0 mismatches in second, favor the first
            Assert.Equal(1, comparer.CompareAlignments(oneMismatch, twoIndels));

            // 1 mismatch and 0 indels in first, 1 indel in second, favor the second
            Assert.Equal(-1, comparer.CompareAlignments(oneMismatch, oneIndel));

            // 2 mismatches and 0 indels in first, 2 indels in second, favor the second -- special rule only applies to single-mismatch reads (with no indels)
            Assert.Equal(-1, comparer.CompareAlignments(twoMismatches, twoIndels));

            // 1 mismatch and 1 indel in first, 2 indels and 0 mismatches in second, favor the second -- special rule only applies to single-mismatch reads (with no indels)
            Assert.Equal(-1, comparer.CompareAlignments(oneIndelOneMismatch, twoIndels));

            var oneMismatchOneSoftclip = new AlignmentSummary() { NumNonNSoftclips = 1, NumMismatches = 1 };
            var oneSoftclip = new AlignmentSummary() { NumNonNSoftclips = 1 };
            var oneIndelOneSoftclip = new AlignmentSummary() { NumIndels = 1, NumNonNSoftclips = 1 };

            // 1 mismatch and 1 softclip, 0 mismatch and 1 softclip, favor the second
            Assert.Equal(-1, comparer.CompareAlignments(oneMismatchOneSoftclip, oneSoftclip));

            // 1 mismatch and 0 softclip, 0 mimatch and 1 softclip, favor the second
            Assert.Equal(-1, comparer.CompareAlignments(oneMismatch, oneSoftclip));

            // 0 mismatch and 0 softclip, 0 mismatch and 1 softclip, favor the first
            Assert.Equal(1, comparer.CompareAlignments(perfect, oneSoftclip));

            // 1 indel and 0 mismatch/sc, 1 indel and 1 softclip, favor the first
            Assert.Equal(1, comparer.CompareAlignments(oneIndel, oneIndelOneSoftclip));

            // 2 indel and 0 mismatch/sc, 1 indel and 1 softclip, favor the first
            Assert.Equal(1, comparer.CompareAlignments(twoIndels, oneIndelOneSoftclip));

            // 1 indel and 0 mismatch/sc, 0 indel and 1 softclip, favor the first
            Assert.Equal(1, comparer.CompareAlignments(oneIndel, oneSoftclip));

            // 2 indel and 0 mismatch/sc, 0 indel and 1 softclip, favor the first
            Assert.Equal(1, comparer.CompareAlignments(twoIndels, oneSoftclip));

        }

    }
}
