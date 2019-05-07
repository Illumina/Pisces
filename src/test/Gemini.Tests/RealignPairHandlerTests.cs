using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using BamStitchingLogic;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.FromHygea;
using Gemini.Interfaces;
using Gemini.Logic;
using Gemini.Realignment;
using Gemini.Stitching;
using Gemini.Types;
using Moq;
using Pisces.Domain.Types;
using ReadRealignmentLogic.Models;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{
    public class RealignPairHandlerTests
    {
        private PairResult GetPairResult(Alignment.Domain.ReadPair pair)
        {
            return new PairResult() {ReadPair = pair};
        }

        [Fact]
        public void PairAwareVsNonAwareRealignment()
        {
            // TODO find a way to actually test this that isolates properly. This was a mess.
            //var indel = new HashableIndel()
            //{
            //    AlternateAllele = "AG",
            //    ReferenceAllele = "A",
            //    Chromosome = "chr1",
            //    Length = 1,
            //    ReferencePosition = 102,
            //    Score = 1,
            //    Type = AlleleCategory.Insertion,
            //    IsDuplication = true
            //};
            //var indelNotHard = new HashableIndel()
            //{
            //    AlternateAllele = "AGTT",
            //    ReferenceAllele = "A",
            //    Chromosome = "chr1",
            //    Length = 3,
            //    ReferencePosition = 102,
            //    Score = 2,
            //    Type = AlleleCategory.Insertion,
            //    IsDuplication = true
            //};
            //var indel2 = new HashableIndel()
            //{
            //    AlternateAllele = "AGT",
            //    ReferenceAllele = "A",
            //    Chromosome = "chr1",
            //    Length = 2,
            //    ReferencePosition = 102,
            //    Score = 10,
            //    Type = AlleleCategory.Insertion
            //};
            //var indels = new List<HashableIndel>()
            //{
            //    indel,
            //    indel2,
            //    indelNotHard
            //};

            //var readSequence = "AAAGTTTTCCCCCCCCCCCC";

            //var snippetSource = new Mock<IGenomeSnippetSource>();
            //snippetSource.Setup(s => s.GetGenomeSnippet(It.IsAny<int>())).Returns(new GenomeSnippet() { Chromosome = "chr1", Sequence = new string('A', 102) + "TTTTCCCCCCCCCCCC" + new string('A', 1000), StartPosition = 1 });
            //var regionFilterer = new Mock<IRegionFilterer>();
            //regionFilterer.Setup(x => x.AnyIndelsNearby(It.IsAny<int>())).Returns(true);

            //var indelSource = new ChromosomeIndelSource(indels, snippetSource.Object);
            //var comparer = new GemBasicAlignmentComparer();
            //var realigner = new GeminiReadRealigner(comparer, maskNsOnly: false, maskPartialInsertion: false);
            //var pairHandler = new PairHandler(new Dictionary<int, string>() {{1, "chr1"}}, new BasicStitcher(0),
            //    new ReadStatusCounter(), tryStitch: true);

            ////var judger = new RealignmentJudger(comparer);
            //var judgerMock = new Mock<IRealignmentJudger>();
            //judgerMock.Setup(x =>
            //    x.RealignmentBetterOrEqual(It.IsAny<RealignmentResult>(), It.IsAny<AlignmentSummary>(),
            //        It.IsAny<bool>())).Returns(true);
            //judgerMock.Setup(x => x.IsVeryConfident(It.IsAny<AlignmentSummary>())).Returns(true);
            //var judger = judgerMock.Object;

            //var mockStatusHandler = new Mock<IStatusHandler>();
            //var evaluator =
            //    new RealignmentEvaluator(indelSource, mockStatusHandler.Object, realigner, judger, "chr1", true, true, true, true, regionFilterer.Object, true);
            //var pairRealigner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(), 
            //    new PostRealignmentStitcher(pairHandler, mockStatusHandler.Object), evaluator, 
            //    new PairSpecificIndelFinder(), "chr1", false, hasExistingIndels:true);
            //var nonPairRealigner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(), 
            //    new PostRealignmentStitcher(pairHandler, mockStatusHandler.Object), evaluator, 
            //    new NonPairSpecificIndelFinder(), "chr1", false, hasExistingIndels: true, pairAware: false);
            //var nonPairNonStitchRealigner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(), new NonRestitchingRestitcher(), 
            //    evaluator, new NonPairSpecificIndelFinder(), "chr1", false, hasExistingIndels: true, pairAware: false);
            //var pairNonStitchRealigner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(), new NonRestitchingRestitcher(), evaluator, 
            //    new PairSpecificIndelFinder(), "chr1", false, hasExistingIndels: true);

            //var handler = pairRealigner;
            //var nonPairHandler = nonPairRealigner;
            //var nonPairNonStitchHandler = nonPairNonStitchRealigner;
            //var yesPairNoStitchHandler = pairNonStitchRealigner;

            //List<BamAlignment> reads = new List<BamAlignment>();

            //// TODO make this be with actual sequences that are better and worse.

            //////// Case 1: Pair with one having indel seen and one mismatched
            ////// If there's a higher rated insertion and it DOES look just as good in the read, we're going to go with that one unless it's hard to call
            ////reads = handler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "8M")));
            ////CheckResult(reads, 1, "3M2I3M");

            ////// Pair-aware and not stitching, should get two reads with best insertion
            ////reads = yesPairNoStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "8M")));
            //////CheckResult(reads, 2, "3M1I4M", "3M1I4M");
            ////CheckResult(reads, 2, "3M2I3M", "3M2I3M");

            ////// Non-pair-aware and stitching, should get stitched read with other insertion
            ////reads = nonPairHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "8M")));
            ////CheckResult(reads, 1, "3M2I3M");

            ////// Non-pair-aware and not stitching, should get other insertion in both
            ////reads = nonPairNonStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "8M")));
            ////CheckResult(reads, 2, "3M2I3M", "3M2I3M");


            //////// Case 2: pair with one having indel seen and one sofclipped
            ////// Pair-aware and stitching, should get stitched read with seen insertion, even though the other is higher rated, as long as both integrate equally well
            ////reads = handler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "3M5S")));
            //////CheckResult(reads, 1, "3M1I4M");
            ////CheckResult(reads, 1, "3M2I3M");

            ////// Pair-aware and not stitching, should get two reads with seen insertion
            ////reads = yesPairNoStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "3M5S")));
            //////CheckResult(reads, 2, "3M1I4M", "3M1I1M3S"); // TODO do we really want to resoftclip this? 
            ////CheckResult(reads, 2, "3M2I3M", "3M2I1M2S"); // TODO do we really want to resoftclip this? 
            ////Assert.Equal(2, reads.Count);

            ////// Non-pair-aware and stitching, should get stitched read with other insertion
            ////reads = nonPairHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "3M5S")));
            ////CheckResult(reads, 1, "3M2I3M");

            //////// Non-pair-aware and not stitching, should get other insertion in both
            //////reads = nonPairNonStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "3M5S")));
            //////CheckResult(reads, 2, "3M2I3M", "3M2I1M2S");
            //////Assert.Equal(2, reads.Count);


            ////// Case 3: Pair with one having indel seen and one unsupported indel
            ////reads = handler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "1M1I6M")));
            //////CheckResult(reads, 1, "3M1I4M");
            ////CheckResult(reads, 1, "3M2I3M");


            ////// All else being equal, should take the seen one first even though the other has higher score, given that the seen one is hard to call
            ////reads = handler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "3M1I4M")));
            ////CheckResult(reads, 1, "3M1I4M");

            //reads = handler.ExtractReads(GetPairResult(TestHelpers.GetPair("1M1I6M", "3M1I4M")));
            //CheckResult(reads, 1, "3M1I4M");

            //// Pair-aware and not stitching, should get two reads with seen insertion
            //reads = yesPairNoStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "1M1I6M")));
            //CheckResult(reads, 2, "3M1I4M", "3M1I4M");

            ////// Non-pair-aware and stitching, should get stitched read with other insertion
            ////reads = nonPairHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "1M1I6M")));
            ////CheckResult(reads, 1, "3M2I3M");

            ////// Non-pair-aware and not stitching, should get other insertion in both
            ////reads = nonPairNonStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "1M1I6M")));
            ////CheckResult(reads, 2, "3M2I3M", "3M2I3M");


            ////// Case 4: Pair with both indels supported but one is better
            //// Pair-aware and stitching, should get stitched read with best seen insertion
            //reads = handler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "3M2I3M")));
            //CheckResult(reads, 1, "3M2I3M");

            //// Pair-aware and not stitching, should get two reads with best seen insertion
            //reads = yesPairNoStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "3M2I3M")));
            //CheckResult(reads, 2, "3M2I3M", "3M2I3M");

            //// Non-pair-aware and stitching, should get stitched read with best seen insertion
            //reads = nonPairHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "3M2I3M")));
            //CheckResult(reads, 1, "3M2I3M");

            //// Non-pair-aware and not stitching, should get best seen insertion in both
            //reads = nonPairNonStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M1I4M", "3M2I3M")));
            //CheckResult(reads, 2, "3M2I3M", "3M2I3M");


            ////// Case 5: Pair had one read with an unsupported indel seen
            //// Pair-aware and stitching, should get stitched read with best supported insertion (realigning around the good insertion)
            //reads = handler.ExtractReads(GetPairResult(TestHelpers.GetPair("1M1I6M", "8M")));
            //CheckResult(reads, 1, "3M2I3M");

            //// Pair-aware and not stitching, should get two reads with best supported insertion
            //reads = yesPairNoStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("1M1I6M", "8M")));
            //CheckResult(reads, 2, "3M2I3M", "3M2I3M");

            ////// Non-pair-aware and stitching, should get stitched read with other insertion
            ////reads = nonPairHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("1M1I6M", "8M")));
            ////CheckResult(reads, 1, "3M2I3M");

            ////// Non-pair-aware and not stitching, should get other insertion in both
            ////reads = nonPairNonStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("1M1I6M", "8M")));
            ////CheckResult(reads, 2, "3M2I3M", "3M2I3M");


            //// Case 6: Pair had both reads agreeing on an unsupported indel
            //// Pair-aware and stitching, should get stitched read with best supported insertion ** TODO or would we rather keep the agreed-upon seen one?
            //reads = handler.ExtractReads(GetPairResult(TestHelpers.GetPair("1M1I6M", "1M1I6M")));
            //CheckResult(reads, 1, "3M2I3M");

            //// Pair-aware and not stitching, should get two reads with best supported insertion *** TODO or would we rather keep the agreed-upon seen one?
            //reads = yesPairNoStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("1M1I6M", "1M1I6M")));
            //CheckResult(reads, 2, "3M2I3M", "3M2I3M");

            ////// Non-pair-aware and stitching, should get stitched read with other insertion
            ////reads = nonPairHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("1M1I6M", "1M1I6M")));
            ////CheckResult(reads, 1, "3M2I3M");

            ////// Non-pair-aware and not stitching, should get other insertion in both
            ////reads = nonPairNonStitchHandler.ExtractReads(GetPairResult(TestHelpers.GetPair("1M1I6M", "1M1I6M")));
            ////CheckResult(reads, 2, "3M2I3M", "3M2I3M");


            //////var pairWithPartialInsertion = TestHelpers.GetPair("3M2I3M", "1I3M", read2Offset: 2);
            //// Case 7: Read 1 has long insertion, read 2 has partial. Should stitch and retain insertion.
            //// Pair-aware and stitching, should get stitched read with best supported insertion ** TODO or would we rather keep the agreed-upon seen one?
            //reads = handler.ExtractReads(GetPairResult(TestHelpers.GetPair("3M2I3M", "3M1I")));
            //CheckResult(reads, 1, "3M2I3M");


            //// TODO ADD TEST WITH R2 HAS INDELS ONLY
            ////var pairDiffIndelsR1Better = Helpers.GetPair("3M1I4M", "3M2I3M", 60, 20);

            ////reads = handler.ExtractReads(pairDiffIndelsR1Better);
            ////Assert.Equal(1, reads.Count);
            ////Assert.Equal("3M1I4M", reads[0].CigarData.ToString());

            ////pairDiffIndelsR1Better.PairStatus = PairStatus.Paired;
            ////reads = noStitchHandler.ExtractReads(pairDiffIndelsR1Better);
            ////Assert.Equal(2, reads.Count);
            ////Assert.Equal("3M1I4M", reads[0].CigarData.ToString());
            ////Assert.Equal("3M1I4M", reads[1].CigarData.ToString());

            ////reads = nonPairHandler.ExtractReads(pairDiffIndelsR1Better);
            ////Assert.Equal(2, reads.Count);
            ////Assert.Equal("3M2I3M", reads[0].CigarData.ToString());
            ////Assert.Equal("3M2I3M", reads[1].CigarData.ToString());

            ////var pairDiffIndelsR2Better = Helpers.GetPair("3M1I4M", "3M2I3M", 20, 60);

            ////reads = handler.ExtractReads(pairDiffIndelsR2Better);
            ////Assert.Equal(1, reads.Count);
            ////Assert.Equal("3M2I3M", reads[0].CigarData.ToString());

            ////// Read 2 is the better one anyway
            ////reads = nonPairHandler.ExtractReads(pairDiffIndelsR2Better);
            ////Assert.Equal(2, reads.Count);
            ////Assert.Equal("3M2I3M", reads[0].CigarData.ToString());
            ////Assert.Equal("3M2I3M", reads[1].CigarData.ToString());

            ////reads = nonPairHandler.ExtractReads(pairDiffIndelsR2Better);
            ////Assert.Equal(2, reads.Count);
            ////Assert.Equal("3M2I3M", reads[0].CigarData.ToString());
            ////Assert.Equal("3M2I3M", reads[1].CigarData.ToString());



        }

        private void CheckResult(List<BamAlignment> reads, int numReadsExpected, string cigar1, string cigar2 = null)
        {
            Assert.Equal(numReadsExpected, reads.Count);
            Assert.Equal(cigar1, reads[0].CigarData.ToString());
            if (numReadsExpected > 1)
            {
                Assert.Equal(cigar2, reads[1].CigarData.ToString());
            }

        }


    }
}
