//using System.Collections.Generic;
//using Gemini.Interfaces;
//using Gemini.Types;
//using Gemini.Utility;
//using Moq;
//using Xunit;

//namespace Gemini.Tests
//{
//    public class GeminiWorkflowTests
//    {
//        [Fact]
//        public void Execute_VerifySkipRealign()
//        {
//            var mockDataSourceFactory = new Mock<IGeminiDataSourceFactory>();
//            var mockDataOutputFactory = new Mock<IGeminiDataOutputFactory>();
//            var mockGeminiFactory = new Mock<IGeminiFactory>();
//            var geminiOptions = new GeminiOptions(){StitchOnly = true};
//            var geminiSampleOptions = new GeminiSampleOptions() { OutputFolder = "Out" };
//            var realignmentOptions = new RealignmentOptions();

//            var mockRealigner = new Mock<ICategorizedBamRealigner>();
//            mockGeminiFactory.Setup(x => x.GetCategorizedBamRealigner()).Returns(mockRealigner.Object);

//            var samtoolsWrapper = GetSamtoolsWrapper();
//            mockGeminiFactory.Setup(x => x.GetSamtoolsWrapper()).Returns(samtoolsWrapper.Object);

//            var mockEvidenceSource = GetMockEvidenceSource(
//                new Dictionary<string, Dictionary<PairClassification, List<string>>>()
//                {
//                    { "chr1",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}},{PairClassification.FailStitch, new List<string>(){"failstitch"}}}
//                    },
//                    { "chr2",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}},{PairClassification.FailStitch, new List<string>(){"failstitch"}}}
//                    }
//                });
//            mockGeminiFactory.Setup(x => x.GetCategorizationAndEvidenceSource()).Returns(mockEvidenceSource.Object);


//            var workflow = new GeminiWorkflow(mockDataSourceFactory.Object, mockGeminiFactory.Object, mockDataOutputFactory.Object, geminiOptions, geminiSampleOptions, realignmentOptions);
//            workflow.Execute();
//            mockGeminiFactory.Verify(x => x.GetCategorizationAndEvidenceSource(), Times.Once);
//            // Should not try to realign
//            mockRealigner.Verify(x => x.RealignAroundCandidateIndels(It.IsAny<Dictionary<string, List<HashableIndel>>>(), It.IsAny<Dictionary<string, Dictionary<PairClassification, List<string>>>>(), It.IsAny<Dictionary<string, int[]>>()), Times.Never);
//            // Should still do merge
//            VerifySamtoolsCalls(samtoolsWrapper, 3, 1);

//        }
//        [Fact]
//        public void Execute_VerifyMergeAndFinalize()
//        {
//            var mockDataSourceFactory = new Mock<IGeminiDataSourceFactory>();
//            var mockDataOutputFactory = new Mock<IGeminiDataOutputFactory>();
//            var mockGeminiFactory = new Mock<IGeminiFactory>();
//            var geminiOptions = new GeminiOptions();
//            var geminiSampleOptions = new GeminiSampleOptions() {OutputFolder = "Out"};
//            var realignmentOptions = new RealignmentOptions();

//            var mockEvidenceSource =
//                GetMockEvidenceSource(new Dictionary<string, Dictionary<PairClassification, List<string>>>());
            
//            mockGeminiFactory.Setup(x => x.GetCategorizationAndEvidenceSource()).Returns(mockEvidenceSource.Object);

//            var mockRealigner = new Mock<ICategorizedBamRealigner>();
//            mockGeminiFactory.Setup(x => x.GetCategorizedBamRealigner()).Returns(mockRealigner.Object);

//            var samtoolsWrapper = GetSamtoolsWrapper();
//            mockGeminiFactory.Setup(x => x.GetSamtoolsWrapper()).Returns(samtoolsWrapper.Object);

