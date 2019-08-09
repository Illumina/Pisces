using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using BamStitchingLogic;
using Gemini.CandidateIndelSelection;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.FromHygea;
using Gemini.IndelCollection;
using Gemini.Infrastructure;
using Gemini.Interfaces;
using Gemini.Logic;
using Gemini.Models;
using Gemini.Realignment;
using Gemini.Stitching;
using Gemini.Types;
using Moq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using ReadRealignmentLogic.Models;
using ReadRealignmentLogic.Utlity;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void NumIndels()
        {
            Assert.Equal(0, Extensions.NumIndels(new CigarAlignment("5M")));
            Assert.Equal(1, Extensions.NumIndels(new CigarAlignment("5M1D5M")));
            Assert.Equal(1, Extensions.NumIndels(new CigarAlignment("5M1I5M")));
            Assert.Equal(1, Extensions.NumIndels(new CigarAlignment("5M2I5M")));
            Assert.Equal(1, Extensions.NumIndels(new CigarAlignment("5M2D5M")));
            Assert.Equal(2, Extensions.NumIndels(new CigarAlignment("5M1D1M1D5M")));
            Assert.Equal(2, Extensions.NumIndels(new CigarAlignment("5M1D1M1I5M")));
            Assert.Equal(2, Extensions.NumIndels(new CigarAlignment("5M1D1M2I5M")));
            Assert.Equal(3, Extensions.NumIndels(new CigarAlignment("5M2D1M1D1M1I5M")));
        }

        [Fact]
        public void NumIndelBases()
        {
            Assert.Equal(0, Extensions.NumIndelBases(new CigarAlignment("5M")));
            Assert.Equal(1, Extensions.NumIndelBases(new CigarAlignment("5M1D5M")));
            Assert.Equal(1, Extensions.NumIndelBases(new CigarAlignment("5M1I5M")));
            Assert.Equal(2, Extensions.NumIndelBases(new CigarAlignment("5M2I5M")));
            Assert.Equal(2, Extensions.NumIndelBases(new CigarAlignment("5M2D5M")));
            Assert.Equal(2, Extensions.NumIndelBases(new CigarAlignment("5M1D1M1D5M")));
            Assert.Equal(2, Extensions.NumIndelBases(new CigarAlignment("5M1D1M1I5M")));
            Assert.Equal(3, Extensions.NumIndelBases(new CigarAlignment("5M1D1M2I5M")));
            Assert.Equal(4, Extensions.NumIndelBases(new CigarAlignment("5M2D1M1D1M1I5M")));
        }
    }

    public class NmCalculatorTests
    {
        [Fact]
        public void GetNm()
        {
            var snippetSource = new Mock<IGenomeSnippetSource>();
            var genomeSnippet = new GenomeSnippet()
            {
                Chromosome = "chr1",
                Sequence = "NNNNNAAAAATTTTTGGGGGCCCCC",
                StartPosition = 94 // 0 based
            };
            snippetSource.Setup(x => x.GetGenomeSnippet(It.IsAny<int>())).Returns(genomeSnippet);
            var nmCalculator = new NmCalculator(snippetSource.Object);


            // Positions passed to CreateBamAlignment are one based bc it adjusts by one in the helper
            var alignment = TestHelpers.CreateBamAlignment("AAAAA", 100, 0, 30, true);
            Assert.Equal(0, nmCalculator.GetNm(alignment));

            alignment = TestHelpers.CreateBamAlignment("AATAA", 100, 0, 30, true);
            Assert.Equal(1, nmCalculator.GetNm(alignment));

            alignment = TestHelpers.CreateBamAlignment("AGTGT", 100, 0, 30, true);
            Assert.Equal(4, nmCalculator.GetNm(alignment));

            alignment = TestHelpers.CreateBamAlignment("AGTGT", 100, 0, 30, true, cigar: new CigarAlignment("1M4I"));
            Assert.Equal(4, nmCalculator.GetNm(alignment));

            alignment = TestHelpers.CreateBamAlignment("ATTTT", 100, 0, 30, true, cigar: new CigarAlignment("1M4D4M"));
            Assert.Equal(4, nmCalculator.GetNm(alignment));

            alignment = TestHelpers.CreateBamAlignment("ACCCC", 100, 0, 30, true, cigar: new CigarAlignment("1M4D4M"));
            Assert.Equal(8, nmCalculator.GetNm(alignment));

            alignment = TestHelpers.CreateBamAlignment("GAAAA", 100, 0, 30, true);
            Assert.Equal(1, nmCalculator.GetNm(alignment));

            alignment = TestHelpers.CreateBamAlignment("AATAA", 100, 0, 30, true, cigar: new CigarAlignment("2M3S"));
            Assert.Equal(0, nmCalculator.GetNm(alignment));


        }
    }

    public class ReadPairRealignerAndCombinerTests
    {
        private PairResult GetPairResult(Alignment.Domain.ReadPair pair)
        {
            return new PairResult() {ReadPair = pair};
        }

        [Fact]
        public void GetFinalReads()
        {
            var mockIndelFinder = new Mock<IPairSpecificIndelFinder>();
            var mockEvaluator = new Mock<IRealignmentEvaluator>();
            var mockNmCalculator = new Mock<INmCalculator>();
            bool realigned = true;
            bool softclipped = false;
            bool confirmed = false;
            bool sketchy = false;
            mockEvaluator
                .Setup(x => x.GetFinalAlignment(It.IsAny<BamAlignment>(), out realigned, out softclipped, out confirmed, out sketchy,
                    It.IsAny<List<PreIndel>>(), It.IsAny<List<PreIndel>>(), It.IsAny<bool>(),
                    It.IsAny<List<HashableIndel>>(), It.IsAny<List<PreIndel>>(), It.IsAny<RealignmentState>()))
                .Returns<BamAlignment, bool, bool, bool, bool, List<PreIndel>, List<PreIndel>, bool, List<HashableIndel>,
                    List<PreIndel>>((b, r, sc, conf, s, i, i2, z, c, mateIndels) =>
                {
                    return new BamAlignment(b) {Position = b.IsReverseStrand() ? 10 : b.Position};
                });
            var hashable = new HashableIndel()
            {
                Chromosome = "chr1",
                ReferencePosition = 123,
                ReferenceAllele = "A",
                AlternateAllele = "AT",
                Type = AlleleCategory.Insertion
            };
            mockEvaluator.Setup(x => x.GetIndelOutcomes()).Returns(new Dictionary<HashableIndel, int[]>()
            {
                {hashable, new int[] {0, 1, 2, 3, 4, 5, 6}}
            });
            var mockReadRestitcher = new Mock<IReadRestitcher>();

            var masterLookup = new Dictionary<string, IndelEvidence>();
            var masterOutcomesLookup = new Dictionary<HashableIndel, int[]>();
            var pairRealigner = new ReadPairRealignerAndCombiner(new SnowballEvidenceCollector(new IndelTargetFinder()),
                mockReadRestitcher.Object,
                mockEvaluator.Object,
                mockIndelFinder.Object, "chr1", false, masterLookup: masterLookup,
                masterOutcomesLookup: masterOutcomesLookup);

            var unpairedMates = GetPairResult(TestHelpers.GetPair("5M1I5M", "5M1I5M"));
            unpairedMates.ReadPair.PairStatus = PairStatus.SplitQuality;

            // Non-paired
            var reads = pairRealigner.ExtractReads(unpairedMates, mockNmCalculator.Object);
            Assert.Equal(2, reads.Count);
            // Should set realigned position as mate positions
            Assert.Equal(10, reads[0].MatePosition);
            Assert.Equal(99, reads[1].MatePosition);

            // Paired but fail re-stitching
            var pairedMates = GetPairResult(TestHelpers.GetPair("5M1I5M", "5M1I5M"));

            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>(),
                    It.IsAny<INmCalculator>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool, INmCalculator, bool, bool>(
                    (p, b1, b2, n1, n2, r, nc, doRecalc, s) =>
                        new List<BamAlignment>() {b1, b2});
            reads = pairRealigner.ExtractReads(pairedMates, mockNmCalculator.Object);
            Assert.Equal(2, reads.Count);
            // Should set realigned position as mate positions
            Assert.Equal(10, reads[0].MatePosition);
            Assert.Equal(99, reads[1].MatePosition);

            // Instructed to silence both reads, but was realigned, so don't silence
            pairedMates = GetPairResult(TestHelpers.GetPair("5M1I5M", "5M1I5M"));
            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>(),
                    It.IsAny<INmCalculator>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool, INmCalculator, bool, bool>(
                    (p, b1, b2, n1, n2, r, nc, doRecalc, s) =>
                        new List<BamAlignment>() {b1, b2});
            reads = pairRealigner.ExtractReads(pairedMates, mockNmCalculator.Object, true, 3);
            Assert.Equal(2, reads.Count);
            Assert.True(reads[0].Qualities.All(x => x == 30));
            Assert.True(reads[1].Qualities.All(x => x == 30));

            // Instructed to silence both reads, but was realigned, so don't silence
            pairedMates = GetPairResult(TestHelpers.GetPair("5M1I5M", "5M1I5M"));
            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>(),
                    It.IsAny<INmCalculator>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool, INmCalculator, bool, bool>(
                    (p, b1, b2, n1, n2, r, nc, doRecalc, s) =>
                        new List<BamAlignment>() {b1, b2});
            reads = pairRealigner.ExtractReads(pairedMates, mockNmCalculator.Object, false, 3);
            Assert.Equal(2, reads.Count);
            Assert.True(reads[0].Qualities.All(x => x == 0));
            Assert.True(reads[1].Qualities.All(x => x == 0));

            // Instructed to silence R1, but was realigned, so don't silence
            pairedMates = GetPairResult(TestHelpers.GetPair("5M1I5M", "5M1I5M"));
            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>(),
                    It.IsAny<INmCalculator>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool, INmCalculator, bool, bool>(
                    (p, b1, b2, n1, n2, r, nc, doRecalc, s) =>
                        new List<BamAlignment>() {b1, b2});
            reads = pairRealigner.ExtractReads(pairedMates, mockNmCalculator.Object, true, 1);
            Assert.Equal(2, reads.Count);
            Assert.True(reads[0].Qualities.All(x => x == 30));
            Assert.True(reads[1].Qualities.All(x => x == 30));

            // Instructed to silence R1, but was realigned, so don't silence
            pairedMates = GetPairResult(TestHelpers.GetPair("5M1I5M", "5M1I5M"));
            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>(),
                    It.IsAny<INmCalculator>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool, INmCalculator, bool, bool>(
                    (p, b1, b2, n1, n2, r, nc, doRecalc, s) =>
                        new List<BamAlignment>() {b1, b2});
            reads = pairRealigner.ExtractReads(pairedMates, mockNmCalculator.Object, false, 1);
            Assert.Equal(2, reads.Count);
            Assert.True(reads[0].Qualities.All(x => x == 0));
            Assert.True(reads[1].Qualities.All(x => x == 30));

            // Instructed to silence R2, but was realigned, so don't silence
            pairedMates = GetPairResult(TestHelpers.GetPair("5M1I5M", "5M1I5M"));
            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>(),
                    It.IsAny<INmCalculator>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool, INmCalculator, bool, bool>(
                    (p, b1, b2, n1, n2, r, nc, doRecalc, s) =>
                        new List<BamAlignment>() {b1, b2});
            reads = pairRealigner.ExtractReads(pairedMates, mockNmCalculator.Object, true, 2);
            Assert.Equal(2, reads.Count);
            Assert.True(reads[0].Qualities.All(x => x == 30));
            Assert.True(reads[1].Qualities.All(x => x == 30));

            // Instructed to silence R2, was not realigned, so silence
            pairedMates = GetPairResult(TestHelpers.GetPair("5M1I5M", "5M1I5M"));
            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>(),
                    It.IsAny<INmCalculator>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool, INmCalculator, bool, bool>(
                    (p, b1, b2, n1, n2, r, nc, doRecalc, s) =>
                        new List<BamAlignment>() {b1, b2});
            reads = pairRealigner.ExtractReads(pairedMates, mockNmCalculator.Object, false, 2);
            Assert.Equal(2, reads.Count);
            Assert.True(reads[0].Qualities.All(x => x == 30));
            Assert.True(reads[1].Qualities.All(x => x == 0));


            // Paired and succeed re-stitching
            var pairedMatesStitchable = GetPairResult(TestHelpers.GetPair("5M1I5M", "5M1I5M"));

            mockReadRestitcher.Setup(x => x.GetRestitchedReads(It.IsAny<ReadPair>(), It.IsAny<BamAlignment>(),
                    It.IsAny<BamAlignment>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>(),
                    It.IsAny<INmCalculator>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns<ReadPair, BamAlignment, BamAlignment, int?, int?, bool, INmCalculator, bool, bool>(
                    (p, b1, b2, n1, n2, r, nc, doRecalc, s) =>
                        new List<BamAlignment>() {b1});
            reads = pairRealigner.ExtractReads(pairedMatesStitchable, mockNmCalculator.Object);
            Assert.Equal(1.0, reads.Count);
            Assert.Equal(-1, reads[0].MatePosition);

            // Master outcomes gets added to at the end
            Assert.Empty(masterOutcomesLookup);
            pairRealigner.Finish();
            Assert.Single(masterOutcomesLookup);
        }


        [Fact]
        public void ExtractReads_Scenarios()
        {
            //ZZXXXXXCAGCAGCAGCAGXYZ            // Ref (4 CAG repeats)
            //ZZXXXXX   CAGCAGCAGCAGXYZ         // Gapped Ref (4 CAG repeats)
            //  XXXXXCAGCAGCAG                  // R1 (3 CAG repeats, but not spanning)
            //    XXXCAGCAGCAGCAGCAGXYZ         // R2 (has insertion - 5 CAG repeats)
            //    XXXCAGCAGCAGCAGCAG            // R3 (5 CAG repeats, not spanning)
            //    XXXCAGCAGCAGCAGCA             // R3' (4.5 CAG repeats, not spanning)
            //    XXXCAGCAGCAGXYZ               // R4 (has deletion - 3 CAG repeats)
            //    XXXCAGCAGCAGCAGXYZ            // R5 (4 repeats and does span)
            //    XXXCAGCAGCAGCAG               // R6 (4 repeats and does NOT span)

            var refSequence = "ZZZZZZZXXXXXCAGCAGCAGCAGXYZ";
            var threeRepeatsNoSpan = "XXXXXCAGCAGCAG";
            var fiveRepeatsSpan = "XXXCAGCAGCAGCAGCAGXYZ";
            var insertionVeryWellAnchored = "ZZZZZZXXXXXCAGCAGCAGCAGCAGXYZTTTT";
            var deletionVeryWellAnchored = "ZZZZZZXXXXXCAGCAGCAGXYZTTTT";
            var fiveRepeatsNoSpan = "XXXCAGCAGCAGCAGCAG";
            var fourAndHalfRepeatsNoSpan = "XXXCAGCAGCAGCAGCA";
            var threeRepeatsSpan = "XXXCAGCAGCAGXYZ";
            var fourRepeatsSpan = "XXXCAGCAGCAGCAGXYZ";
            var fourRepeatsNoSpan = "XXXCAGCAGCAGCAG";
            var threeRepeatsMismatch1bSpan = "XXXXXCAGCAGCAGM";
            var threeRepeatsSpanSingleMismatch = "XXXCAGCAXCAGXYZ";

            List<BamAlignment> result;

            var indels = new List<PreIndel>();
            indels.Add(new PreIndel(new CandidateAllele("chr1", 12, "X", "XCAG", AlleleCategory.Insertion)){Score=1000});

            // Both reads already have insertion
            var pair = GetPairResult(TestHelpers.GetPair("3M3I15M", "3M3I15M", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: fiveRepeatsSpan, read2Bases: fiveRepeatsSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckStitchedRead(result, "3M3I15M");
            Assert.True(pair.R1Confirmed);
            Assert.True(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.False(pair.ReadPair.RealignedR2);

            // Both reads span, show insertion, were softclipped
            pair = GetPairResult(TestHelpers.GetPair("14M7S", "14M7S", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: fiveRepeatsSpan, read2Bases: fiveRepeatsSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, false);
            CheckStitchedRead(result, "3M3I15M");
            Assert.False(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.True(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // One read has insertion and spans, one read shows insertion but doesn't span
            pair = GetPairResult(TestHelpers.GetPair("3M3I15M", "14M4S", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: fiveRepeatsSpan, read2Bases: fiveRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckStitchedRead(result, "3M3I15M");
            Assert.True(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // One read has insertion and spans, one read shows probable insertion but doesn't completely cover it or span
            pair = GetPairResult(TestHelpers.GetPair("3M3I15M", "14M3S", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: fiveRepeatsSpan, read2Bases: fourAndHalfRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckStitchedRead(result, "3M3I15M");
            Assert.True(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // One read has insertion and spans, one read spans and refutes
            pair = GetPairResult(TestHelpers.GetPair("3M3I15M", "18M", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: fiveRepeatsSpan, read2Bases: fourRepeatsSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckNotStitchedRead(result, "3M3I15M", "18M");
            Assert.True(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.False(pair.ReadPair.RealignedR2);

            pair = GetPairResult(TestHelpers.GetPair("11M3I19M", "3M3I15M", read1Position: 1, nm2: 3, read2Offset: 8,
                read1Bases: insertionVeryWellAnchored, read2Bases: fiveRepeatsSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckStitchedRead(result, "11M3I19M");
            Assert.True(pair.R1Confirmed);
            Assert.True(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.False(pair.ReadPair.RealignedR2);

            // One read has very well anchored insertion that gets realigned, one read doesn't refute or show insertion but doesn't span(ref # rpts)
            pair = GetPairResult(TestHelpers.GetPair("11M22S", "15M", read1Position: 1, nm2: 0, read2Offset: 8,
                read1Bases: insertionVeryWellAnchored, read2Bases: fourRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, false);
            CheckStitchedRead(result, "11M3I19M");
            Assert.False(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.True(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // One read has very well anchored insertion, one read doesn't refute or show insertion but doesn't span(ref # rpts)
            pair = GetPairResult(TestHelpers.GetPair("11M3I19M", "15M", read1Position: 1, nm: 3, nm2: 0, read2Offset: 8,
                read1Bases: insertionVeryWellAnchored, read2Bases: fourRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckStitchedRead(result, "11M3I19M");
            Assert.True(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // One read has insertion and spans but was originally softclipped, one read doesn't refute or show insertion but doesn't span
            // Since the R1 indel is not called by the original aligner AND it is not super-strong, we don't allow R2 to realign around it
            pair = GetPairResult(TestHelpers.GetPair("14M7S", "15M", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: fiveRepeatsSpan, read2Bases: fourRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels);
            CheckNotStitchedRead(result, "3M3I15M", "15M");
            Assert.False(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.True(pair.ReadPair.RealignedR1);
            Assert.False(pair.ReadPair.RealignedR2);

            // One read has insertion and spans, one read doesn't refute or show insertion but doesn't span
            // The indel in R1 is considered super-strong by virtue of it being already called by the aligner and being the top indel around, so if it fits in R2, it gets to realign and stitch.
            pair = GetPairResult(TestHelpers.GetPair("3M3I15M", "15M", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: fiveRepeatsSpan, read2Bases: fourRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckStitchedRead(result, "3M3I15M");
            Assert.True(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // Same scenario but not the top indel around: don't force-realign R2, don't stitch
            indels.Add(new PreIndel(new CandidateAllele("chr1", 12, "X", "XCAGCAG", AlleleCategory.Insertion)) { Score = 4000 });
            pair = GetPairResult(TestHelpers.GetPair("3M3I15M", "15M", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: fiveRepeatsSpan, read2Bases: fourRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckNotStitchedRead(result, "3M3I15M", "15M");
            //Assert.False(pair.R1Confirmed); // Nothing convinced us this was amazing, therefore it doesn't count as confirmed
            Assert.True(pair.R1Confirmed); // ^^We did literally confirm it. That's what convinced us.
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.False(pair.ReadPair.RealignedR2);

            // One read has very well anchored insertion but not called, one read doesn't refute or show insertion but doesn't span(ref # rpts)
            // Still force-realign because the original one is so strong and believable
            pair = GetPairResult(TestHelpers.GetPair("11M22S", "15M", read1Position: 1, nm: 3, nm2: 0, read2Offset: 8,
                read1Bases: insertionVeryWellAnchored, read2Bases: fourRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, false);
            CheckStitchedRead(result, "11M3I19M");
            Assert.False(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.True(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // One read has very well anchored insertion, one read doesn't refute or show insertion but doesn't span(ref # rpts)
            // Still force-realign because the original one is so strong and believable
            pair = GetPairResult(TestHelpers.GetPair("11M3I19M", "15M", read1Position: 1, nm: 3, nm2: 0, read2Offset: 8,
                read1Bases: insertionVeryWellAnchored, read2Bases: fourRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckStitchedRead(result, "11M3I19M");
            Assert.True(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // TODO consider the option (configurable) to still force-realign even if something is not "super-strong", since in real life if it had been super well-anchored it would have likely been called to begin with
            // TODO consider not force-realigning if we know we are not going to stitch after (realign only) or undoing the realignment if we fail stitching - this would maybe consist of adding a flag that says whether we took the easy route
            // TODO see if we can come up with any example of repeat/reference-mimicking indel scenarios that can otherwise introduce FPs. So far I haven't thought of anything that would not be handled by simply enforcing that adding an indel without improving the read otherwise is not allowed (unless it's pair-enforced). But maybe there's a multi-indel or softclip scenario we can come up with.

            // Deletions
            indels.Clear();
            indels.Add(new PreIndel(new CandidateAllele("chr1", 12, "XCAG", "X", AlleleCategory.Deletion)){Score = 1000});

            // Both show evidence for the deletion but don't initially have it. Should realign and stitch
            pair = GetPairResult(TestHelpers.GetPair("10M5S", "10M5S", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: threeRepeatsSpan, read2Bases: threeRepeatsSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, false);
            CheckStitchedRead(result, "3M3D12M");
            Assert.False(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.True(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // Both show evidence for the deletion but don't initially have it. One of the reads has a mismatch. Should realign and stitch
            pair = GetPairResult(TestHelpers.GetPair("10M5S", "10M5S", read1Position: 9, nm2: 3, read2Offset: 0,
                read1Bases: threeRepeatsSpan, read2Bases: threeRepeatsSpanSingleMismatch));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, false);
            CheckStitchedRead(result, "3M3D12M");
            Assert.False(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.True(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // Both show evidence for the dleetion, one has very well-anchored evidence and del call, one is softclipped. Should realign the second and stitch.
            pair = GetPairResult(TestHelpers.GetPair("11M3D16M", "10M5S", read1Position: 1, nm2: 3, read2Offset: 8,
                read1Bases: deletionVeryWellAnchored, read2Bases: threeRepeatsSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckStitchedRead(result, "11M3D16M");
            Assert.True(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // One has very well-anchored evidence and del call, one does not span
            pair = GetPairResult(TestHelpers.GetPair("11M3D16M", "14M", read1Position: 1, nm2: 3, read2Offset: 6,
                read1Bases: deletionVeryWellAnchored, read2Bases: threeRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckStitchedRead(result, "11M3D16M");
            Assert.True(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.True(pair.ReadPair.RealignedR2);

            // One has very well-anchored evidence and del call, but other refutes by repeat count without even spanning
            pair = GetPairResult(TestHelpers.GetPair("11M3D16M", "15M", read1Position: 1, nm2: 3, read2Offset: 8,
                read1Bases: deletionVeryWellAnchored, read2Bases: fourRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, true);
            CheckNotStitchedRead(result, "11M3D16M","15M");
            Assert.True(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.False(pair.ReadPair.RealignedR2);

            // Neither spans, neither realigns
            pair = GetPairResult(TestHelpers.GetPair("14M", "14M", read1Position: 7, nm2: 3, read2Offset: 0,
                read1Bases: threeRepeatsNoSpan, read2Bases: threeRepeatsNoSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, false);
            CheckStitchedRead(result, "14M");
            Assert.False(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.False(pair.ReadPair.RealignedR2);

            // Neither spans, neither realigns
            pair = GetPairResult(TestHelpers.GetPair("1M13S", "1M14S", read1Position: 7, nm2: 3, read2Offset: 0,
                read1Bases: threeRepeatsNoSpan, read2Bases: threeRepeatsMismatch1bSpan));
            result = ExtractReadsFromRealignerAndCombiner(pair, refSequence, 0, indels, false);
            CheckStitchedRead(result, "1M14S");
            Assert.False(pair.R1Confirmed);
            Assert.False(pair.R2Confirmed);
            Assert.False(pair.ReadPair.RealignedR1);
            Assert.False(pair.ReadPair.RealignedR2);

        }

        private static void CheckNotStitchedRead(List<BamAlignment> result, string expectedCigar1, string expectedCigar2)
        {
            Assert.Equal(2, result.Count);
            Assert.Equal(expectedCigar1, result[0].CigarData.ToString());
            Assert.Equal(expectedCigar2, result[1].CigarData.ToString());
        }

        private static void CheckStitchedRead(List<BamAlignment> result, string expectedCigar)
        {
            Assert.Single(result);
            Assert.Equal(expectedCigar, result[0].CigarData.ToString());
        }


        private static List<BamAlignment> ExtractReadsFromRealignerAndCombiner(PairResult pair, string refSeq,
            int refSeqOffset, List<PreIndel> preIndels, bool hasExistingIndels = false)
        {
            var stitchedPairHandler =
                new PairHandler(new Dictionary<int, string>() {{1, "chr1"}}, new BasicStitcher(0), tryStitch: true);

            var snippetSource = new Mock<IGenomeSnippetSource>();
            var genomeSnippet = new GenomeSnippet()
            {
                Chromosome = "chr1",
                Sequence = new string('A', refSeqOffset) + refSeq + new string('T', 1000),
                StartPosition = 0
            };
            snippetSource.Setup(x => x.GetGenomeSnippet(It.IsAny<int>())).Returns(genomeSnippet);
            var mockStatusHandler = new Mock<IStatusHandler>();
            var comparer = new GemBasicAlignmentComparer(false, false);

            var readRealigner = new GeminiReadRealigner(comparer, remaskSoftclips: false,
                keepProbeSoftclips: false, keepBothSideSoftclips: false,
                trackActualMismatches: false, checkSoftclipsForMismatches: true,
                debug: false, maskNsOnly: false, maskPartialInsertion: false,
                minimumUnanchoredInsertionLength: 1,
                minInsertionSizeToAllowMismatchingBases: 4,
                maxProportionInsertSequenceMismatch: 0.2); // TODO fix // TODO figure out what I was saying to fix here...

            var filterer = new Mock<IRegionFilterer>();
            filterer.Setup(x => x.AnyIndelsNearby(It.IsAny<int>())).Returns(true);

            var indels = preIndels.Select(x => HashableIndelSource.GetHashableIndel(genomeSnippet, x, 0, false)).ToList();
            var indelSource = new ChromosomeIndelSource(indels, snippetSource.Object);
            var realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);

            var combiner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(),
                new PostRealignmentStitcher(stitchedPairHandler, new DebugStatusHandler(new ReadStatusCounter())),
                realignmentEvaluator, new PairSpecificIndelFinder(), "chr1", false, hasExistingIndels: hasExistingIndels);
            var nmCalc = new NmCalculator(snippetSource.Object);

            var result = combiner.ExtractReads(pair, nmCalc);
            return result;
        }
    }
}