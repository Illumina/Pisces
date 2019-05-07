using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Gemini.FromHygea;
using Gemini.Interfaces;
using Gemini.IO;
using Gemini.Models;
using Gemini.Realignment;
using Gemini.Types;
using Moq;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using ReadRealignmentLogic.Models;
using Xunit;

namespace Gemini.Tests
{
    public class RealignmentEvaluatorTests
    {
        [Fact]
        public void GetFinalAlignment_NonMock()
        {
            var snippetSource = new Mock<IGenomeSnippetSource>();
            var genomeSnippet = new GenomeSnippet()
            {
                Chromosome = "chr1",
                Sequence = new string('A', 1000) + "ATCGATTGA" + new string('T', 1000),
                StartPosition = 1000
            };
            snippetSource.Setup(x => x.GetGenomeSnippet(It.IsAny<int>())).Returns(genomeSnippet);
            var mockStatusHandler = new Mock<IStatusHandler>();
            var comparer = new GemBasicAlignmentComparer(false, false);

            var readRealigner = new GeminiReadRealigner(comparer, remaskSoftclips: false,
                keepProbeSoftclips: false, keepBothSideSoftclips: false,
                trackActualMismatches: false, checkSoftclipsForMismatches: true,
                debug: false, maskNsOnly: false, maskPartialInsertion: false,
                minimumUnanchoredInsertionLength: 1,
                minInsertionSizeToAllowMismatchingBases: 4, maxProportionInsertSequenceMismatch: 0.2); // TODO fix // TODO figure out what I was saying to fix here...

            var filterer = GetMockRegionFilterer();

            var indels = new List<HashableIndel>();
            var indelSource = new ChromosomeIndelSource(indels, snippetSource.Object);
            var realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);

            var origBamAlignment =
                TestHelpers.CreateBamAlignment("AAAAAAATTCA", 1500, 1500, 30, true, cigar: new CigarAlignment("11M"));
            var realigned = realignmentEvaluator.GetFinalAlignment(origBamAlignment, out bool changed, out bool forcedSoftclip,
                out bool confirmed, out bool sketchy);

            // No indels
            Assert.False(changed);
            Assert.False(confirmed);

            indels = new List<HashableIndel>()
                { new HashableIndel()
                {
                    Chromosome = "chr1",
                    ReferencePosition = 1506,
                    ReferenceAllele = "A",
                    AlternateAllele = "ATT",
                    Type = AlleleCategory.Insertion,
                    Length = 2
                }};
            indelSource = new ChromosomeIndelSource(indels, snippetSource.Object);
            realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);
            realigned = realignmentEvaluator.GetFinalAlignment(origBamAlignment, out changed, out forcedSoftclip,
                out confirmed, out sketchy);
            Assert.True(changed);
            Assert.False(confirmed);
            Assert.Equal("7M2I2M", realigned.CigarData.ToString());

