using System.Runtime.InteropServices.ComTypes;
using Xunit;
using System.Collections.Generic;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;


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


        [Fact]
        public void CompareAlignmentsWithOriginal()
        {
            var comparer = new BasicAlignmentComparer();

            var perfect = new AlignmentSummary();
            var otherPerfect = new AlignmentSummary();
            var oneMismatch = new AlignmentSummary();
            oneMismatch.NumMismatchesIncludeSoftclip = 1;
            var oneIndel = new AlignmentSummary();
            oneIndel.NumIndels = 1;
            var oneIndel2 = new AlignmentSummary();
            oneIndel2.NumIndels = 1;
            var oneIndelOneMismatch = new AlignmentSummary();
            oneIndelOneMismatch.NumMismatchesIncludeSoftclip = 1;
            oneIndelOneMismatch.NumIndels = 1;
            var twoIndels = new AlignmentSummary() { NumIndels = 2, NumMismatchesIncludeSoftclip = 0 };
            var twoIndels2 = new AlignmentSummary() { NumIndels = 2, NumMismatchesIncludeSoftclip = 0 };
            var twoMismatches = new AlignmentSummary() { NumIndels = 0, NumMismatchesIncludeSoftclip = 2 };

            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(perfect, null));

            // --------------
            // realignment has zero mismatch 
            // --------------
            // both perfect, pick original  
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(perfect, otherPerfect));

            // indels both 0, mismatch smaller by 1, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(perfect, oneMismatch));

            // special rule for one indel vs. one mismatch , pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(oneIndel, oneMismatch));

            // gain one indel, mismatch both 0, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(oneIndel, perfect));
            
            // gain one indel, mismatch smaller by 2, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(oneIndel, twoMismatches));

            // indels both 1, mismatch both 0, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(oneIndel, oneIndel2));

            // indels both 1, mismatch smaller by 1, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(oneIndel, oneIndelOneMismatch));

            // special rule doesn't apply to two indels, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(twoIndels, oneMismatch));

            // gain two indels, mismatch both 0, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(twoIndels, perfect));

            // gain two indels, mismatch smaller by 2, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(twoIndels, twoMismatches));

            // indels both 2, mismatch both 0, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(twoIndels, twoIndels2));

            // gain one indel, mismatch smaller by 1, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(twoIndels, oneIndelOneMismatch));

            // gain one indel, mismatch both 0, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(twoIndels, oneIndel));



            // --------------
            // realignment has >=1 mismatch
            // --------------

            // --------------
            // short indels <= 3bp 
            // --------------
            var shortIndelOneMismatchNew = new AlignmentSummary() { NumIndelBases = 3, NumIndels = 1, NumMismatchesIncludeSoftclip = 1, MismatchesIncludeSoftclip = new List<string> { "5_A_C" } };
            var shortIndelOneMismatchShared = new AlignmentSummary() { NumIndelBases = 3, NumIndels = 1, NumMismatchesIncludeSoftclip = 1, MismatchesIncludeSoftclip = new List<string> { "3_A_C" } };
            var zeroIndelWithFourMismatch = new AlignmentSummary() { NumMismatchesIncludeSoftclip = 4, MismatchesIncludeSoftclip = new List<string> {"0_A_C", "1_A_C", "2_A_C", "3_A_C"} };
            var zeroIndelWithThreeMismatch = new AlignmentSummary() { NumMismatchesIncludeSoftclip = 3, MismatchesIncludeSoftclip = new List<string> { "1_A_C", "2_A_C", "3_A_C" } };

            // realignment introduced a new mismatch, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(shortIndelOneMismatchNew, zeroIndelWithFourMismatch));

            // the one mismatch exists in both original and realignment, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(shortIndelOneMismatchShared, zeroIndelWithFourMismatch));

            // reduction of mismatch < 3, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(shortIndelOneMismatchShared, zeroIndelWithThreeMismatch));


            // --------------
            // indels > 3bp 
            // --------------
            var MediumIndelOneMismatchNew = new AlignmentSummary() { NumIndelBases = 4, NumIndels = 1, NumMismatchesIncludeSoftclip = 1, MismatchesIncludeSoftclip = new List<string> { "5_A_C" } };
            var MediumIndelOneMismatchShared = new AlignmentSummary() { NumIndelBases = 4, NumIndels = 1, NumMismatchesIncludeSoftclip = 1, MismatchesIncludeSoftclip = new List<string> { "3_A_C" } };
            var MediumIndelTwoMismatchBothNew = new AlignmentSummary() { NumIndelBases = 4, NumIndels = 1, NumMismatchesIncludeSoftclip = 2, MismatchesIncludeSoftclip = new List<string> { "5_A_C", "6_A_C" } };
            var MediumIndelTwoMismatchOneShared = new AlignmentSummary() { NumIndelBases = 4, NumIndels = 1, NumMismatchesIncludeSoftclip = 2, MismatchesIncludeSoftclip = new List<string> { "2_A_C", "5_A_C" } };
            var zeroIndelWithFiveMismatch = new AlignmentSummary() { NumMismatchesIncludeSoftclip = 5, MismatchesIncludeSoftclip = new List<string> { "0_A_C", "1_A_C", "2_A_C", "3_A_C", "4_A_C" } };

            // mismatch smaller by 2, realignment has one mismatch, shared with original, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(MediumIndelOneMismatchShared, zeroIndelWithThreeMismatch));

            // mismatch smaller by 3, realignment has one mismatch, shared with original, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(MediumIndelOneMismatchShared, zeroIndelWithFourMismatch));

            // mismatch smaller by 2, realignment created 1 new mismatch, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(MediumIndelOneMismatchNew, zeroIndelWithThreeMismatch));

            // mismatch smaller by 3, realignment created 1 new mismatch, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(MediumIndelOneMismatchNew, zeroIndelWithFourMismatch));

            // mismatch smaller by 3, but realignment created 2 new mismatches, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(MediumIndelTwoMismatchBothNew, zeroIndelWithFiveMismatch));

            // mismatch smaller by 3, realignment has two mismatches, one introduced by indel, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(MediumIndelTwoMismatchOneShared, zeroIndelWithFiveMismatch));

            // mismatch smaller by 2, realignment has two mismatches, one introduced by indel, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(MediumIndelTwoMismatchOneShared, zeroIndelWithFourMismatch));


            var LongIndelOneMismatchShared = new AlignmentSummary() { NumIndelBases = 9, NumIndels = 1, NumMismatchesIncludeSoftclip = 1, MismatchesIncludeSoftclip = new List<string> { "5_A_C" } };
            var LongIndelTwoMismatchOneShared = new AlignmentSummary() { NumIndelBases = 9, NumIndels = 1, NumMismatchesIncludeSoftclip = 2, MismatchesIncludeSoftclip = new List<string> { "2_A_C", "5_A_C" } };
            var LongIndelTwoMismatchBothNew = new AlignmentSummary() { NumIndelBases = 9, NumIndels = 1, NumMismatchesIncludeSoftclip = 2, MismatchesIncludeSoftclip = new List<string> { "5_A_C", "6_A_C" } };

            var HighFrequencyIndelOneMismatchShared = new AlignmentSummary() { NumIndelBases = 4, NumIndels = 1, HasHighFrequencyIndel = true, NumMismatchesIncludeSoftclip = 1, MismatchesIncludeSoftclip = new List<string> { "5_A_C" } };
            var HighFrequencyIndelTwoMismatchOneShared = new AlignmentSummary() { NumIndelBases = 4, NumIndels = 1, HasHighFrequencyIndel = true, NumMismatchesIncludeSoftclip = 2, MismatchesIncludeSoftclip = new List<string> { "2_A_C", "5_A_C" } };
            var HighFrequencyIndelTwoMismatchBothNew = new AlignmentSummary() { NumIndelBases = 9, NumIndels = 1, NumMismatchesIncludeSoftclip = 2, MismatchesIncludeSoftclip = new List<string> { "5_A_C", "6_A_C" } };

            var zeroIndelWithTwoMismatch = new AlignmentSummary() { NumMismatchesIncludeSoftclip = 2, MismatchesIncludeSoftclip = new List<string> { "1_A_C", "2_A_C"} };


            // mismatch smaller by 2 (3->1), realignment has one mismatch, shared with original, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(LongIndelOneMismatchShared, zeroIndelWithThreeMismatch));

            // mismatch smaller by 1 (2->1), realignment has one mismatch, shared with original, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(LongIndelOneMismatchShared, zeroIndelWithTwoMismatch));

            // mismatch smaller by 2 (4->2), realignment has two mismatches, one introduced by indel, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(LongIndelTwoMismatchOneShared, zeroIndelWithFourMismatch));

            // mismatch smaller by 1 (3->2), realignment has two mismatches, one introduced by indel, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(LongIndelTwoMismatchOneShared, zeroIndelWithThreeMismatch));

            // mismatch smaller by 2 (4->2), but realignment created 2 new mismatches, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(LongIndelTwoMismatchBothNew, zeroIndelWithFourMismatch));

            // mismatch smaller by 2 (3->1), realignment has one mismatch, shared with original, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(HighFrequencyIndelOneMismatchShared, zeroIndelWithThreeMismatch));

            // mismatch smaller by 1 (2->1), realignment has one mismatch, shared with original, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(HighFrequencyIndelOneMismatchShared, zeroIndelWithTwoMismatch));

            // mismatch smaller by 2 (4->2), realignment has two mismatches, one introduced by indel, pick new
            Assert.Equal(1, comparer.CompareAlignmentsWithOriginal(HighFrequencyIndelTwoMismatchOneShared, zeroIndelWithFourMismatch));

            // mismatch smaller by 1 (3->2), realignment has two mismatches, one introduced by indel, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(HighFrequencyIndelTwoMismatchOneShared, zeroIndelWithThreeMismatch));

            // mismatch smaller by 2 (4->2), but realignment created 2 new mismatches, pick original
            Assert.Equal(-1, comparer.CompareAlignmentsWithOriginal(HighFrequencyIndelTwoMismatchBothNew, zeroIndelWithFourMismatch));
        }

    }
}
