using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using BamStitchingLogic;
using Gemini.Interfaces;
using Gemini.Logic;
using Gemini.Models;
using Gemini.Realignment;
using Gemini.Stitching;
using Moq;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{
    public class ReadPairRealignerAndCombinerTests
    {
        [Fact]
        public void GetFinalReads()
        {
            var mockIndelFinder = new Mock<IPairSpecificIndelFinder>();
            var mockEvaluator = new Mock<IRealignmentEvaluator>();
            bool realigned = true;
            bool softclipped = false;
            mockEvaluator
                .Setup(x => x.GetFinalAlignment(It.IsAny<BamAlignment>(), out realigned, out softclipped,
                    It.IsAny<List<PreIndel>>())).Returns<BamAlignment, bool, bool, List<PreIndel>>((b, x, y, z) =>
                    {
                        return new BamAlignment(b) {Position = b.IsReverseStrand() ? 10 : b.Position};
                    });
            var mockReadRestitcher = new Mock<IReadRestitcher>();

            var pairRealigner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(),
                mockReadRestitcher.Object,
                mockEvaluator.Object,
                mockIndelFinder.Object, "chr1", false);

            var unpairedMates = TestHelpers.GetPair("5M1I5M", "5M1I5M");
            unpairedMates.PairStatus = PairStatus.SplitQuality;

            // Non-paired
            var reads = pairRealigner.ExtractReads(unpairedMates);
            Assert.Equal(2, reads.Count);
            // Should set realigned position as mate positions
            Assert.Equal(10, reads[0].MatePosition);
            Assert.Equal(99, reads[1].MatePosition);

            // Paired but fail re-stitching
            var pairedMates = TestHelpers.GetPair("5M1I5M", "5M1I5M");

            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool>((p, b1, b2, n1, n2, r) =>
                    new List<BamAlignment>() {b1, b2});
            reads = pairRealigner.ExtractReads(pairedMates);
            Assert.Equal(2, reads.Count);
            // Should set realigned position as mate positions
            Assert.Equal(10, reads[0].MatePosition);
            Assert.Equal(99, reads[1].MatePosition);

            // Paired and succeed re-stitching
            var pairedMatesStitchable = TestHelpers.GetPair("5M1I5M", "5M1I5M");

            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool>((p, b1, b2, n1, n2, r) =>
                    new List<BamAlignment>() { b1});
            reads = pairRealigner.ExtractReads(pairedMatesStitchable);
            Assert.Equal(1.0, reads.Count);
            Assert.Equal(-1, reads[0].MatePosition);
        }
    }
}