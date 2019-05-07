using System.Collections.Generic;
using System.Linq;
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
            // --> unstitch single. Would have been able to stitch, but we can't guarantee the NM in that case
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm:1), PairClassification.UnstitchSingleMismatch, 2, true, shouldTryStitch: false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1S4M", "5M", nm: 1), PairClassification.UnstitchSingleMismatch, 2, true, shouldTryStitch: false);
            // Same read pair as above, but not trusting softclips - don't stitch
            //VerifyClassificationAndExtraction(TestHelpers.GetPair("1S4M", "5M", nm: 1), PairClassification.UnstitchImperfect, 2, true, shouldTryStitch: false);
            // Softclip and mismatch on the same read
            // Large softclip
            VerifyClassificationAndExtraction(TestHelpers.GetPair("8S4M", "5M", nm: 0, nm2: 3), PairClassification.UnstitchMessy, 2, false, shouldTryStitch:false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("7S4M", "5M", nm: 1, nm2: 3), PairClassification.UnstitchMessy, 2, false, shouldTryStitch: false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("7S4M", "5M", nm: 2, nm2: 3), PairClassification.UnstitchMessy, 2, false, shouldTryStitch: false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1S4M", "5M", nm: 1, nm2: 3), PairClassification.UnstitchMessy, 2, false, shouldTryStitch: false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("7S4M", "5M", nm: 0), PairClassification.UnstitchImperfect, 2, false, shouldTryStitch: false);

            // MessyStitched
            // - has suspicious cigar ops or mismatches >= messy read threshold
            // --> unstitch messy. Would have been able to stitch, but we can't guarantee the NM in that case
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 3), PairClassification.UnstitchMessy, 2, false, shouldTryStitch: false);

            // TODO Should we categorize messy indel reads separately from UnstitchIndel? We still do use the NM for reputability. Previously this was MessyStitched.
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "1M1D4M", nm: 3), PairClassification.UnstitchIndel, 2, false, shouldTryStitch: false);

            // FailStitch
            // - high quality paired reads that fail stitching
            // (here, stage failing)
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 0), PairClassification.FailStitch, 2, false, stageStitchSucceed: false);

            // UnstitchIndel
            //  High - quality paired reads that contain indels and:
            //  - do not overlap OR
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "1M1D4M", read2Offset:10), PairClassification.UnstitchIndel, 2, false, deferStitchIndelReads:false, shouldTryStitch: false);
            //  - do overlap, do not have high - quality disagreements, and the program is configured to defer stitching of indel-containing reads        
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "1M1D4M"), PairClassification.UnstitchIndel, 2, false, shouldTryStitch: false);
            // - same as above, but without NM set (test helper will leave NM unset in staged read if passed NM <0). This previously caused an exception (see PICS-1182). Now we will allow it and log warning. 
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "1M1D4M", nm: -1), PairClassification.UnstitchIndel, 2, false, shouldTryStitch: false);


            // Disagree
            // High-quality paired reads that contain indels and:
            // - contain high-quality disagreements
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "1M1D4M"), PairClassification.Disagree, 2, false, deferStitchIndelReads: false, shouldTryStitch: false);


            // UnstitchForwardMessy
            // Read1 has high mess and r2 is clean, r1 is forward
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 5, nm2: 0), PairClassification.UnstitchForwardMessy, 
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);
            // Read1 has high mess and r2 has a little mess
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 5, nm2: 1), PairClassification.UnstitchForwardMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);
            // Read1 has high mess and r2 has a little but not high - plain old messy
            // --> unstitch messy. Would have been able to stitch, but we can't guarantee the NM in that case
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 5, nm2: 2), PairClassification.UnstitchMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // Read2 has high mess and r1 is clean, r2 is forward
            var pair = TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 5);
            pair.Read1.SetIsReverseStrand(true);
            pair.Read1.SetIsMateReverseStrand(false);
            pair.Read2.SetIsReverseStrand(false);
            pair.Read2.SetIsMateReverseStrand(true);
            VerifyClassificationAndExtraction(pair, PairClassification.UnstitchForwardMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);


            // UnstitchForwardMessyIndel
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "5M", nm: 5, nm2: 0), PairClassification.UnstitchForwardMessyIndel,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "1M1D4M", nm: 5, nm2: 0), PairClassification.UnstitchForwardMessyIndel,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1S1M1D4M", "5M", nm: 5, nm2: 0, read2Offset: 5), PairClassification.UnstitchForwardMessyIndel,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "1S1M1D4M", nm: 5, nm2: 1, read2Offset: 5), PairClassification.UnstitchForwardMessyIndel,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // UnstitchReverseMessy
            // Read2 has high mess and r1 is clean, r1 is forward
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 5), PairClassification.UnstitchReverseMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);
            // Read2 has high mess and r1 has a little mess
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 1, nm2: 5), PairClassification.UnstitchReverseMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);
            // Read2 has high mess and r1 has a little but not high - plain old messy
            // --> unstitch single. Would have been able to stitch, but we can't guarantee the NM in that case
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 2, nm2: 4), PairClassification.UnstitchMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // Read1 has high mess and r2 is clean, r2 is forward
            pair = TestHelpers.GetPair("5M", "5M", nm: 5, nm2: 0);
            pair.Read1.SetIsReverseStrand(true);
            pair.Read1.SetIsMateReverseStrand(false);
            pair.Read2.SetIsReverseStrand(false);
            pair.Read2.SetIsMateReverseStrand(true);
            VerifyClassificationAndExtraction(pair, PairClassification.UnstitchReverseMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // UnstitchReverseMessyIndel
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "5M", nm: 0, nm2: 5), PairClassification.UnstitchReverseMessyIndel,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "1M1D4M", nm: 0, nm2: 5), PairClassification.UnstitchReverseMessyIndel,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // UnstitchMessySuspiciousRead
            // Both have low mapq, reverse only has high mismatches -> UnstitchReverseMessy
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 5, mapq1: 20, mapq2: 20), PairClassification.UnstitchReverseMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // Both have low mapq, reverse has high mismatches and forward has a couple
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 2, nm2: 5, mapq1: 20, mapq2: 20), PairClassification.UnstitchMessySuspiciousRead,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // Both have low mapq, both have high mismatches - suspicious
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 4, nm2: 5, mapq1: 20, mapq2: 20), PairClassification.UnstitchMessySuspiciousRead,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // One has low mapq, same has high mismatches - mess trumps mapq, call it directional mess (TODO add spec, and determine if we want something different)
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 5, mapq1: 60, mapq2: 20), PairClassification.UnstitchReverseMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // One has low mapq, other has high mismatches - mess trumps mapq, call it directional mess (TODO add spec, and determine if we want something different)
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 5, mapq1: 20, mapq2: 60), PairClassification.UnstitchReverseMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // One has low mapq, both have high mismatches - treat as both directions are suspicious
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 4, nm2: 5, mapq1: 20, mapq2: 60), PairClassification.UnstitchMessySuspiciousRead,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // Both have low mapq, neither has high mismatches - not suspicious
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 0, mapq1: 20, mapq2: 20), PairClassification.PerfectStitched,
                1, false, deferStitchIndelReads: false, shouldTryStitch: true);

            // Both reads low quality - unusable
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 0, mapq1: 5, mapq2: 5), PairClassification.Unusable,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // One read very low quality, no indels - Split
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 0, mapq1: 5, mapq2: 65), PairClassification.Split,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);
            
            // One read very low quality, has indels - Split
            VerifyClassificationAndExtraction(TestHelpers.GetPair("1M1D4M", "5M", nm: 0, nm2: 0, mapq1: 5, mapq2: 65), PairClassification.Split,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // Duplicate 
            VerifyClassificationAndExtraction(TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 0, mapq1: 5, mapq2: 65, pairStatus: PairStatus.Duplicate), PairClassification.Duplicate,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // Improper
            var improperPair = TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 0, pairStatus: PairStatus.Unknown);
            improperPair.Read2 = null;
            improperPair.IsImproper = true;
            VerifyClassificationAndExtraction(improperPair, PairClassification.Improper,
                1, false, deferStitchIndelReads: false, shouldTryStitch: false);

            improperPair = TestHelpers.GetPair("1M1D4M", "5M", nm: 0, nm2: 0, pairStatus: PairStatus.Unknown);
            improperPair.Read2 = null;
            improperPair.IsImproper = true;
            VerifyClassificationAndExtraction(improperPair, PairClassification.IndelImproper,
                1, false, deferStitchIndelReads: false, shouldTryStitch: false);

            var improperPair2 = TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 0, pairStatus: PairStatus.Unknown);
            improperPair2.Read2 = null;
            improperPair2.NormalPairOrientation = false;
            VerifyClassificationAndExtraction(improperPair2, PairClassification.Improper,
                1, false, deferStitchIndelReads: false, shouldTryStitch: false, treatAbnormalOrientationAsImproper: true);
            VerifyClassificationAndExtraction(improperPair2, PairClassification.Unstitchable,
                1, false, deferStitchIndelReads: false, shouldTryStitch: false, treatAbnormalOrientationAsImproper: false);

            // Improper long fragment
            improperPair = TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 0, pairStatus: PairStatus.LongFragment);
            VerifyClassificationAndExtraction(improperPair, PairClassification.LongFragment,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // Improper single
            improperPair = TestHelpers.GetPair("5M", "5M", nm: 0, nm2: 0, pairStatus: PairStatus.LongFragment);
            improperPair.Read2 = null;
            improperPair.IsImproper = false;
            improperPair.NumPrimaryReads = 1;
            VerifyClassificationAndExtraction(improperPair, PairClassification.UnstitchableAsSingleton,
                1, false, deferStitchIndelReads: false, shouldTryStitch: false);

            improperPair = TestHelpers.GetPair("1M1D4M", "5M", nm: 0, nm2: 0, pairStatus: PairStatus.LongFragment);
            improperPair.Read2 = null;
            improperPair.IsImproper = false;
            improperPair.NumPrimaryReads = 1;
            VerifyClassificationAndExtraction(improperPair, PairClassification.IndelSingleton,
                1, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // MD shows read 1 has a bunch of mismatches that follow a pattern and are different than r2
            var messy = TestHelpers.GetPair("15M", "15M", nm: 5, read1Bases: "AAAAAAAAAAAAAAA", read2Bases: "AAAAATTTTTAAAAA");
            messy.Read1.ReplaceOrAddStringTag("MD", "5T0T0T0T0T5");
            messy.Read2.ReplaceOrAddStringTag("MD", "15");
            VerifyClassificationAndExtraction(messy, PairClassification.UnstitchMessySuspiciousMd,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false, checkMd: true);

            // Not configured to check MD
            VerifyClassificationAndExtraction(messy, PairClassification.UnstitchMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false);

            // Both messy in same way
            messy = TestHelpers.GetPair("15M", "15M", nm:5, nm2: 3, read1Bases: "AAAAAAAAAAAAAAA", read2Bases: "AAAAAAAATTAAAAA");
            messy.Read1.ReplaceOrAddStringTag("MD", "5T0T0T0T0T5");
            messy.Read2.ReplaceOrAddStringTag("MD", "5T0T0T7");
            VerifyClassificationAndExtraction(messy, PairClassification.UnstitchMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false, checkMd: true);

            // Both messy in different ways - same bases changed, but changed to diff things
            messy = TestHelpers.GetPair("15M", "15M", nm: 5, nm2: 5, read1Bases: "AAAAAGGGGGAAAAA", read2Bases: "AAAAACCCCCAAAAA");
            messy.Read1.ReplaceOrAddStringTag("MD", "5T0T0T0T0T5");
            messy.Read2.ReplaceOrAddStringTag("MD", "5T0T0T0T0T5");
            VerifyClassificationAndExtraction(messy, PairClassification.UnstitchMessySuspiciousMd,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false, checkMd: true);

            // Both messy in different ways - different bases changed
            messy = TestHelpers.GetPair("15M", "15M", nm: 5, nm2: 5, read1Bases: "AAAAATTTTTGGGGG", read2Bases: "AAAAAGGGGGAAAAA");
            messy.Read1.ReplaceOrAddStringTag("MD", "5T0T0T0T0T5");
            messy.Read2.ReplaceOrAddStringTag("MD", "10A0A0A0A0A");
            VerifyClassificationAndExtraction(messy, PairClassification.UnstitchMessySuspiciousMd,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false, checkMd: true);

            // Checking for MD but don't have any - can't flag as suspicious
            messy = TestHelpers.GetPair("15M", "15M", nm: 5, nm2: 5, read1Bases: "AAAAATTTTTGGGGG", read2Bases: "AAAAAGGGGGAAAAA");
            VerifyClassificationAndExtraction(messy, PairClassification.UnstitchMessy,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false, checkMd: true);

            // Total MD mismatches for one is really high
            messy = TestHelpers.GetPair("15M", "15M", nm: 5, nm2: 3, read1Bases: "AAAAAAAAAAAAAAA", read2Bases: "AAAAAAAATTGGGAA");
            messy.Read1.ReplaceOrAddStringTag("MD", "5T0T0T0T0T0G0G0G2");
            messy.Read2.ReplaceOrAddStringTag("MD", "5T0T0T7");
            VerifyClassificationAndExtraction(messy, PairClassification.UnstitchMessySuspiciousMd,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false, checkMd: true);

            // num Ns - NM only counts non-Ns. If there are a bunch of Ns
            messy = TestHelpers.GetPair("15M", "15M", nm: 3, nm2:3, read1Bases: "AAAAANNNNTTTTAA", read2Bases: "AAAAANNNNTTTTAA");
            messy.Read1.ReplaceOrAddStringTag("MD", "5T0T0T0T1A0A0A2");
            messy.Read2.ReplaceOrAddStringTag("MD", "5T0T0T0T1A0A0A2");
            VerifyClassificationAndExtraction(messy, PairClassification.UnstitchMessySuspiciousMd,
                2, false, deferStitchIndelReads: false, shouldTryStitch: false, checkMd: true);

        }

        private void VerifyClassificationAndExtraction(ReadPair readpair, PairClassification expectedClassification,
            int expectedNumReads, bool trustSoftclips, bool deferStitchIndelReads = true, bool shouldTryStitch = true, bool stageStitchSucceed = true, bool treatAbnormalOrientationAsImproper = false,
            int messyMapq = 30, bool checkMd = false)
        {
            var pairHandler = new Mock<IReadPairHandler>();
            pairHandler.Setup(x => x.ExtractReads(It.IsAny<ReadPair>())).Returns(stageStitchSucceed
                ? new List<BamAlignment>() {readpair.Read1}
                : new List<BamAlignment>() {readpair.Read1, readpair.Read2});

            var extractor = new ReadPairClassifierAndExtractor(trustSoftclips,
                messyMapq: messyMapq, treatAbnormalOrientationAsImproper: treatAbnormalOrientationAsImproper, checkMd: checkMd);

            var result = extractor.GetBamAlignmentsAndClassification(readpair, pairHandler.Object);

            var alignments = result.Alignments;

            Assert.Equal(expectedClassification, result.Classification);
            pairHandler.Verify(x => x.ExtractReads(It.IsAny<ReadPair>()), Times.Exactly(shouldTryStitch ? 1 : 0));

            Assert.Equal(expectedNumReads, alignments.ToList().Count);

        }
    }
}