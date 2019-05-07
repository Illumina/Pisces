using System;
using System.Collections.Generic;
using Alignment.Domain;
using Gemini.BinSignalCollection;
using Gemini.ClassificationAndEvidenceCollection;
using Xunit;

namespace Gemini.Tests
{
    public class BinEvidenceHelperTests
    {
        [Fact]
        public void GetBinId()
        {
            var binEvidence = new BinEvidence(1, true, 5000, false, 500, 123400);

            Assert.Equal(0, binEvidence.GetBinId(123400));
            Assert.Equal(0, binEvidence.GetBinId(123405));
            Assert.Equal(0, binEvidence.GetBinId(123899));
            Assert.Equal(1, binEvidence.GetBinId(123900));
            Assert.Equal(1, binEvidence.GetBinId(123905));

            // Position is out of range of the bins
            // Still return the theoretical bin id, because it's handled elsewhere
            Assert.Equal(5000, binEvidence.GetBinId(123400 + (500 * 5000) + 100));
            Assert.Equal(5001, binEvidence.GetBinId(123400 + (500 * 5000) + 600));
        }

        [Fact]
        public void AddMessEvidence()
        {
            var read1 = TestHelpers.CreateBamAlignment("ATCGATCG", 123405, 123505, 30, true);
            var read2 = TestHelpers.CreateBamAlignment("ATCGATCG", 123505, 123405, 30, true);

            var pair = new ReadPair(read1);
            pair.AddAlignment(read2);
            var pairResult = new PairResult(pair.GetAlignments(), pair);


            var numBins = 5000;
            var messyHitNonZeroes = new Dictionary<int, int>();
            var indelHitNonZeroes = new Dictionary<int, int>();
            var forwardMessNonZeroes = new Dictionary<int, int>();
            var reverseMessNonZeroes = new Dictionary<int, int>();
            var mapqMessNonZeroes = new Dictionary<int, int>();

            var forwardMessNonZeroesNotUsed = new Dictionary<int, int>();
            var reverseMessNonZeroesNotUsed = new Dictionary<int, int>();
            var mapqMessNonZeroesNotUsed = new Dictionary<int, int>();

            var singleMismatchNonZeroes = new Dictionary<int, int>();
            var allHitsNonZeroes = new Dictionary<int, int>();
            var binEvidence = new BinEvidence(1, true, numBins, false, 500, 123000, true, true);
            var binEvidenceNoMapqMess = new BinEvidence(1, true, numBins, false, 500, 123000, true, false);
            var binEvidenceNoDirectional = new BinEvidence(1, true, numBins, false, 500, 123000, false, true);
            var binEvidenceNoDirectionalOrMapqMess = new BinEvidence(1, true, numBins, false, 500, 123000, false, false);

            // Should add one piece of evidence for each alignment
            // Read1 should be in bin 0, read2 in bin 1
            // First, only messy
            binEvidence.AddMessEvidence(true, pairResult, false, false, false, false, false);
            allHitsNonZeroes[0] = 1;
            allHitsNonZeroes[1] = 1;
            messyHitNonZeroes[0] = 1;
            messyHitNonZeroes[1] = 1;
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);

            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(true, pairResult, false, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(true, pairResult, false, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(true, pairResult, false, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

            // Add indel and mess evidence
            binEvidence.AddMessEvidence(true, pairResult, true, false, false, false, false);
            allHitsNonZeroes[0] = 2;
            allHitsNonZeroes[1] = 2;
            messyHitNonZeroes[0] = 2;
            messyHitNonZeroes[1] = 2;
            indelHitNonZeroes[0] = 1;
            indelHitNonZeroes[1] = 1;
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);

            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(true, pairResult, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(true, pairResult, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(true, pairResult, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

            // Add forward mess (must also be called as mess - TODO perhaps change this)
            binEvidence.AddMessEvidence(true, pairResult, false, false, true, false, false);
            allHitsNonZeroes[0] = 3;
            allHitsNonZeroes[1] = 3;
            messyHitNonZeroes[0] = 3;
            messyHitNonZeroes[1] = 3;
            forwardMessNonZeroes[0] = 1;
            forwardMessNonZeroes[1] = 1;
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);

            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(true, pairResult, false, false, true, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(true, pairResult, false, false, true, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(true, pairResult, false, false, true, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

            // Add reverse mess (must also be called as mess - TODO perhaps change this)
            binEvidence.AddMessEvidence(true, pairResult, false, false, false, true, false);
            allHitsNonZeroes[0] = 4;
            allHitsNonZeroes[1] = 4;
            messyHitNonZeroes[0] = 4;
            messyHitNonZeroes[1] = 4;
            reverseMessNonZeroes[0] = 1;
            reverseMessNonZeroes[1] = 1;
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);

            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(true, pairResult, false, false, false, true, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(true, pairResult, false, false, false, true, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(true, pairResult, false, false, false, true, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

            // Add mapq mess (must also be called as mess - TODO perhaps change this)
            binEvidence.AddMessEvidence(true, pairResult, false, false, false, false, true);
            allHitsNonZeroes[0] = 5;
            allHitsNonZeroes[1] = 5;
            messyHitNonZeroes[0] = 5;
            messyHitNonZeroes[1] = 5;
            mapqMessNonZeroes[0] = 1;
            mapqMessNonZeroes[1] = 1;
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);

            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(true, pairResult, false, false, false, false, true);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(true, pairResult, false, false, false, false, true);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(true, pairResult, false, false, false, false, true);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

            // Add indel only
            binEvidence.AddMessEvidence(false, pairResult, true, false, false, false, false);
            allHitsNonZeroes[0] = 6;
            allHitsNonZeroes[1] = 6;
            indelHitNonZeroes[0] = 2;
            indelHitNonZeroes[1] = 2;
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);

            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(false, pairResult, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(false, pairResult, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(false, pairResult, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

            var read1_pair2 = TestHelpers.CreateBamAlignment("ATCGATCG", 125005, 126005, 30, true);
            var read2_pair2 = TestHelpers.CreateBamAlignment("ATCGATCG", 126005, 125005, 30, true);

            var pair2 = new ReadPair(read1_pair2);
            pair2.AddAlignment(read2_pair2);
            var pairResult2 = new PairResult(pair2.GetAlignments(), pair2);

            // Add at different region
            binEvidence.AddMessEvidence(false, pairResult2, true, false, false, false, false);
            allHitsNonZeroes[4] = 1;
            allHitsNonZeroes[6] = 1;
            indelHitNonZeroes[4] = 1;
            indelHitNonZeroes[6] = 1;
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);

            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(false, pairResult2, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(false, pairResult2, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(false, pairResult2, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

            // Test on diff chroms - mate on diff chrom shouldn't contribute
            var read1_pair3 = TestHelpers.CreateBamAlignment("ATCGATCG", 125005, 126005, 30, true);
            var read2_pair3 = TestHelpers.CreateBamAlignment("ATCGATCG", 126005, 125005, 30, true);
            read2_pair3.RefID = 2;
            read1_pair3.MateRefID = 2;

            var pairSplitChrom = new ReadPair(read1_pair3);
            pairSplitChrom.AddAlignment(read2_pair3);
            var pairResultSplitChrom = new PairResult(pairSplitChrom.GetAlignments(), pairSplitChrom);

            binEvidence.AddMessEvidence(false, pairResultSplitChrom, true, false, false, false, false);
            allHitsNonZeroes[4] = 2;
            indelHitNonZeroes[4] = 2;
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);

            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(false, pairResultSplitChrom, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(false, pairResultSplitChrom, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(false, pairResultSplitChrom, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

            // Read spans 2 bins (long read, or straddles two). Should increment in both.
            var read1_pair4 = TestHelpers.CreateBamAlignment("ATCGATCG", 125005, 126495, 30, true);
            var read2_pair4 = TestHelpers.CreateBamAlignment("ATCGATCG", 126495, 125005, 30, true);

            var pairRead2Spans2Bins = new ReadPair(read1_pair4);
            pairRead2Spans2Bins.AddAlignment(read2_pair4);
            var pairResultRead2Spans2Bins = new PairResult(pairRead2Spans2Bins.GetAlignments(), pairRead2Spans2Bins);

            binEvidence.AddMessEvidence(false, pairResultRead2Spans2Bins, true, false, false, false, false);
            allHitsNonZeroes[4] = 3;
            allHitsNonZeroes[6] = 2;
            allHitsNonZeroes[7] = 1;
            indelHitNonZeroes[4] = 3;
            indelHitNonZeroes[6] = 2;
            indelHitNonZeroes[7] = 1;

            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);

            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(false, pairResultRead2Spans2Bins, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(false, pairResultRead2Spans2Bins, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(false, pairResultRead2Spans2Bins, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

            // Test mate goes outside of binEvidence region
            var read1_pair5 = TestHelpers.CreateBamAlignment("ATCGATCG", 125005, 100026495, 30, true);
            var read2_pair5 = TestHelpers.CreateBamAlignment("ATCGATCG", 100026495, 125005, 30, true);

            var pairRead2PastRegion = new ReadPair(read1_pair5);
            pairRead2PastRegion.AddAlignment(read2_pair5);
            var pairResultRead2PastRegion = new PairResult(pairRead2PastRegion.GetAlignments(), pairRead2PastRegion);

            binEvidence.AddMessEvidence(false, pairResultRead2PastRegion, true, false, false, false, false);
            allHitsNonZeroes[4] = 4;
            indelHitNonZeroes[4] = 4;
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidence, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using mapq mess
            binEvidenceNoMapqMess.AddMessEvidence(false, pairResultRead2PastRegion, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoMapqMess, indelHitNonZeroes, forwardMessNonZeroes, reverseMessNonZeroes, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional
            binEvidenceNoDirectional.AddMessEvidence(false, pairResultRead2PastRegion, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectional, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroes, singleMismatchNonZeroes, allHitsNonZeroes);
            // Not using directional or mapq mess
            binEvidenceNoDirectionalOrMapqMess.AddMessEvidence(false, pairResultRead2PastRegion, true, false, false, false, false);
            CheckCorrectBinsIncremented(numBins, messyHitNonZeroes, binEvidenceNoDirectionalOrMapqMess, indelHitNonZeroes, forwardMessNonZeroesNotUsed, reverseMessNonZeroesNotUsed, mapqMessNonZeroesNotUsed, singleMismatchNonZeroes, allHitsNonZeroes);

        }

        [Fact]
        public void AddAllHits()
        {
            var allHits = new uint[500];
            var binEvidence = new BinEvidence(1, true, 500, false, 500, 123000);

            var nonZeroBins = new Dictionary<int, int>();
            binEvidence.AddAllHits(allHits);
            CheckBins(500, nonZeroBins, (i) => binEvidence.GetAllHits(i));

            allHits[25] = 18;
            allHits[100] = 10;
            nonZeroBins[25] = 18;
            nonZeroBins[100] = 10;
            binEvidence.AddAllHits(allHits);
            CheckBins(500, nonZeroBins, (i) => binEvidence.GetAllHits(i));

            // Test with AllHits not same size as BinEvidence
        }

        private static void CheckCorrectBinsIncremented(int numBins, Dictionary<int, int> messyHitNonZeroes, BinEvidence binEvidence,
            Dictionary<int, int> indelHitNonZeroes, Dictionary<int, int> forwardMessNonZeroes, Dictionary<int, int> reverseMessNonZeroes,
            Dictionary<int, int> mapqMessNonZeroes, Dictionary<int, int> singleMismatchNonZeroes, Dictionary<int, int> allHitsNonZeroes)
        {
            CheckBins(numBins, messyHitNonZeroes, (i) => binEvidence.GetMessyHit(i));
            CheckBins(numBins, indelHitNonZeroes, (i) => binEvidence.GetIndelHit(i));
            CheckBins(numBins, forwardMessNonZeroes, (i) => binEvidence.GetForwardMessyRegionHit(i));
            CheckBins(numBins, reverseMessNonZeroes, (i) => binEvidence.GetReverseMessyRegionHit(i));
            CheckBins(numBins, mapqMessNonZeroes, (i) => binEvidence.GetMapqMessyHit(i));
            CheckBins(numBins, singleMismatchNonZeroes, (i) => binEvidence.GetSingleMismatchHit(i));
            CheckBins(numBins, allHitsNonZeroes, (i)=>binEvidence.GetAllHits(i));
        }

        private static void CheckBins(int numBins, Dictionary<int, int> nonZeroBins, Func<int, int> action)
        {
            for (int i = 0; i < numBins; i++)
            {
                if (!nonZeroBins.ContainsKey(i))
                {
                    Assert.Equal(0, action(i));
                }
                else
                {
                    Assert.Equal(nonZeroBins[i], action(i));
                }
            }
        }
    }
}