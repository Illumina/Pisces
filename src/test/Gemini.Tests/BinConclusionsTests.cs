using Gemini.BinSignalCollection;
using Moq;
using Xunit;

namespace Gemini.Tests
{
    public class BinConclusionsTests
    {
        [Fact]
        public void ProcessRegions()
        {
            var binEvidence = new Mock<IBinEvidence>();
            binEvidence.SetupGet(x => x.NumBins).Returns(1000);

            // Bin 10, 10% mess and 10% indel
            binEvidence.Setup(x => x.GetMessyHit(10)).Returns(10);
            binEvidence.Setup(x => x.GetAllHits(10)).Returns(100);
            binEvidence.Setup(x => x.GetIndelHit(10)).Returns(10);

            // Bin 45, 2% mess and 5% indel - edge positive
            binEvidence.Setup(x => x.GetAllHits(45)).Returns(100);
            binEvidence.Setup(x => x.GetMessyHit(45)).Returns(2);
            binEvidence.Setup(x => x.GetIndelHit(45)).Returns(5);

            // Bin 55, 1% mess and 10% indel - mess edge negative
            binEvidence.Setup(x => x.GetAllHits(55)).Returns(100);
            binEvidence.Setup(x => x.GetMessyHit(55)).Returns(1);
            binEvidence.Setup(x => x.GetIndelHit(55)).Returns(5);

            // Bin 65, 10% mess and 5% indel - indel edge positive
            binEvidence.Setup(x => x.GetAllHits(65)).Returns(100);
            binEvidence.Setup(x => x.GetMessyHit(65)).Returns(10);
            binEvidence.Setup(x => x.GetIndelHit(65)).Returns(5);

            // Bin 75, 10% mess and 4% indel - indel edge negative
            binEvidence.Setup(x => x.GetAllHits(75)).Returns(100);
            binEvidence.Setup(x => x.GetMessyHit(75)).Returns(10);
            binEvidence.Setup(x => x.GetIndelHit(75)).Returns(4);

            // Bin 85, 50% mess and 50% indel, but depth too low
            binEvidence.Setup(x => x.GetAllHits(85)).Returns(8);
            binEvidence.Setup(x => x.GetMessyHit(85)).Returns(4);
            binEvidence.Setup(x => x.GetIndelHit(85)).Returns(4);

            // Bin 95, negative, but neighbor 96 is positive and neighbor bins are therefore turned on
            binEvidence.Setup(x => x.GetAllHits(95)).Returns(8);
            binEvidence.Setup(x => x.GetMessyHit(95)).Returns(0);
            binEvidence.Setup(x => x.GetIndelHit(95)).Returns(0);
            binEvidence.Setup(x => x.GetAllHits(96)).Returns(100);
            binEvidence.Setup(x => x.GetMessyHit(96)).Returns(10);
            binEvidence.Setup(x => x.GetIndelHit(96)).Returns(10);

            // Bin 105, a little mess and it's all reverse
            binEvidence.Setup(x => x.GetAllHits(105)).Returns(100);
            binEvidence.Setup(x => x.GetMessyHit(105)).Returns(5);
            binEvidence.Setup(x => x.GetIndelHit(105)).Returns(0);
            binEvidence.Setup(x => x.GetForwardMessyRegionHit(105)).Returns(0);
            binEvidence.Setup(x => x.GetReverseMessyRegionHit(105)).Returns(5);

            // Bin 115, a little mess and it's mostly forward
            binEvidence.Setup(x => x.GetAllHits(115)).Returns(100);
            binEvidence.Setup(x => x.GetMessyHit(115)).Returns(5);
            binEvidence.Setup(x => x.GetIndelHit(115)).Returns(0);
            binEvidence.Setup(x => x.GetForwardMessyRegionHit(115)).Returns(3);
            binEvidence.Setup(x => x.GetReverseMessyRegionHit(115)).Returns(0);

            // Bin 125, a little mess and it's from a lot of low mapq
            binEvidence.Setup(x => x.GetAllHits(125)).Returns(100);
            binEvidence.Setup(x => x.GetMessyHit(125)).Returns(20);
            binEvidence.Setup(x => x.GetIndelHit(125)).Returns(0);
            binEvidence.Setup(x => x.GetForwardMessyRegionHit(125)).Returns(1);
            binEvidence.Setup(x => x.GetReverseMessyRegionHit(125)).Returns(0);
            binEvidence.Setup(x => x.GetMapqMessyHit(125)).Returns(3);

            // Bin 135, all mess is reverse but there's not enough of if to trigger the filter
            binEvidence.Setup(x => x.GetAllHits(135)).Returns(100);
            binEvidence.Setup(x => x.GetMessyHit(135)).Returns(3);
            binEvidence.Setup(x => x.GetIndelHit(135)).Returns(0);
            binEvidence.Setup(x => x.GetForwardMessyRegionHit(135)).Returns(0);
            binEvidence.Setup(x => x.GetReverseMessyRegionHit(135)).Returns(3);


            var binConclusions = new BinConclusions(binEvidence.Object, true, true, true);
            binConclusions.ProcessRegions(3, 0.07, 10, 0.05, 2, 0.1f);

            // 10 and neighbors - positive
            Assert.True(binConclusions.GetIsMessyEnough(10));
            Assert.True(binConclusions.GetIsMessyEnough(9));
            Assert.True(binConclusions.GetIsMessyEnough(11));

            Assert.True(binConclusions.GetIsMessyEnough(45));
            Assert.True(binConclusions.GetIsMessyEnough(44));
            Assert.True(binConclusions.GetIsMessyEnough(46));

            Assert.False(binConclusions.GetIsMessyEnough(55));
            Assert.False(binConclusions.GetIsMessyEnough(54));
            Assert.False(binConclusions.GetIsMessyEnough(56));

            Assert.True(binConclusions.GetIsMessyEnough(65));
            Assert.True(binConclusions.GetIsMessyEnough(64));
            Assert.True(binConclusions.GetIsMessyEnough(66));

            Assert.False(binConclusions.GetIsMessyEnough(75));
            Assert.False(binConclusions.GetIsMessyEnough(74));
            Assert.False(binConclusions.GetIsMessyEnough(76));

            Assert.False(binConclusions.GetIsMessyEnough(85));
            Assert.False(binConclusions.GetIsMessyEnough(84));
            Assert.False(binConclusions.GetIsMessyEnough(86));

            Assert.False(binConclusions.GetIsMessyEnough(94));
            Assert.True(binConclusions.GetIsMessyEnough(95));
            Assert.True(binConclusions.GetIsMessyEnough(96));
            Assert.True(binConclusions.GetIsMessyEnough(97));

            Assert.True(binConclusions.GetRevMessyStatus(105));
            Assert.False(binConclusions.GetIsMessyEnough(105)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusions.GetFwdMessyStatus(105));
            Assert.False(binConclusions.GetMapqMessyStatus(105));

            Assert.False(binConclusions.GetRevMessyStatus(115));
            Assert.False(binConclusions.GetIsMessyEnough(115)); // doesn't have to be messy to be rev-messy
            Assert.True(binConclusions.GetFwdMessyStatus(115));
            Assert.False(binConclusions.GetMapqMessyStatus(115));

            Assert.False(binConclusions.GetRevMessyStatus(125));
            Assert.False(binConclusions.GetIsMessyEnough(125)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusions.GetFwdMessyStatus(125));
            Assert.True(binConclusions.GetMapqMessyStatus(125));

            // Mess too low to trigger filter
            Assert.False(binConclusions.GetRevMessyStatus(135));
            Assert.False(binConclusions.GetIsMessyEnough(135)); 
            Assert.False(binConclusions.GetFwdMessyStatus(135));
            Assert.False(binConclusions.GetMapqMessyStatus(135));

            //// No mapq mess - same as original, but never should have true for mapq messy
            var binConclusionsNoMapqMess = new BinConclusions(binEvidence.Object, true, true, false);
            binConclusionsNoMapqMess.ProcessRegions(3, 0.07, 10, 0.05, 2, 0.1f);

            // 10 and neighbors - positive
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(10));
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(9));
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(11));

            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(45));
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(44));
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(46));

            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(55));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(54));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(56));

            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(65));
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(64));
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(66));

            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(75));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(74));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(76));

            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(85));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(84));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(86));

            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(94));
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(95));
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(96));
            Assert.True(binConclusionsNoMapqMess.GetIsMessyEnough(97));

            Assert.True(binConclusionsNoMapqMess.GetRevMessyStatus(105));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(105)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusionsNoMapqMess.GetFwdMessyStatus(105));
            Assert.False(binConclusionsNoMapqMess.GetMapqMessyStatus(105));

            Assert.False(binConclusionsNoMapqMess.GetRevMessyStatus(115));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(115)); // doesn't have to be messy to be rev-messy
            Assert.True(binConclusionsNoMapqMess.GetFwdMessyStatus(115));
            Assert.False(binConclusionsNoMapqMess.GetMapqMessyStatus(115));

            Assert.False(binConclusionsNoMapqMess.GetRevMessyStatus(125));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(125)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusionsNoMapqMess.GetFwdMessyStatus(125));
            Assert.False(binConclusionsNoMapqMess.GetMapqMessyStatus(125));

            // Mess too low to trigger filter
            Assert.False(binConclusionsNoMapqMess.GetRevMessyStatus(135));
            Assert.False(binConclusionsNoMapqMess.GetIsMessyEnough(135));
            Assert.False(binConclusionsNoMapqMess.GetFwdMessyStatus(135));
            Assert.False(binConclusionsNoMapqMess.GetMapqMessyStatus(135));

            //// No directional mess - should be same as original except fwd and reverse always false
            var binConclusionsNoDirectional = new BinConclusions(binEvidence.Object, true, false, true);
            binConclusionsNoDirectional.ProcessRegions(3, 0.07, 10, 0.05, 2, 0.1f);

            // 10 and neighbors - positive
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(10));
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(9));
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(11));

            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(45));
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(44));
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(46));

            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(55));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(54));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(56));

            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(65));
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(64));
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(66));

            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(75));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(74));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(76));

            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(85));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(84));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(86));

            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(94));
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(95));
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(96));
            Assert.True(binConclusionsNoDirectional.GetIsMessyEnough(97));

            Assert.False(binConclusionsNoDirectional.GetRevMessyStatus(105));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(105)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusionsNoDirectional.GetFwdMessyStatus(105));
            Assert.False(binConclusionsNoDirectional.GetMapqMessyStatus(105));

            Assert.False(binConclusionsNoDirectional.GetRevMessyStatus(115));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(115)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusionsNoDirectional.GetFwdMessyStatus(115));
            Assert.False(binConclusionsNoDirectional.GetMapqMessyStatus(115));

            Assert.False(binConclusionsNoDirectional.GetRevMessyStatus(125));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(125)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusionsNoDirectional.GetFwdMessyStatus(125));
            Assert.True(binConclusionsNoDirectional.GetMapqMessyStatus(125));

            // Mess too low to trigger filter
            Assert.False(binConclusionsNoDirectional.GetRevMessyStatus(135));
            Assert.False(binConclusionsNoDirectional.GetIsMessyEnough(135));
            Assert.False(binConclusionsNoDirectional.GetFwdMessyStatus(135));
            Assert.False(binConclusionsNoDirectional.GetMapqMessyStatus(135));


            //// No directional or mapq mess - should be same as original except for fwd, rev, and mapq statuses always false
            var binConclusionsNoDirectionalOrMapqMess = new BinConclusions(binEvidence.Object, true, false, false);
            binConclusionsNoDirectionalOrMapqMess.ProcessRegions(3, 0.07, 10, 0.05, 2, 0.1f);

            // 10 and neighbors - positive
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(10));
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(9));
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(11));

            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(45));
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(44));
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(46));

            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(55));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(54));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(56));

            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(65));
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(64));
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(66));

            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(75));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(74));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(76));

            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(85));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(84));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(86));

            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(94));
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(95));
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(96));
            Assert.True(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(97));

            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetRevMessyStatus(105));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(105)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetFwdMessyStatus(105));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetMapqMessyStatus(105));

            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetRevMessyStatus(115));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(115)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetFwdMessyStatus(115));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetMapqMessyStatus(115));

            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetRevMessyStatus(125));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(125)); // doesn't have to be messy to be rev-messy
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetFwdMessyStatus(125));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetMapqMessyStatus(125));

            // Mess too low to trigger filter
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetRevMessyStatus(135));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetIsMessyEnough(135));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetFwdMessyStatus(135));
            Assert.False(binConclusionsNoDirectionalOrMapqMess.GetMapqMessyStatus(135));

        }

        [Fact]
        public void FinalizeConclusions()
        {
            var binConclusions = new Mock<IBinConclusions>();
            binConclusions.SetupGet(x => x.NumBins).Returns(1000);
            binConclusions.Setup(x => x.GetBinId(It.IsAny<int>())).Returns<int>(i=> i / 10);

            MockBinResult(binConclusions, 1, true, true, false);
            MockBinResult(binConclusions, 10, true, true, false);
            MockBinResult(binConclusions, 15, true, false, false);
            MockBinResult(binConclusions, 25, true, true, false);
            MockBinResult(binConclusions, 26, true, true, true);

            var usable = new UsableBins(binConclusions.Object);
            usable.FinalizeConclusions(2);

            VerifyStatusForPositionsInBin(0, 9, usable, true); // Bin 0 - propagate from 1
            VerifyStatusForPositionsInBin(10, 19, usable, true); // Bin 1 - explicitly set
            VerifyStatusForPositionsInBin(20, 29, usable, true); // Bin 2 - propagate from 1
            VerifyStatusForPositionsInBin(30, 39, usable, false); // Bin 3 - outside range of bin 1 propagation
            VerifyStatusForPositionsInBin(40, 89, usable, false); // Bin 4-8 - false
            VerifyStatusForPositionsInBin(90, 119, usable, true); // Bin 9, 10, 11 - propagate from 10
            VerifyStatusForPositionsInBin(120, 239, usable, false); // Bin 12 - 23 - false
            VerifyStatusForPositionsInBin(240, 249, usable, true); // Bin 24 - propagate from 25
            VerifyStatusForPositionsInBin(250, 259, usable, true); // Bin 25 - explicitly set
            VerifyStatusForPositionsInBin(260, 269, usable, false); // Bin 26 - would have propagated from 25, but has likely true snp
            VerifyStatusForPositionsInBin(270, 10000, usable, false); // Everything else - false - not explicitly set

        }

        private void VerifyStatusForPositionsInBin(int minInBin, int maxInBin, UsableBins usable, bool expected)
        {
            for (int i = minInBin; i <= maxInBin; i++)
            {
                VerifyUsableStatus(usable, i, expected);
            }
        }

        private void VerifyUsableStatus(UsableBins usable, int position, bool expected)
        {
            Assert.Equal(expected, usable.IsPositionUsable(position));
        }

        private void MockBinResult(Mock<IBinConclusions> binConclusions, int binNumber, bool isMessyEnough, bool isIndelRegion, bool isProbableTrueSnvRegion)
        {
            binConclusions.Setup(x => x.GetProbableTrueSnvRegion(binNumber)).Returns(isProbableTrueSnvRegion);
            binConclusions.Setup(x => x.GetIsMessyEnough(binNumber)).Returns(isMessyEnough);
            binConclusions.Setup(x => x.GetIndelRegionHit(binNumber)).Returns(isIndelRegion);
        }
    }
}