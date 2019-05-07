using Alignment.Domain;
using Gemini.CandidateIndelSelection;
using Xunit;

namespace Gemini.Tests
{
    public class OverlappingIndelHelpersTests
    {
        [Fact]
        public void AnyIndelCoveredInMate()
        {
            CheckR1IndelCoveredInMate("3M2D3M", "3M2I1M1D1M", true);
            CheckR1IndelCoveredInMate("3M2D3M", "3M2I1M", false);
            CheckR1IndelCoveredInMate("3M1I3M", "7M", true);
            CheckR1IndelCoveredInMate("3M1I3M", "3M", false);
            CheckR1IndelCoveredInMate("3M1I3M", "3M2I3M", true);

        }

        private void CheckR1IndelCoveredInMate(string read1Cigar, string read2Cigar, bool expectedResult, int r2Offset = 0)
        {
            var readPair = TestHelpers.GetPair(read1Cigar, read2Cigar, read2Offset: r2Offset);
            var indelPositions = OverlappingIndelHelpers.GetIndelPositions(readPair.Read1, out int totalIndelBasesR1);
            var coveredInMate = OverlappingIndelHelpers.AnyIndelCoveredInMate(indelPositions, readPair.Read2, readPair.Read1, anchorSize:0);
            Assert.Equal(expectedResult, coveredInMate > 0);

        }

        [Fact]
        public void IndelsDisagreeWithStrongMate()
        {
            // Reads strongly disagree: diff indels, no mess
            var readPair = TestHelpers.GetPair("3M2I3M", "4M1I3M", nm: 2, nm2: 1);
            CheckReadsDisagreeTest(readPair, true, "3M2I3M", "4M1I3M");

            // Reads strongly disagree: diff indels, yes mess
            readPair = TestHelpers.GetPair("3M2I3M", "4M1I3M", nm: 2, nm2: 5);
            CheckReadsDisagreeTest(readPair, true, "3M2I3M", "4M1I3M", false);
            // Softclip messier one
            CheckReadsDisagreeTest(readPair, true, "3M2I3M", "5S3M", true);

            // Should disagree - same position, different indels - same mess, don't softclip
            readPair = TestHelpers.GetPair("3M2I3M", "3M1I5M", nm: 2, nm2: 1);
            CheckReadsDisagreeTest(readPair, true, "3M2I3M", "3M1I5M", false);
            CheckReadsDisagreeTest(readPair, true, "3M2I3M", "3M1I5M", true);

            // Should disagree - same position, different indels - r2 messier, softclip that one if configured
            readPair = TestHelpers.GetPair("3M2I3M", "3M1I5M", nm: 2, nm2: 5);
            CheckReadsDisagreeTest(readPair, true, "3M2I3M", "3M1I5M", false);
            CheckReadsDisagreeTest(readPair, true, "3M2I3M", "4S5M", true);

            // Should not disagree - same indels
            readPair = TestHelpers.GetPair("3M2I3M", "3M2I5M", nm: 2, nm2:2);
            CheckReadsDisagreeTest(readPair, false, "3M2I3M", "3M2I5M");

            // Should not disagree - same indels at the overlap points
            readPair = TestHelpers.GetPair("3M2I3M", "3M2I5M1I1M", nm: 2, nm2: 3);
            CheckReadsDisagreeTest(readPair, false, "3M2I3M", "3M2I5M1I1M");

            // Should not disagree - don't overlap at point of indels
            readPair = TestHelpers.GetPair("3M2I3M", "3M", nm: 2, nm2: 2);
            CheckReadsDisagreeTest(readPair, false, "3M2I3M", "3M");

            // Should not disagree - don't overlap at point of indels
            readPair = TestHelpers.GetPair("3M2I3M", "2I3M", nm: 2, nm2: 2, read2Offset: 3);
            CheckReadsDisagreeTest(readPair, false, "3M2I3M", "2I3M");
            readPair = TestHelpers.GetPair("2I3M", "3M2I3M", nm: 2, nm2: 2);
            readPair.Read1.Position += 3;
            CheckReadsDisagreeTest(readPair, false, "2I3M", "3M2I3M");

            // Unanchored insertion that could be part of the insertion in R1 - don't call this a disagreement
            readPair = TestHelpers.GetPair("3M2I3M", "1I3M", nm: 2, nm2: 1, read2Offset: 3);
            CheckReadsDisagreeTest(readPair, false, "3M2I3M", "1I3M");



        }

        private void CheckReadsDisagreeTest(ReadPair readPair, bool shouldDisagree, string expectedCigarR1,
            string expectedCigarR2, bool softclipWeakOne = false)
        {
            var result =
                OverlappingIndelHelpers.IndelsDisagreeWithStrongMate(readPair.Read1, readPair.Read2, out bool disagree, 1, softclipWeakOne: softclipWeakOne);
            Assert.Equal(shouldDisagree, disagree);
            Assert.Equal(expectedCigarR1, result[0].CigarData.ToString());
            Assert.Equal(expectedCigarR2, result[1].CigarData.ToString());

        }

        [Fact]
        public void GetIndelPositions()
        {
            var readPair = TestHelpers.GetPair("3M2D3M", "3M2I1M1D1M");
            var indelPositions = OverlappingIndelHelpers.GetIndelPositions(readPair.Read1, out int totalIndelBasesR1);
            Assert.Equal(1.0, indelPositions.Count);
            Assert.Equal(2.0, totalIndelBasesR1);
            var expectedR1DelStart = readPair.Read1.Position + 3 - 1;
            var expectedR1DelEnd = expectedR1DelStart + 2 + 1;
            Assert.Equal(expectedR1DelStart, indelPositions[0].PreviousMappedPosition);
            Assert.Equal(expectedR1DelEnd, indelPositions[0].NextMappedPosition);

            var indelPositions2 = OverlappingIndelHelpers.GetIndelPositions(readPair.Read2, out int totalIndelBasesR2);
            Assert.Equal(2, indelPositions2.Count);
            Assert.Equal(3, totalIndelBasesR2);
            var expectedR2InsStart = readPair.Read2.Position + 3 - 1;
            var expectedR2InsEnd = expectedR2InsStart + 1;
            var expectedR2DelStart = readPair.Read2.Position + 4 - 1;
            var expectedR2DelEnd = expectedR2DelStart + 1 + 1;
            Assert.Equal(expectedR2InsStart, indelPositions2[0].PreviousMappedPosition);
            Assert.Equal(expectedR2InsEnd, indelPositions2[0].NextMappedPosition);
            Assert.Equal(expectedR2DelStart, indelPositions2[1].PreviousMappedPosition);
            Assert.Equal(expectedR2DelEnd, indelPositions2[1].NextMappedPosition);

        }

    }
}