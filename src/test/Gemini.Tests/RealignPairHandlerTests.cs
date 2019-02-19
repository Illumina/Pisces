using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using BamStitchingLogic;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.FromHygea;
using Gemini.Interfaces;
using Gemini.IO;
using Gemini.Logic;
using Gemini.Realignment;
using Gemini.Stitching;
using Gemini.Types;
using Moq;
using Pisces.Domain.Types;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{
    public class RealignPairHandlerTests
    {
        [Fact]
        public void PairAwareVsNonAwareRealignment()
        {
            var indel = new HashableIndel()
            {
                AlternateAllele = "AG",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 1,
                ReferencePosition = 102,
                Score = 1,
                Type = AlleleCategory.Insertion
            };
            var indel2 = new HashableIndel()
            {
                AlternateAllele = "AGT",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 2,
                ReferencePosition = 102,
                Score = 10,
                Type = AlleleCategory.Insertion
            };
            var indels = new List<HashableIndel>()
            {
                indel,
                indel2
            };

            var snippetSource = new Mock<IGenomeSnippetSource>();
            snippetSource.Setup(s => s.GetGenomeSnippet(It.IsAny<int>())).Returns(new GenomeSnippet() { Chromosome = "chr1", Sequence = new string('A', 2000), StartPosition = 1 });

            var indelSource = new ChromosomeIndelSource(indels, snippetSource.Object);
            var comparer = new GemBasicAlignmentComparer();
            var realigner = new GeminiReadRealigner(comparer);
            var pairHandler = new PairHandler(new Dictionary<int, string>() {{1, "chr1"}}, new BasicStitcher(0),
                new ReadStatusCounter(), tryStitch: true);

            var mockStatusHandler = new Mock<IStatusHandler>();
            var mockCollector = new Mock<IEvidenceCollector>();
            var evaluator =
                new RealignmentEvaluator(indelSource, mockStatusHandler.Object, realigner, new RealignmentJudger(comparer), "chr1", true, true, true, true);
            var pairRealigner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(), 
                new PostRealignmentStitcher(pairHandler, mockStatusHandler.Object), evaluator, 
                new PairSpecificIndelFinder("chr1", indelSource), "chr1", false);
            var nonPairRealigner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(), 
                new PostRealignmentStitcher(pairHandler, mockStatusHandler.Object), evaluator, 
                new NonPairSpecificIndelFinder(), "chr1", false);
            var nonPairNonStitchRealigner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(), new NonRestitchingRestitcher(), evaluator,
                new NonPairSpecificIndelFinder(), "chr1", false);
            var pairNonStitchRealigner = new ReadPairRealignerAndCombiner(new NonSnowballEvidenceCollector(), new NonRestitchingRestitcher(), evaluator, 
                new PairSpecificIndelFinder("chr1", indelSource), "chr1", false);

            var handler = pairRealigner;
            var nonPairHandler = nonPairRealigner;
            var nonPairNonStitchHandler = nonPairNonStitchRealigner;
            var yesPairNoStitchHandler = pairNonStitchRealigner;

            var pair = TestHelpers.GetPair("3M1I4M", "3M5S");
            var pairWithMismatches = TestHelpers.GetPair("3M1I4M", "8M");
            var pairWithDiffIndelsOnlyOneSupported = TestHelpers.GetPair("3M1I4M", "1M1I6M");
            var pairWithDiffIndelsOneIsBetter = TestHelpers.GetPair("3M1I4M", "3M2I3M");
            var pairWithNoSupportedIndels = TestHelpers.GetPair("1M1I6M", "8M");
            var pairAgreeingOnUnsupportedIndel = TestHelpers.GetPair("1M1I6M", "1M1I6M");
            List<BamAlignment> reads;

            // Case 1: Pair with one having indel seen and one mismatched
            // Pair-aware and stitching, should get stitched read with seen insertion, even though the other would have been stronger
            reads = handler.ExtractReads(pairWithMismatches);
            CheckResult(reads, 1, "3M1I4M");

            // Pair-aware and not stitching, should get two reads with seen insertion
            reads = yesPairNoStitchHandler.ExtractReads(pairWithMismatches);
            CheckResult(reads, 2, "3M1I4M", "3M1I4M");

            // Non-pair-aware and stitching, should get stitched read with other insertion
            reads = nonPairHandler.ExtractReads(pairWithMismatches);
            CheckResult(reads, 1, "3M2I3M");

            // Non-pair-aware and not stitching, should get other insertion in both
            reads = nonPairNonStitchHandler.ExtractReads(pairWithMismatches);
            CheckResult(reads, 2, "3M2I3M", "3M2I3M");
            

            // Case 2: pair with one having indel seen and one sofclipped
            // Pair-aware and stitching, should get stitched read with seen insertion, even though the other would have been stronger
            reads = handler.ExtractReads(pair);
            CheckResult(reads, 1, "3M1I4M");

            // Pair-aware and not stitching, should get two reads with seen insertion
            reads = yesPairNoStitchHandler.ExtractReads(pair);
            CheckResult(reads, 2, "3M1I4M", "3M1I4S"); // TODO do we really want to resoftclip this?
            Assert.Equal(2, reads.Count);

            // Non-pair-aware and stitching, should get stitched read with other insertion
            reads = nonPairHandler.ExtractReads(pair);
            CheckResult(reads, 1, "3M2I3M");

            // Non-pair-aware and not stitching, should get other insertion in both
            reads = nonPairNonStitchHandler.ExtractReads(pair);
            CheckResult(reads, 2, "3M2I3M", "3M2I3S");
            Assert.Equal(2, reads.Count);


            // Case 3: Pair with one having indel seen and one unsupported indel
            // Pair-aware and stitching, should get stitched read with seen insertion, even though the other would have been stronger
            reads = handler.ExtractReads(pairWithDiffIndelsOnlyOneSupported);
            CheckResult(reads, 1, "3M1I4M");

            // Pair-aware and not stitching, should get two reads with seen insertion
            reads = yesPairNoStitchHandler.ExtractReads(pairWithDiffIndelsOnlyOneSupported);
            CheckResult(reads, 2, "3M1I4M", "3M1I4M");

            // Non-pair-aware and stitching, should get stitched read with other insertion
            reads = nonPairHandler.ExtractReads(pairWithDiffIndelsOnlyOneSupported);
            CheckResult(reads, 1, "3M2I3M");

            // Non-pair-aware and not stitching, should get other insertion in both
            reads = nonPairNonStitchHandler.ExtractReads(pairWithDiffIndelsOnlyOneSupported);
            CheckResult(reads, 2, "3M2I3M", "3M2I3M");


            // Case 4: Pair with both indels supported but one is better
            // Pair-aware and stitching, should get stitched read with best seen insertion
            reads = handler.ExtractReads(pairWithDiffIndelsOneIsBetter);
            CheckResult(reads, 1, "3M2I3M");

            // Pair-aware and not stitching, should get two reads with best seen insertion
            reads = yesPairNoStitchHandler.ExtractReads(pairWithDiffIndelsOneIsBetter);
            CheckResult(reads, 2, "3M2I3M", "3M2I3M");

            // Non-pair-aware and stitching, should get stitched read with best seen insertion
            reads = nonPairHandler.ExtractReads(pairWithDiffIndelsOneIsBetter);
            CheckResult(reads, 1, "3M2I3M");

            // Non-pair-aware and not stitching, should get best seen insertion in both
            reads = nonPairNonStitchHandler.ExtractReads(pairWithDiffIndelsOneIsBetter);
            CheckResult(reads, 2, "3M2I3M", "3M2I3M");


            // Case 5: Pair had one read with an unsupported indel seen
            // Pair-aware and stitching, should get stitched read with best supported insertion 
            reads = handler.ExtractReads(pairWithNoSupportedIndels);
            CheckResult(reads, 1, "3M2I3M");

            // Pair-aware and not stitching, should get two reads with best supported insertion
            reads = yesPairNoStitchHandler.ExtractReads(pairWithNoSupportedIndels);
            CheckResult(reads, 2, "3M2I3M", "3M2I3M");

            // Non-pair-aware and stitching, should get stitched read with other insertion
            reads = nonPairHandler.ExtractReads(pairWithNoSupportedIndels);
            CheckResult(reads, 1, "3M2I3M");

            // Non-pair-aware and not stitching, should get other insertion in both
            reads = nonPairNonStitchHandler.ExtractReads(pairWithNoSupportedIndels);
            CheckResult(reads, 2, "3M2I3M", "3M2I3M");


            // Case 6: Pair had both reads agreeing on an unsupported indel
            // Pair-aware and stitching, should get stitched read with best supported insertion ** TODO or would we rather keep the agreed-upon seen one?
            reads = handler.ExtractReads(pairAgreeingOnUnsupportedIndel);
            CheckResult(reads, 1, "3M2I3M");

            // Pair-aware and not stitching, should get two reads with best supported insertion *** TODO or would we rather keep the agreed-upon seen one?
            reads = yesPairNoStitchHandler.ExtractReads(pairAgreeingOnUnsupportedIndel);
            CheckResult(reads, 2, "3M2I3M", "3M2I3M");

            // Non-pair-aware and stitching, should get stitched read with other insertion
            reads = nonPairHandler.ExtractReads(pairAgreeingOnUnsupportedIndel);
            CheckResult(reads, 1, "3M2I3M");

            // Non-pair-aware and not stitching, should get other insertion in both
            reads = nonPairNonStitchHandler.ExtractReads(pairAgreeingOnUnsupportedIndel);
            CheckResult(reads, 2, "3M2I3M", "3M2I3M");


            //var pairDiffIndelsR1Better = Helpers.GetPair("3M1I4M", "3M2I3M", 60, 20);

            //reads = handler.ExtractReads(pairDiffIndelsR1Better);
            //Assert.Equal(1, reads.Count);
            //Assert.Equal("3M1I4M", reads[0].CigarData.ToString());

            //pairDiffIndelsR1Better.PairStatus = PairStatus.Paired;
            //reads = noStitchHandler.ExtractReads(pairDiffIndelsR1Better);
            //Assert.Equal(2, reads.Count);
            //Assert.Equal("3M1I4M", reads[0].CigarData.ToString());
            //Assert.Equal("3M1I4M", reads[1].CigarData.ToString());

            //reads = nonPairHandler.ExtractReads(pairDiffIndelsR1Better);
            //Assert.Equal(2, reads.Count);
            //Assert.Equal("3M2I3M", reads[0].CigarData.ToString());
            //Assert.Equal("3M2I3M", reads[1].CigarData.ToString());

            //var pairDiffIndelsR2Better = Helpers.GetPair("3M1I4M", "3M2I3M", 20, 60);

            //reads = handler.ExtractReads(pairDiffIndelsR2Better);
            //Assert.Equal(1, reads.Count);
            //Assert.Equal("3M2I3M", reads[0].CigarData.ToString());

            //// Read 2 is the better one anyway
            //reads = nonPairHandler.ExtractReads(pairDiffIndelsR2Better);
            //Assert.Equal(2, reads.Count);
            //Assert.Equal("3M2I3M", reads[0].CigarData.ToString());
            //Assert.Equal("3M2I3M", reads[1].CigarData.ToString());

            //reads = nonPairHandler.ExtractReads(pairDiffIndelsR2Better);
            //Assert.Equal(2, reads.Count);
            //Assert.Equal("3M2I3M", reads[0].CigarData.ToString());
            //Assert.Equal("3M2I3M", reads[1].CigarData.ToString());



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