//            var workflow = new GeminiWorkflow(mockDataSourceFactory.Object, mockGeminiFactory.Object, mockDataOutputFactory.Object, geminiOptions, geminiSampleOptions, realignmentOptions);
//            workflow.Execute();

//            mockGeminiFactory.Verify(x => x.GetCategorizationAndEvidenceSource(), Times.Once);
//            mockRealigner.Verify(x => x.RealignAroundCandidateIndels(It.IsAny<Dictionary<string, List<HashableIndel>>>(), It.IsAny<Dictionary<string, Dictionary<PairClassification, List<string>>>>(), It.IsAny<Dictionary<string, int[]>>()), Times.Once);

//            // Nothing to write
//            VerifySamtoolsCalls(samtoolsWrapper, 0, 0);

//            // 1 chromosome, 1 classifications -> chroms * classifications + 0 
//            samtoolsWrapper = GetSamtoolsWrapper();
//            mockGeminiFactory.Setup(x => x.GetSamtoolsWrapper()).Returns(samtoolsWrapper.Object);

//            mockEvidenceSource = GetMockEvidenceSource(
//                new Dictionary<string, Dictionary<PairClassification, List<string>>>()
//                {
//                    { "chr1",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}}}
//                    }
//                });
//            mockGeminiFactory.Setup(x => x.GetCategorizationAndEvidenceSource()).Returns(mockEvidenceSource.Object);

//            workflow = new GeminiWorkflow(mockDataSourceFactory.Object, mockGeminiFactory.Object, mockDataOutputFactory.Object, geminiOptions, geminiSampleOptions, realignmentOptions);
//            workflow.Execute();
//            VerifySamtoolsCalls(samtoolsWrapper, 1, 1);

//            // 1 chromosome, 2 classifications -> num chroms + 0 
//            samtoolsWrapper = GetSamtoolsWrapper();
//            mockGeminiFactory.Setup(x => x.GetSamtoolsWrapper()).Returns(samtoolsWrapper.Object);

//            mockEvidenceSource = GetMockEvidenceSource(
//                new Dictionary<string, Dictionary<PairClassification, List<string>>>()
//                {
//                    { "chr1",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}},{PairClassification.FailStitch, new List<string>(){"failstitch"}}}
//                    }
//                });
//            mockGeminiFactory.Setup(x => x.GetCategorizationAndEvidenceSource()).Returns(mockEvidenceSource.Object);

//            workflow = new GeminiWorkflow(mockDataSourceFactory.Object, mockGeminiFactory.Object, mockDataOutputFactory.Object, geminiOptions, geminiSampleOptions, realignmentOptions);
//            workflow.Execute();
//            VerifySamtoolsCalls(samtoolsWrapper, 1, 1);

//            // 2 chromosomes -> chroms * classifications + 1
//            samtoolsWrapper = GetSamtoolsWrapper();
//            mockGeminiFactory.Setup(x => x.GetSamtoolsWrapper()).Returns(samtoolsWrapper.Object);

//            mockEvidenceSource = GetMockEvidenceSource(
//                new Dictionary<string, Dictionary<PairClassification, List<string>>>()
//                {
//                    { "chr1",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}},{PairClassification.FailStitch, new List<string>(){"failstitch"}}}
//                    },
//                    { "chr2",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}},{PairClassification.FailStitch, new List<string>(){"failstitch"}}}
//                    }
//                });
//            mockGeminiFactory.Setup(x => x.GetCategorizationAndEvidenceSource()).Returns(mockEvidenceSource.Object);

//            workflow = new GeminiWorkflow(mockDataSourceFactory.Object, mockGeminiFactory.Object, mockDataOutputFactory.Object, geminiOptions, geminiSampleOptions, realignmentOptions);
//            workflow.Execute();
//            VerifySamtoolsCalls(samtoolsWrapper, 3, 1);

