using Alignment.Domain;
using BamStitchingLogic;
using Xunit;

namespace Stitcher.Tests
{
    public class StitchingReadPairEvaluatorTests
    {
        [Fact]
        public void TreatReadPairAsIncomplete()
        {
            var improperPairsEvaluator = new StitchingReadPairEvaluator(true, false, false);

            Assert.True(improperPairsEvaluator.TreatReadPairAsIncomplete(CreateReadPair(false, 1, "3M", 2, "3M")));
            Assert.False(improperPairsEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "3M", 2, "3M")));

            var overlapEvaluator = new StitchingReadPairEvaluator(false, true, false);
            //Assert.True(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "1M2I", 2, "1I2M")));

            //0123456
            // aaa      Overlap
            //  aaa
            Assert.False(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "3M", 2, "3M")));

            //0123456
            // bb       No overlap
            //   bbb
            Assert.True(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "2M", 3, "3M")));

            //0123456
            // cc       No overlap
            //    ccc
            Assert.True(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "2M", 4, "3M")));

            //0123456
            // ddss     Overlap is half anchored
            //   ddd
            //Assert.False(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "2M2S", 3, "3M")));

            //0123456
            // eess     Overlap is only unanchored
            //   sseee
            Assert.True(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "2M2S", 5, "2S3M")));

            //0123456
            // fffs     Overlap is half anchored on both sides
            //   sf
            Assert.False(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "3M1S", 4, "1S1M")));

            //0123456
            //   ggg    Overlap
            // ggg
            Assert.False(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 3, "3M", 1, "3M")));

            //0123456
            // hhhhh    Overlap
            //   hhh
            Assert.False(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "5M", 3, "3M")));

            //0123456
            // iiiii    Overlap
            //  ii
            Assert.False(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "5M", 2, "2M")));

            // PiscesUnitTestScenarios_Insertions_Insertion Inputs_Insertion¬6 
            // "I think leaving unstitched is probably best(treat as non - overlapping, as oppsed to flagging as unstitchable.) we don’t want to throw those reads out for disagreeing when they do not"
            // R1   1 : 3M2I | R2  4 : 2I4M | Stitch Exp  1 : 3M2I4M(3F2S4R) | Stitch Actual: ()
            //012345678
            // mmmii
            //  iimmmm
            Assert.True(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "3M2I", 4, "2I4M")));

            // PiscesUnitTestScenarios_SoftClippedInsertions_SoftClippedInsertion Inputs_SoftclippedInsertion­10 and other semi-unanchored softclip/M overlap scenarios
            // We want to stitch so that Pisces can see that these are unclean refs (if there’s a hidden insertion in that 3S).
            Assert.False(overlapEvaluator.TreatReadPairAsIncomplete(CreateReadPair(true, 1, "1M3S", 2, "5M")));

        }

        private ReadPair CreateReadPair(bool isProperPair, int read1Pos, string read1Cigar, int read2Pos,
            string read2Cigar)
        {
            var read1 = StitcherPairFilterTests.CreateAlignment("ABC", isProperPair, read1Pos, read1Cigar);
            var read2 = StitcherPairFilterTests.CreateAlignment("ABC", isProperPair, read2Pos, read2Cigar);
            var readpair = new ReadPair(read1, readNumber: ReadNumber.Read1);
            readpair.AddAlignment(read2, ReadNumber.Read2);

            return readpair;
        }
    }
}