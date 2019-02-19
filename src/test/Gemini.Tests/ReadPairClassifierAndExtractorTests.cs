using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.Types;
using Moq;
using Xunit;

namespace Gemini.Tests
{
    public class ReadPairClassifierAndExtractorTests
    {
        [Fact]
        public void GetBamAlignmentAndClassification()
        {
            // PerfectStitched
            // - no mismatches, no suspicious cigar ops
            // --> single stitched read
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M"), PairClassification.PerfectStitched, 1, false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1S4M", "5M", nm: 0), PairClassification.PerfectStitched, 1, true);

            // ImperfectStitched
            // - no suspicious cigar ops, 0 < mismatches < messy read threshold
            // --> single stitched read
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm:1), PairClassification.ImperfectStitched, 1, false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1S4M", "5M", nm: 1), PairClassification.ImperfectStitched, 1, true);
            // Same read pair as above, but not trusting softclips - don't stitch
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1S4M", "5M", nm: 1), PairClassification.UnstitchImperfect, 2, false, shouldTryStitch:false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1S4M", "5M", nm: 0), PairClassification.UnstitchImperfect, 2, false, shouldTryStitch: false);

            // MessyStitched
            // - has suspicious cigar ops or mismatches >= messy read threshold
            // --> single stitched read
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 3), PairClassification.MessyStitched, 1, false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "1M1D4M", nm: 3), PairClassification.MessyStitched, 1, false, false);

            // FailStitch
            // - high quality paired reads that fail stitching
            // (here, stage failing)
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 3), PairClassification.FailStitch, 2, false, stageStitchSucceed: false);

            // UnstitchIndel
            //  High - quality paired reads that contain indels and:
            //  - do not overlap OR
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "1M1D4M", read2Offset:10), PairClassification.UnstitchIndel, 2, false, deferStitchIndelReads:false, shouldTryStitch: false);
            //  - do overlap, do not have high - quality disagreements, and the program is configured to defer stitching of indel-containing reads        
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "1M1D4M"), PairClassification.UnstitchIndel, 2, false, shouldTryStitch: false);

            // Disagree
            // High-quality paired reads that contain indels and:
            // - contain high-quality disagreements
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "1M1D4M"), PairClassification.Disagree, 2, false, deferStitchIndelReads: false, shouldTryStitch: false);

        }

        private void VerifyClassificationAndExtraction(ReadPair readpair, PairClassification expectedClassification,
            int expectedNumReads, bool trustSoftclips, bool deferStitchIndelReads = true, bool shouldTryStitch = true, bool stageStitchSucceed = true)
        {
            var pairHandler = new Mock<IReadPairHandler>();
            pairHandler.Setup(x => x.ExtractReads(It.IsAny<ReadPair>())).Returns(stageStitchSucceed
                ? new List<BamAlignment>() {readpair.Read1}
                : new List<BamAlignment>() {readpair.Read1, readpair.Read2});

            var extractor = new ReadPairClassifierAndExtractor(trustSoftclips, deferStitchIndelReads);
            PairClassification actualClassification;
            bool hasIndels;
            int numMismatchesInSingleton;
            bool isSplit;
            var alignments = extractor.GetBamAlignmentsAndClassification(readpair, pairHandler.Object,
                out actualClassification,
                out hasIndels, out numMismatchesInSingleton, out isSplit);

            pairHandler.Verify(x=>x.ExtractReads(It.IsAny<ReadPair>()), Times.Exactly(shouldTryStitch ? 1 : 0));
            Assert.Equal(expectedClassification, actualClassification);
            Assert.Equal(expectedNumReads, alignments.Count);

        }
    }
}