            var confirmedAccepteds = new List<HashableIndel>();
            realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);
            var reRealigned = realignmentEvaluator.GetFinalAlignment(realigned, out changed, out forcedSoftclip,
                out confirmed, out sketchy, confirmedAccepteds: confirmedAccepteds);
            Assert.False(changed);
            Assert.True(confirmed);
            Assert.Equal("7M2I2M", reRealigned.CigarData.ToString());

            // Existing indel is best (and only)
            realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);
            reRealigned = realignmentEvaluator.GetFinalAlignment(realigned, out changed, out forcedSoftclip,
                out confirmed, out sketchy, confirmedAccepteds: confirmedAccepteds, existingIndels: new List<PreIndel>()
                {
                    new PreIndel(new CandidateAllele("chr1", 1506,"A","ATT",AlleleCategory.Insertion))
                });
            Assert.False(changed);
            Assert.True(confirmed);
            Assert.Equal("7M2I2M", reRealigned.CigarData.ToString());

            // Existing indel is unsanctioned but good fit - keep it
            var alignmentWithInsertion =
                TestHelpers.CreateBamAlignment("AAAAAAATTCA", 1500, 1500, 30, true, cigar: new CigarAlignment("7M3I1M"));

            realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, false, filterer.Object, false);

            var realignedExistingIns = realignmentEvaluator.GetFinalAlignment(alignmentWithInsertion, out changed, out forcedSoftclip,
                out confirmed, out sketchy, confirmedAccepteds: confirmedAccepteds, existingIndels: new List<PreIndel>()
                {
                    new PreIndel(new CandidateAllele("chr1", 1506, "A", "ATTC", AlleleCategory.Insertion))
                });
            Assert.False(changed);
            Assert.False(confirmed);
            Assert.Equal("7M3I1M", realignedExistingIns.CigarData.ToString());

            // Existing indel is unsanctioned and we're softclipping unknowns - softclip it
            realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);

            realignedExistingIns = realignmentEvaluator.GetFinalAlignment(alignmentWithInsertion, out changed, out forcedSoftclip,
                out confirmed, out sketchy, confirmedAccepteds: confirmedAccepteds, existingIndels: new List<PreIndel>()
                {
                    new PreIndel(new CandidateAllele("chr1", 1506, "A", "ATTC", AlleleCategory.Insertion))
                });
            Assert.False(changed);
            Assert.False(confirmed);
            Assert.Equal("7M4S", realignedExistingIns.CigarData.ToString());

            indels = new List<HashableIndel>()
                {
                    new HashableIndel()
                    {
                        Chromosome = "chr1",
                        ReferencePosition = 1506,
                        ReferenceAllele = "A",
                        AlternateAllele = "ATT",
                        Type = AlleleCategory.Insertion,
                        Length = 2,
                        Score = 1000
                    },

                    new HashableIndel()
                {
                    Chromosome = "chr1",
                    ReferencePosition = 1506,
                    ReferenceAllele = "A",
                    AlternateAllele = "ATTC",
                    Type = AlleleCategory.Insertion,
                    Length = 3,
                    Score = 760
                },
                    new HashableIndel()
                    {
                        Chromosome = "chr1",
                        ReferencePosition = 1506,
                        ReferenceAllele = "A",
                        AlternateAllele = "ATTG",
                        Type = AlleleCategory.Insertion,
                        Length = 3,
                        Score = 10
                    }
                };
            indelSource = new ChromosomeIndelSource(indels, snippetSource.Object);
            realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);
            realigned = realignmentEvaluator.GetFinalAlignment(origBamAlignment, out changed, out forcedSoftclip,
                out confirmed, out sketchy);
            Assert.True(changed);
            Assert.False(confirmed);
            Assert.Equal("7M3I1M", realigned.CigarData.ToString());

            confirmedAccepteds = new List<HashableIndel>();
            realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);
            reRealigned = realignmentEvaluator.GetFinalAlignment(realigned, out changed, out forcedSoftclip,
                out confirmed, out sketchy, confirmedAccepteds: confirmedAccepteds);
            Assert.False(changed);
            Assert.True(confirmed);
            Assert.Equal("7M3I1M", reRealigned.CigarData.ToString());

            // Existing indel is not the top one but is the best fit, keep it
            realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);
            reRealigned = realignmentEvaluator.GetFinalAlignment(realigned, out changed, out forcedSoftclip,
                out confirmed, out sketchy, confirmedAccepteds: confirmedAccepteds, existingIndels: new List<PreIndel>()
                {
                    new PreIndel(new CandidateAllele("chr1", 1506, "A", "ATTC", AlleleCategory.Insertion))
                });
            Assert.False(changed);
            Assert.True(confirmed);
            Assert.Equal("7M3I1M", reRealigned.CigarData.ToString());


            // Has existing unsanctioned indel and there are better ones to realign around - ignore the bad one, take the good
            realignmentEvaluator = new RealignmentEvaluator(indelSource, mockStatusHandler.Object, readRealigner,
                new RealignmentJudger(comparer), "chr1", false, true, true, true, filterer.Object, false);
            reRealigned = realignmentEvaluator.GetFinalAlignment(realigned, out changed, out forcedSoftclip,
                out confirmed, out sketchy, confirmedAccepteds: confirmedAccepteds, existingIndels: new List<PreIndel>()
                {
                    new PreIndel(new CandidateAllele("chr1", 1507, "A", "ATC", AlleleCategory.Insertion))
                });
            Assert.False(changed);
            Assert.True(confirmed);
            Assert.Equal("7M3I1M", reRealigned.CigarData.ToString());
        }

        [Fact]
        public void GetFinalAlignment()
        {
            var callbackIndelsList = new List<HashableIndel>();
            var mockIndelSource = GetMockIndelSource(new List<HashableIndel>());
            var mockStatusHandler = new Mock<IStatusHandler>();
            var readRealigner = GetMockReadRealigner(null, callbackIndelsList);
            var realignmentJudger = GetMockJudger(false, true, false);
            var filterer = GetMockRegionFilterer();

            var evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true, filterer.Object, true);

            // No indels to realign around, no need to call realignment
            var pair = TestHelpers.GetPair("5M1I5M", "5M1I5M");
            var existingIndels = new List<PreIndel>()
                {new PreIndel(new CandidateAllele("chr1", 100, "A", "ATG", AlleleCategory.Insertion))};
            var alignment = evaluator.GetFinalAlignment(pair.Read1, out bool realigned, out bool forcedSoftclip, out bool confirmed, out bool sketchy, existingIndels: new List<PreIndel>());
            readRealigner.Verify(x=>x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Never);
            Assert.False(realigned);
            Assert.False(forcedSoftclip);
            Assert.Equal(alignment, pair.Read1);
            Assert.Equal("5M1I5M", alignment.CigarData.ToString());
            Assert.Equal(0.0, callbackIndelsList.Count);

            var indel = new HashableIndel()
            {
                ReferencePosition = 100,
                AlternateAllele = "A",
                ReferenceAllele = "ATT"
            };
            var indel2 = new HashableIndel()
            {
                ReferencePosition = 100,
                AlternateAllele = "AAA",
                ReferenceAllele = "A"
            };

            // Has indel to realign around and it failed, and it has a different indel, force softclip
            //pair = TestHelpers.GetPair("5M1I5M", "5M1I5M");
            //callbackIndelsList = new List<HashableIndel>();
            //readRealigner = GetMockReadRealigner(null, callbackIndelsList);
            //mockIndelSource = GetMockIndelSource(new List<HashableIndel>() {indel, indel2});
            
            //evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true, filterer.Object, true);
            //alignment = evaluator.GetFinalAlignment(pair.Read1, out realigned, out forcedSoftclip, out confirmed, existingIndels: existingIndels);
            //readRealigner.Verify(x => x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), 
            //    It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Once);
            //Assert.False(realigned);
            //Assert.True(forcedSoftclip);
            //Assert.Equal(alignment, pair.Read1);
            //Assert.Equal("5M6S", alignment.CigarData.ToString());
            //Assert.Equal(2, callbackIndelsList.Count); // Check indels passed to realigner

            // Has indel to realign around and it succeeds
            pair = TestHelpers.GetPair("5M1I5M", "5M1I5M");
            callbackIndelsList = new List<HashableIndel>();
            mockIndelSource = GetMockIndelSource(new List<HashableIndel>() { indel, indel2 });
            readRealigner = GetMockReadRealigner(new RealignmentResult()
            {
                AcceptedIndels = new List<int> { 1},
                Cigar = new CigarAlignment("4M1I6M"), NumMismatchesIncludeSoftclip = 0, Indels = "blah"
            }, callbackIndelsList);
            realignmentJudger = GetMockJudger(true,false, false);
            evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true, filterer.Object, true);
            alignment = evaluator.GetFinalAlignment(pair.Read1, out realigned, out forcedSoftclip, out confirmed, out sketchy);
            readRealigner.Verify(x => x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Once);
            Assert.True(realigned);
            Assert.False(forcedSoftclip);
            Assert.Equal("4M1I6M", alignment.CigarData.ToString());
            Assert.Equal(2, callbackIndelsList.Count); // Check indels passed to realigner

            // Has indel to realign around but not good enough. Also nothing to softclip.
            pair = TestHelpers.GetPair("11M", "11M");
            callbackIndelsList = new List<HashableIndel>();
            mockIndelSource = GetMockIndelSource(new List<HashableIndel>() { indel, indel2 });
            readRealigner = GetMockReadRealigner(new RealignmentResult() { Cigar = new CigarAlignment("4M1I6M"), NumMismatchesIncludeSoftclip = 0, Indels = "blah" }, callbackIndelsList);
            realignmentJudger = GetMockJudger(false, false, true);
            evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true, filterer.Object, true);
            alignment = evaluator.GetFinalAlignment(pair.Read1, out realigned, out forcedSoftclip, out confirmed, out sketchy);
            readRealigner.Verify(x => x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Once);
            Assert.False(realigned);
            Assert.False(forcedSoftclip);
            Assert.Equal("11M", alignment.CigarData.ToString());
            Assert.Equal(2, callbackIndelsList.Count); // Check indels passed to realigner

            //// Same as above: has indel to realign around but not good enough. Also nothing to softclip. But this time, it's (mocked) pair aware.
            //pair = TestHelpers.GetPair("11M", "11M");
            //callbackIndelsList = new List<HashableIndel>();
            //mockIndelSource = GetMockIndelSource(new List<HashableIndel>() { indel, indel2 });
            //readRealigner = GetMockReadRealigner(new RealignmentResult()
            //{
            //    AcceptedIndels = new List<int>() { 0},
            //    Cigar = new CigarAlignment("4M1I6M"), NumMismatchesIncludeSoftclip = 0, Indels = "blah"
            //}, callbackIndelsList);
            //realignmentJudger = GetMockJudger(false, false, true);
            //evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true,filterer.Object, true);
            //alignment = evaluator.GetFinalAlignment(pair.Read1, out realigned, out forcedSoftclip, out confirmed, selectedIndels: new List<PreIndel>() { new PreIndel(new CandidateAllele("chr1", 100, "A", "ATC", AlleleCategory.Insertion)) });
            //readRealigner.Verify(x => x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Once);
            //Assert.True(realigned);
            //Assert.False(forcedSoftclip);
            //Assert.Equal("4M1I6M", alignment.CigarData.ToString());
            //Assert.Equal(2, callbackIndelsList.Count);

        }

        private Mock<IRealignmentJudger> GetMockJudger(bool betterOrEqual, bool unchanged, bool betterOrEqualPairAware)
        {
            var realignmentJudger = new Mock<IRealignmentJudger>();

            realignmentJudger
                .Setup(x => x.RealignmentBetterOrEqual(It.IsAny<RealignmentResult>(), It.IsAny<AlignmentSummary>(), true))
                .Returns(betterOrEqualPairAware);
            realignmentJudger
                .Setup(x => x.RealignmentBetterOrEqual(It.IsAny<RealignmentResult>(), It.IsAny<AlignmentSummary>(), false))
                .Returns(betterOrEqual);
            realignmentJudger
                .Setup(x => x.RealignmentIsUnchanged(It.IsAny<RealignmentResult>(), It.IsAny<BamAlignment>()))
                .Returns(unchanged);

            return realignmentJudger;

        }

        private Mock<IReadRealigner> GetMockReadRealigner(RealignmentResult result, List<HashableIndel> callbackIndelsList)
        {
            var readRealigner = new Mock<IReadRealigner>();
            readRealigner.Setup(x => x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(),
                It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<bool>(),
                It.IsAny<int>())).Returns<Read,List<HashableIndel>,Dictionary<HashableIndel,GenomeSnippet>,bool,int>((r,i,g,ps,m)=>result).Callback<Read, List<HashableIndel>, Dictionary<HashableIndel, GenomeSnippet>, bool, int>((r, i, g, ps, m) => callbackIndelsList.AddRange(i));
            return readRealigner;
        }

        private Mock<IRegionFilterer> GetMockRegionFilterer()
        {
            var filterer = new Mock<IRegionFilterer>();
            filterer.Setup(x => x.AnyIndelsNearby(It.IsAny<int>())).Returns(true);
            return filterer;
        }
        private Mock<IChromosomeIndelSource> GetMockIndelSource(List<HashableIndel> indels)
        {
            var mockIndelSource = new Mock<IChromosomeIndelSource>();
            var result = new List<KeyValuePair<HashableIndel, GenomeSnippet>>();
            foreach (var indel in indels)
            {
                result.Add(new KeyValuePair<HashableIndel, GenomeSnippet>(indel, new GenomeSnippet(){Sequence = new string('A', 2000)}));
            }

            mockIndelSource.Setup(x => x.GetRelevantIndels(It.IsAny<int>(), It.IsAny<List<PreIndel>>(), It.IsAny<List<HashableIndel>>(), It.IsAny<List<PreIndel>>(), It.IsAny<List<PreIndel>>()))
                .Returns(result);
            return mockIndelSource;
        }
    }
}