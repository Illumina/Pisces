using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using BamStitchingLogic;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.Interfaces;
using Gemini.Logic;
using Gemini.Stitching;
using Moq;
using Pisces.Domain.Models;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{
    public class RestitchTests
    {
        [Fact]
        public void Restitch()
        {
            var stitchedPairHandler = new PairHandler(new Dictionary<int, string>(){{1,"chr1"}}, new BasicStitcher(5),new ReadStatusCounter(), tryStitch: true );
            var restitcher = new PostRealignmentStitcher(stitchedPairHandler, new Mock<IStatusHandler>().Object);
            var pair = TestHelpers.GetPair("3M1I4M", "3M1I4M");
            var reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 0, 0, true);
            Assert.Equal(1.0, reads.Count);
            Assert.Equal("3M1I4M", reads.First().CigarData.ToString());

            pair = TestHelpers.GetPair("3M1I4M", "3M5S");
            reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 0, 0, true);
            Assert.Equal(1.0, reads.Count);
            Assert.Equal("3M1I4M", reads.First().CigarData.ToString());

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

            var stitchedPairHandler = new PairHandler(new Dictionary<int, string>() { { 1, "chr1" } }, stitcher, new ReadStatusCounter(), tryStitch: true);
            var restitcher = new PostRealignmentStitcher(stitchedPairHandler, new Mock<IStatusHandler>().Object);

            var pair = new ReadPair(read1.BamAlignment);
            pair.AddAlignment(read2.BamAlignment);

            var reads = restitcher.GetRestitchedReads(pair, pair.Read1, pair.Read2, 1, 1, false);
            Assert.Equal(1.0, reads.Count);
            Assert.Equal("22S78M22S", reads.First().CigarData.ToString());

        }
    }
}