using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Gemini.FromHygea;
using Gemini.Interfaces;
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
        public void GetFinalAlignment()
        {
            var callbackIndelsList = new List<HashableIndel>();
            var mockIndelSource = GetMockIndelSource(new List<HashableIndel>());
            var mockStatusHandler = new Mock<IStatusHandler>();
            var readRealigner = GetMockReadRealigner(null, callbackIndelsList);
            var realignmentJudger = GetMockJudger(false, true, false);

            var evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true);

            // No indels to realign around, no need to call realignment
            var pair = TestHelpers.GetPair("5M1I5M", "5M1I5M");
            var alignment = evaluator.GetFinalAlignment(pair.Read1, out bool realigned, out bool forcedSoftclip);
            readRealigner.Verify(x=>x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<IIndelRanker>(),It.IsAny<bool>(), It.IsAny<int>()), Times.Never);
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
            pair = TestHelpers.GetPair("5M1I5M", "5M1I5M");
            callbackIndelsList = new List<HashableIndel>();
            readRealigner = GetMockReadRealigner(null, callbackIndelsList);
            mockIndelSource = GetMockIndelSource(new List<HashableIndel>() {indel, indel2});
            evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true);
            alignment = evaluator.GetFinalAlignment(pair.Read1, out realigned, out forcedSoftclip);
            readRealigner.Verify(x => x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<IIndelRanker>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Once);
            Assert.False(realigned);
            Assert.True(forcedSoftclip);
            Assert.Equal(alignment, pair.Read1);
            Assert.Equal("5M6S", alignment.CigarData.ToString());
            Assert.Equal(2, callbackIndelsList.Count); // Check indels passed to realigner

            // Has indel to realign around and it succeeds
            pair = TestHelpers.GetPair("5M1I5M", "5M1I5M");
            callbackIndelsList = new List<HashableIndel>();
            mockIndelSource = GetMockIndelSource(new List<HashableIndel>() { indel, indel2 });
            readRealigner = GetMockReadRealigner(new RealignmentResult() {Cigar = new CigarAlignment("4M1I6M"), NumMismatchesIncludeSoftclip = 0, Indels = "blah"}, callbackIndelsList);
            realignmentJudger = GetMockJudger(true,false, false);
            evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true);
            alignment = evaluator.GetFinalAlignment(pair.Read1, out realigned, out forcedSoftclip);
            readRealigner.Verify(x => x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<IIndelRanker>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Once);
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
            evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true);
            alignment = evaluator.GetFinalAlignment(pair.Read1, out realigned, out forcedSoftclip);
            readRealigner.Verify(x => x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<IIndelRanker>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Once);
            Assert.False(realigned);
            Assert.False(forcedSoftclip);
            Assert.Equal("11M", alignment.CigarData.ToString());
            Assert.Equal(2, callbackIndelsList.Count); // Check indels passed to realigner

            // Same as above: has indel to realign around but not good enough. Also nothing to softclip. But this time, it's (mocked) pair aware.
            pair = TestHelpers.GetPair("11M", "11M");
            callbackIndelsList = new List<HashableIndel>();
            mockIndelSource = GetMockIndelSource(new List<HashableIndel>() { indel, indel2 });
            readRealigner = GetMockReadRealigner(new RealignmentResult() { Cigar = new CigarAlignment("4M1I6M"), NumMismatchesIncludeSoftclip = 0, Indels = "blah" }, callbackIndelsList);
            realignmentJudger = GetMockJudger(false, false, true);
            evaluator = new RealignmentEvaluator(mockIndelSource.Object, mockStatusHandler.Object, readRealigner.Object, realignmentJudger.Object, "chr1", true, true, true, true);
            alignment = evaluator.GetFinalAlignment(pair.Read1, out realigned, out forcedSoftclip, new List<PreIndel>() { new PreIndel(new CandidateAllele("chr1", 100, "A", "ATC", AlleleCategory.Insertion)) });
            readRealigner.Verify(x => x.Realign(It.IsAny<Read>(), It.IsAny<List<HashableIndel>>(), It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<IIndelRanker>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Once);
            Assert.True(realigned);
            Assert.False(forcedSoftclip);
            Assert.Equal("4M1I6M", alignment.CigarData.ToString());
            Assert.Equal(2, callbackIndelsList.Count);
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
                It.IsAny<Dictionary<HashableIndel, GenomeSnippet>>(), It.IsAny<IIndelRanker>(), It.IsAny<bool>(),
                It.IsAny<int>())).Returns<Read,List<HashableIndel>,Dictionary<HashableIndel,GenomeSnippet>,IIndelRanker,bool,int>((r,i,g,ir,ps,m)=>result).Callback<Read, List<HashableIndel>, Dictionary<HashableIndel, GenomeSnippet>, IIndelRanker, bool, int>((r, i, g, ir, ps, m) => callbackIndelsList.AddRange(i));
            return readRealigner;
        }

        private Mock<IChromosomeIndelSource> GetMockIndelSource(List<HashableIndel> indels)
        {
            var mockIndelSource = new Mock<IChromosomeIndelSource>();
            var result = new List<KeyValuePair<HashableIndel, GenomeSnippet>>();
            foreach (var indel in indels)
            {
                result.Add(new KeyValuePair<HashableIndel, GenomeSnippet>(indel, new GenomeSnippet(){Sequence = new string('A', 2000)}));
            }

            mockIndelSource.Setup(x => x.GetRelevantIndels(It.IsAny<int>(), It.IsAny<List<PreIndel>>()))
                .Returns(result);
            return mockIndelSource;
        }
    }
}