//            // Don't index - this is a per-chrom execution and therefore assumed to be subprocess unless explicitly configured otherwise
//            geminiSampleOptions.RefId = 1;
//            geminiOptions.IndexPerChrom = false;
//            samtoolsWrapper = GetSamtoolsWrapper();
//            mockGeminiFactory.Setup(x => x.GetSamtoolsWrapper()).Returns(samtoolsWrapper.Object);

//            mockEvidenceSource = GetMockEvidenceSource(
//                new Dictionary<string, Dictionary<PairClassification, List<string>>>()
//                {
//                    { "chr1",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}},{PairClassification.FailStitch, new List<string>(){"failstitch"}}}
//                    },
//                    { "chr2",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}},{PairClassification.FailStitch, new List<string>(){"failstitch"}}}
//                    }
//                });
//            mockGeminiFactory.Setup(x => x.GetCategorizationAndEvidenceSource()).Returns(mockEvidenceSource.Object);

//            workflow = new GeminiWorkflow(mockDataSourceFactory.Object, mockGeminiFactory.Object, mockDataOutputFactory.Object, geminiOptions, geminiSampleOptions, realignmentOptions);
//            workflow.Execute();
//            VerifySamtoolsCalls(samtoolsWrapper, 3, 0);
            
//            // Do index even though it is a per-chrom - indexperchrom is true
//            geminiSampleOptions.RefId = 1;
//            geminiOptions.IndexPerChrom = true;
//            samtoolsWrapper = GetSamtoolsWrapper();
//            mockGeminiFactory.Setup(x => x.GetSamtoolsWrapper()).Returns(samtoolsWrapper.Object);

//            mockEvidenceSource = GetMockEvidenceSource(
//                new Dictionary<string, Dictionary<PairClassification, List<string>>>()
//                {
//                    { "chr1",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}},{PairClassification.FailStitch, new List<string>(){"failstitch"}}}
//                    },
//                    { "chr2",
//                        new Dictionary<PairClassification, List<string>>() {{PairClassification.Disagree, new List<string>(){"disagreepath"}},{PairClassification.FailStitch, new List<string>(){"failstitch"}}}
//                    }
//                });
//            mockGeminiFactory.Setup(x => x.GetCategorizationAndEvidenceSource()).Returns(mockEvidenceSource.Object);

//            workflow = new GeminiWorkflow(mockDataSourceFactory.Object, mockGeminiFactory.Object, mockDataOutputFactory.Object, geminiOptions, geminiSampleOptions, realignmentOptions);
//            workflow.Execute();
//            VerifySamtoolsCalls(samtoolsWrapper, 3, 1);
//        }

//        private Mock<ISamtoolsWrapper> GetSamtoolsWrapper()
//        {
//            var samtoolsWrapper = new Mock<ISamtoolsWrapper>();
//            return samtoolsWrapper;
//        }

//        private void VerifySamtoolsCalls(Mock<ISamtoolsWrapper> samtoolsWrapper, int expectedTimesCatAndSort, int expectedTimesToIndex)
//        {
//            samtoolsWrapper.Verify(x => x.SamtoolsCat(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Exactly(expectedTimesCatAndSort));
//            samtoolsWrapper.Verify(x => x.SamtoolsSort(It.IsAny<string>(),It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Exactly(expectedTimesCatAndSort));
//            samtoolsWrapper.Verify(x => x.SamtoolsIndex(It.IsAny<string>()), Times.Exactly(expectedTimesToIndex));
//        }

//        private Mock<ICategorizedBamAndIndelEvidenceSource> GetMockEvidenceSource(Dictionary<string, Dictionary<PairClassification, List<string>>> categorizedBams)
//        {
//            var mockEvidenceSource = new Mock<ICategorizedBamAndIndelEvidenceSource>();
//            mockEvidenceSource.Setup(x => x.GetCategorizedAlignments())
//                .Returns(categorizedBams);
//            mockEvidenceSource.Setup(x => x.GetIndelStringLookup()).Returns(new Dictionary<string, int[]>());
//            return mockEvidenceSource;

//        }
//    }
//}