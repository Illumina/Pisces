using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BamStitchingLogic;
using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;
using Gemini.Logic;
using Gemini.Models;
using Gemini.Realignment;
using Gemini.Types;
using Gemini.Utility;
using Moq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Gemini.Tests
{
    public class CategorizedBamRealignerTests
    {
        [Fact]
        public void RealignAroundCandidateIndels_HappyPath()
        {
            var geminiOptions = new GeminiOptions();
            var stitcherOptions = new StitcherOptions();
            var geminiSampleOptions = new GeminiSampleOptions() { OutputFolder = "Outfolder" };
            var realignmentOptions = new RealignmentOptions()
            {
                CategoriesForRealignment = new List<PairClassification>() { PairClassification.Disagree, PairClassification.FailStitch, PairClassification.ImperfectStitched, PairClassification.UnstitchIndel },
                CategoriesForSnowballing = new List<PairClassification>() { PairClassification.UnstitchIndel }
            };

            var indelStringLookup = new Dictionary<string, int[]>()
            {
                { "chr1:104 NN>N", new []{1,5,5,0,30,1,0,0,1}}
            };
            var indelStringLookup2 = new Dictionary<string, int[]>()
            {
                { "chr1:104 NN>N", new []{3,15,15,0,90,1,1,1,3}}
            };

            Dictionary<string, List<PreIndel>> indelsLookup = new Dictionary<string, List<PreIndel>>()
            {
                {"chr1", new List<PreIndel>(){ new PreIndel(new CandidateAllele("chr1",123,"A","ATC", AlleleCategory.Insertion))} }
            };

            Dictionary<string, Dictionary<PairClassification, List<string>>> categorizedAlignments = new Dictionary<string, Dictionary<PairClassification, List<string>>>()
            {
                {"chr1", new Dictionary<PairClassification, List<string>>()
                {
                    {PairClassification.Disagree, new List<string>(){"disagreepath1", "disagreepath2", "disagreepath3"} },
                    {PairClassification.UnstitchIndel, new List<string>(){"unstitchindel1", "unstitchindel2"} },
                    {PairClassification.FailStitch, new List<string>(){"failstitch1"} },
                    {PairClassification.ImperfectStitched, new List<string>(){"imperfect1"} },
                    {PairClassification.PerfectStitched, new List<string>(){"perfect1"} },

                }}
            };

            var mockChromIndelSource = new Mock<IChromosomeIndelSource>();
            var mockRealigner = new Mock<IRealigner>();
            mockRealigner.Setup(x => x.GetIndels()).Returns(indelStringLookup2);
            var mockGeminiFactory = new Mock<IGeminiFactory>();
            var filterer = new BasicIndelFilterer(0, 0, false);
            mockGeminiFactory.Setup(x => x.GetRealigner(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<ChromosomeIndelSource>())).Returns(mockRealigner.Object);
            mockGeminiFactory.Setup(x => x.GetIndelFilterer()).Returns(filterer);
            mockGeminiFactory.Setup(x => x.GetIndelPruner()).Returns(new IndelPruner(false, 0));

            var mockDataSourceFactory = new Mock<IGeminiDataSourceFactory>();
            var mockHashableSource = new Mock<IHashableIndelSource>();

            var hashable = new HashableIndel()
            {
                Chromosome = "chr1",
                ReferencePosition = 100,
                ReferenceAllele = "A",
                AlternateAllele = "ATG",
                Type = AlleleCategory.Insertion
            };
            var hashable2 = new HashableIndel()
            {
                Chromosome = "chr1",
                ReferencePosition = 101,
                ReferenceAllele = "A",
                AlternateAllele = "ATTTTG",
                Type = AlleleCategory.Deletion
            };
            var hashable3 = new HashableIndel()
            {
                Chromosome = "chr1",
                ReferencePosition = 1000000,
                ReferenceAllele = "A",
                AlternateAllele = "ATTTTG",
                Type = AlleleCategory.Insertion
            };

            mockChromIndelSource.Setup(x => x.Indels).Returns(new List<HashableIndel>(){hashable, hashable2, hashable3});

            mockHashableSource.Setup(x => x.GetFinalIndelsForChromosome(It.IsAny<string>(), It.IsAny<List<PreIndel>>()))
                .Returns(new List<HashableIndel>() { hashable, hashable2, hashable3 });

            mockDataSourceFactory.Setup(x => x.GetHashableIndelSource()).Returns(mockHashableSource.Object);
            var mockGenomeSnippetSource = new Mock<IGenomeSnippetSource>();
            mockGenomeSnippetSource.Setup(x => x.GetGenomeSnippet(It.IsAny<int>()))
                .Returns(new GenomeSnippet() { Chromosome = "chr1", Sequence = new String('A', 2000) });
            mockDataSourceFactory.Setup(x => x.CreateGenomeSnippetSource(It.IsAny<string>()))
                .Returns(mockGenomeSnippetSource.Object);
            mockDataSourceFactory.Setup(x => x.GetChromosomeIndelSource(It.IsAny<List<HashableIndel>>(), It.IsAny<IGenomeSnippetSource>()))
                .Returns<List<HashableIndel>, IGenomeSnippetSource>((h,g)=>new ChromosomeIndelSource(h,g));
            mockGeminiFactory
                .Setup(x => x.GetRealigner(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IChromosomeIndelSource>()))
                .Returns(mockRealigner.Object);


            var categorizedRealigner = new CategorizedBamRealigner(geminiOptions, geminiSampleOptions,
                realignmentOptions, mockGeminiFactory.Object, mockDataSourceFactory.Object);


            categorizedRealigner.RealignAroundCandidateIndels(indelStringLookup, categorizedAlignments);

            // Snowball on 2 (2 files for UnstitchIndel)
            mockGeminiFactory.Verify(x => x.GetRealigner(It.IsAny<string>(), true, It.IsAny<IChromosomeIndelSource>()), Times.Exactly(2));
            // Normal realign on the rest
            mockGeminiFactory.Verify(x => x.GetRealigner(It.IsAny<string>(), false, It.IsAny<IChromosomeIndelSource>()), Times.Exactly(5));


            // Get snippets once for originals, and once after snowballing
            mockDataSourceFactory.Verify(x => x.CreateGenomeSnippetSource(It.IsAny<string>()), Times.Exactly(2));
            mockGenomeSnippetSource.Verify(x => x.GetGenomeSnippet(It.IsAny<int>()), Times.Exactly(4)); // 2x for each variant bucket - before and after snowballing

            mockHashableSource.Verify(x => x.GetFinalIndelsForChromosome("chr1", It.IsAny<List<PreIndel>>()), Times.Exactly(2));

            Assert.Equal(1.0, categorizedAlignments.Count);
            Assert.Equal(5, categorizedAlignments["chr1"].Count);

            // Paths for realigned categories should be updated to the realigned path
            var disagreePaths = categorizedAlignments["chr1"][PairClassification.Disagree];
            Assert.Equal(3, disagreePaths.Count);
            Assert.Equal(Path.Combine(geminiSampleOptions.OutputFolder, "disagreepath1" + "_realigned"), disagreePaths.First());

            // Shouldn't have been touched, wasn't realigned
            var perfectPaths = categorizedAlignments["chr1"][PairClassification.PerfectStitched];
            Assert.Equal(1.0, perfectPaths.Count);
            Assert.Equal(Path.Combine("perfect1"), perfectPaths.First());

            // Snowball categories should still output at realigned paths, and have same number as before
            var unstitchIndelPaths = categorizedAlignments["chr1"][PairClassification.UnstitchIndel];
            Assert.Equal(2, unstitchIndelPaths.Count);
            Assert.Equal(Path.Combine(geminiSampleOptions.OutputFolder, "unstitchindel1" + "_realigned"), unstitchIndelPaths.First());

        }

        [Fact]
        public void RealignAroundCandidateIndels_NoIndels()
        {
            var geminiOptions = new GeminiOptions();
            var geminiSampleOptions = new GeminiSampleOptions() { OutputFolder = "Outfolder" };
            var realignmentOptions = new RealignmentOptions()
            {
                CategoriesForRealignment = new List<PairClassification>() { PairClassification.Disagree, PairClassification.FailStitch, PairClassification.ImperfectStitched, PairClassification.UnstitchIndel },
                CategoriesForSnowballing = new List<PairClassification>() { PairClassification.UnstitchIndel }
            };

            var indelStringLookup = new Dictionary<string, int[]>()
            {
                { "chr1:104 NN>N", new []{1,5,5,0,30,1,0,0,1}}
            };
            var indelStringLookup2 = new Dictionary<string, int[]>()
            {
                { "chr1:104 NN>N", new []{3,15,15,0,90,1,1,1,3}}
            };

            Dictionary<string, Dictionary<PairClassification, List<string>>> categorizedAlignments = new Dictionary<string, Dictionary<PairClassification, List<string>>>()
            {
                {"chr1", new Dictionary<PairClassification, List<string>>()
                {
                    {PairClassification.Disagree, new List<string>(){"disagreepath1", "disagreepath2", "disagreepath3"} },
                    {PairClassification.UnstitchIndel, new List<string>(){"unstitchindel1", "unstitchindel2"} },
                    {PairClassification.FailStitch, new List<string>(){"failstitch1"} },
                    {PairClassification.ImperfectStitched, new List<string>(){"imperfect1"} },
                    {PairClassification.PerfectStitched, new List<string>(){"perfect1"} },

                }}
            };

            var mockRealigner = new Mock<IRealigner>();
            mockRealigner.Setup(x => x.GetIndels()).Returns(indelStringLookup2);
            var mockGeminiFactory = new Mock<IGeminiFactory>();
            var filterer = new BasicIndelFilterer(0, 0, false);
            mockGeminiFactory.Setup(x => x.GetRealigner(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<ChromosomeIndelSource>())).Returns(mockRealigner.Object);
            mockGeminiFactory.Setup(x => x.GetIndelFilterer()).Returns(filterer);
            mockGeminiFactory.Setup(x => x.GetIndelPruner()).Returns(new IndelPruner(false, 0));

            var mockDataSourceFactory = new Mock<IGeminiDataSourceFactory>();
            var mockHashableSource = new Mock<IHashableIndelSource>();

            var hashable = new HashableIndel()
            {
                Chromosome = "chr1",
                ReferencePosition = 100,
                ReferenceAllele = "A",
                AlternateAllele = "ATG",
                Type = AlleleCategory.Insertion
            };
            var hashable2 = new HashableIndel()
            {
                Chromosome = "chr1",
                ReferencePosition = 101,
                ReferenceAllele = "A",
                AlternateAllele = "ATTTTG",
                Type = AlleleCategory.Deletion
            };
            var hashable3 = new HashableIndel()
            {
                Chromosome = "chr1",
                ReferencePosition = 1000000,
                ReferenceAllele = "A",
                AlternateAllele = "ATTTTG",
                Type = AlleleCategory.Insertion
            };

            // No indels
            var mockChromIndelSource = new Mock<IChromosomeIndelSource>();
            mockChromIndelSource.Setup(x => x.Indels).Returns(new List<HashableIndel>());

            mockHashableSource.Setup(x => x.GetFinalIndelsForChromosome(It.IsAny<string>(), It.IsAny<List<PreIndel>>()))
                .Returns(new List<HashableIndel>() { });
            mockDataSourceFactory.Setup(x => x.GetHashableIndelSource()).Returns(mockHashableSource.Object);
            var mockGenomeSnippetSource = new Mock<IGenomeSnippetSource>();
            mockGenomeSnippetSource.Setup(x => x.GetGenomeSnippet(It.IsAny<int>()))
                .Returns(new GenomeSnippet() { Chromosome = "chr1", Sequence = new String('A', 2000) });
            mockDataSourceFactory.Setup(x => x.CreateGenomeSnippetSource(It.IsAny<string>()))
                .Returns(mockGenomeSnippetSource.Object);
            mockDataSourceFactory.Setup(x => x.GetChromosomeIndelSource(It.IsAny<List<HashableIndel>>(), It.IsAny<IGenomeSnippetSource>()))
                .Returns(mockChromIndelSource.Object);


            var categorizedRealigner = new CategorizedBamRealigner(geminiOptions, geminiSampleOptions,
                realignmentOptions, mockGeminiFactory.Object, mockDataSourceFactory.Object);


            categorizedRealigner.RealignAroundCandidateIndels(indelStringLookup, categorizedAlignments);

            // No indels to realign to -> no realignment
            mockGeminiFactory.Verify(x => x.GetRealigner(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IChromosomeIndelSource>()), Times.Never);

            // Get snippets once for originals, and once after snowballing
            mockDataSourceFactory.Verify(x => x.CreateGenomeSnippetSource(It.IsAny<string>()), Times.Exactly(2));
            mockGenomeSnippetSource.Verify(x => x.GetGenomeSnippet(It.IsAny<int>()), Times.Never);

            mockHashableSource.Verify(x => x.GetFinalIndelsForChromosome("chr1", It.IsAny<List<PreIndel>>()), Times.Exactly(2));

            Assert.Equal(1.0, categorizedAlignments.Count);
            Assert.Equal(5, categorizedAlignments["chr1"].Count);

            // Nothing realigned, paths should stay the same
            var disagreePaths = categorizedAlignments["chr1"][PairClassification.Disagree];
            Assert.Equal(3, disagreePaths.Count);
            Assert.Equal(Path.Combine("disagreepath1"), disagreePaths.First());

            // Shouldn't have been touched, wasn't realigned
            var perfectPaths = categorizedAlignments["chr1"][PairClassification.PerfectStitched];
            Assert.Equal(1.0, perfectPaths.Count);
            Assert.Equal(Path.Combine("perfect1"), perfectPaths.First());

            // Snowball categories should still output at realigned paths, and have same number as before
            var unstitchIndelPaths = categorizedAlignments["chr1"][PairClassification.UnstitchIndel];
            Assert.Equal(2, unstitchIndelPaths.Count);
            Assert.Equal(Path.Combine("unstitchindel1"), unstitchIndelPaths.First());

        }
    }
}