using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using BamStitchingLogic;
using Gemini.Interfaces;
using Gemini.Logic;
using Gemini.Stitching;
using Moq;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{
    public class RestitchTests
    {
        [Fact]
        public void Restitch()
        {
            var stitchedPairHandler = new PairHandler(new Dictionary<int, string>(){{1,"chr1"}}, new BasicStitcher(5), tryStitch: true );
            var restitcher = new PostRealignmentStitcher(stitchedPairHandler, new Mock<IStatusHandler>().Object);
            var mockNmCalculator = new Mock<INmCalculator>();
            var pair = TestHelpers.GetPair("3M1I4M", "3M1I4M");
            var reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 0, 0, true, mockNmCalculator.Object, false);
            Assert.Equal(1.0, reads.Count);
            Assert.Equal("3M1I4M", reads.First().CigarData.ToString());

            pair = TestHelpers.GetPair("3M1I4M", "3M5S");
            reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 0, 0, true, mockNmCalculator.Object, false);
            Assert.Equal(1.0, reads.Count);
            Assert.Equal("3M1I4M", reads.First().CigarData.ToString());

            pair = TestHelpers.GetPair("3M2I4M", "3M1I");
            reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 0, 0, true, mockNmCalculator.Object, false);
            Assert.Equal(1, reads.Count);
            Assert.Equal("3M2I4M", reads.First().CigarData.ToString());

            // Second read insertion is longer, can't stitch
            pair = TestHelpers.GetPair("3M2I4M", "3M3I");
            reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 0, 0, true, mockNmCalculator.Object, false);
            Assert.Equal(2, reads.Count);
            Assert.Equal("3M2I4M", reads[0].CigarData.ToString());
            Assert.Equal("3M3I", reads[1].CigarData.ToString());


            // Same insertion introduced. One read had NM=4 before (included insertion), other did not. Stitched NM is 4 (3 from the insertion, 1 from the mismatch in the original read)
            pair = TestHelpers.GetPair("3M3I4M", "3M3I6M");
            mockNmCalculator.Setup(x => x.GetNm(It.IsAny<BamAlignment>())).Returns(4);
            reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 0, 4, true, mockNmCalculator.Object, true);
            Assert.Equal(1, reads.Count);
            Assert.Equal("3M3I6M", reads[0].CigarData.ToString());

            // Same insertion introduced. One read had NM=4 before (included insertion), other did not (was perfect).
            // Stitched NM is 5 (3 from the insertion, 1 from the mismatch in the original read, and, say, 1 from a mismatch introduced by realignment in the first read)
            pair = TestHelpers.GetPair("3M3I4M", "3M3I6M");
            mockNmCalculator.Setup(x => x.GetNm(It.IsAny<BamAlignment>())).Returns(5);
            reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 0, 4, true, mockNmCalculator.Object, true);
            Assert.Equal(2, reads.Count);


            // Partial insertion, from right
            pair = TestHelpers.GetPair("3M2I4M", "1I4M", read2Offset:3);
            reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 0, 0, true, mockNmCalculator.Object, false);
            Assert.Equal(1, reads.Count);
            Assert.Equal("3M2I4M", reads.First().CigarData.ToString());

        }

        [Fact]
        public void TryReStitch_RealCases()
        {
            var read1 = TestHelpers.CreateRead("chr1", "AGCAGCAGCAGCTCCAGCACCAGCAGTCCCAGCACCAGCAGGCCCCGAAGAAGCATACCCAGCAGCAGAAGACACCTCAGCAGCTGCACCAGGTGATCGG", 14106298,
                new CigarAlignment("41M59S"));

            var read2 = TestHelpers.CreateRead("chr1", "GCGATCTATCAGTATTAGCTCCAGCATCAGCAGCCCGAGCATCTGCAGTTCTAGCAGCAGCAGTCCCAGCAGCAGCAGTCCCAGCAGCAGCTGCCCCAGT", 14106328,
                new CigarAlignment("52S48M"));

            var stitcher = new BasicStitcher(20, false,
                true, debug:false, nifyUnstitchablePairs: true, ignoreProbeSoftclips: true, maxReadLength: 1024, ignoreReadsAboveMaxLength: false, thresholdNumDisagreeingBases:1000);

            var stitchedPairHandler = new PairHandler(new Dictionary<int, string>() { { 1, "chr1" } }, stitcher, tryStitch: true);
            var restitcher = new PostRealignmentStitcher(stitchedPairHandler, new Mock<IStatusHandler>().Object);

            var pair = new ReadPair(read1.BamAlignment);
            pair.AddAlignment(read2.BamAlignment);

            var mockNmCalculator = new Mock<INmCalculator>();
            var reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 1, 1, false, mockNmCalculator.Object, false);
            Assert.Equal(1.0, reads.Count);
            Assert.Equal("22S78M22S", reads.First().CigarData.ToString());

        }
    }
}