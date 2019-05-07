using System.Collections.Concurrent;
using System.Collections.Generic;
using Gemini.IndelCollection;
using Moq;
using ReadRealignmentLogic.Models;
using Xunit;

namespace Gemini.Tests
{
    public class OutcomesWriterTests
    {
        [Fact]
        public void WriteIndelOutcomesFile()
        {
            var hashables = new ConcurrentDictionary<HashableIndel, int[]>();

            var lines = VerifyIndelOutcomesFile(hashables, 1);
            
            hashables = new ConcurrentDictionary<HashableIndel, int[]>();
            var hashable = new HashableIndel()
            {
                ReferencePosition = 100,
                ReferenceAllele = "A",
                AlternateAllele = "T",
                Chromosome = "chr1"
            };
            var hashable2 = new HashableIndel()
            {
                ReferencePosition = 1000,
                ReferenceAllele = "A",
                AlternateAllele = "T",
                Chromosome = "chr1"
            };
            hashables[hashable] = new int[]{0,1,2,3,4,5};
            hashables[hashable2] = new int[] { 0, 1, 2, 3, 4, 5 };

            lines = VerifyIndelOutcomesFile(hashables, 3);
        }

        private static List<string> VerifyIndelOutcomesFile(ConcurrentDictionary<HashableIndel, int[]> hashables, int expectedLines)
        {
            var indelOutcomes = new List<string>();
            var finalIndels = new List<string>();
            var categoryOutcomes = new List<string>();
            var mockFactory = new Mock<IGeminiDataOutputFactory>();
            var indelOutcomesWriter = MockTextWriter(indelOutcomes);
            var finalIndelsWriter = MockTextWriter(finalIndels);
            var categoryOutcomesWriter = MockTextWriter(categoryOutcomes);
            mockFactory.Setup(x => x.GetTextWriter(It.Is<string>(y => y.EndsWith("IndelOutcomes.csv"))))
                .Returns(indelOutcomesWriter.Object);
            mockFactory.Setup(x => x.GetTextWriter(It.Is<string>(y => y.EndsWith("FinalIndels.csv"))))
                .Returns(finalIndelsWriter.Object);
            mockFactory.Setup(x => x.GetTextWriter(It.Is<string>(y => y.EndsWith("CategoryOutcomes.csv"))))
                .Returns(categoryOutcomesWriter.Object);

            var outcomeWriter = new OutcomesWriter("outdir", mockFactory.Object);
            outcomeWriter.WriteIndelOutcomesFile(hashables);
            VerifyNumberOfLines(categoryOutcomes, categoryOutcomesWriter, 0);
            VerifyNumberOfLines(indelOutcomes, indelOutcomesWriter, expectedLines);
            VerifyNumberOfLines(finalIndels, finalIndelsWriter, 0);
            return indelOutcomes;
        }

        [Fact]
        public void WriteIndelsFile()
        {
            var hashables = new ConcurrentDictionary<HashableIndel, int>();

            var lines = VerifyWriteIndelsFile(hashables, 1);

            hashables = new ConcurrentDictionary<HashableIndel, int>();
            var hashable = new HashableIndel()
            {
                ReferencePosition = 100, ReferenceAllele = "A", AlternateAllele = "T", Chromosome = "chr1"
            };
            hashables[hashable] = 10;
            lines = VerifyWriteIndelsFile(hashables, 2);
        }

        private static List<string> VerifyWriteIndelsFile(ConcurrentDictionary<HashableIndel, int> hashables, int expected)
        {
            var indelOutcomes = new List<string>();
            var finalIndels = new List<string>();
            var categoryOutcomes = new List<string>();
            var mockFactory = new Mock<IGeminiDataOutputFactory>();
            var indelOutcomesWriter = MockTextWriter(indelOutcomes);
            var finalIndelsWriter = MockTextWriter(finalIndels);
            var categoryOutcomesWriter = MockTextWriter(categoryOutcomes);
            mockFactory.Setup(x => x.GetTextWriter(It.Is<string>(y => y.EndsWith("IndelOutcomes.csv"))))
                .Returns(indelOutcomesWriter.Object);
            mockFactory.Setup(x => x.GetTextWriter(It.Is<string>(y => y.EndsWith("FinalIndels.csv"))))
                .Returns(finalIndelsWriter.Object);
            mockFactory.Setup(x => x.GetTextWriter(It.Is<string>(y => y.EndsWith("CategoryOutcomes.csv"))))
                .Returns(categoryOutcomesWriter.Object);

            var outcomeWriter = new OutcomesWriter("outdir", mockFactory.Object);
            outcomeWriter.WriteIndelsFile(hashables);
            // Header only
            VerifyNumberOfLines(finalIndels, finalIndelsWriter, expected);
            VerifyNumberOfLines(indelOutcomes, indelOutcomesWriter, 0);
            VerifyNumberOfLines(categoryOutcomes, categoryOutcomesWriter, 0);
            return finalIndels;
        }

        [Fact]
        public void WriteCategoryOutcomesFile()
        {
            var hashables = new ConcurrentDictionary<HashableIndel, int>();
            var tracker = new ConcurrentDictionary<string, int>();
            var indels = new ConcurrentDictionary<string, IndelEvidence>();

            var lines = VerifyCategoryOutcomesFile(tracker, 1);

            tracker["Disagree:Outcome_For_Disagree"] = 1;
            lines = VerifyCategoryOutcomesFile(tracker, 2);
        }

        private static List<string> VerifyCategoryOutcomesFile(ConcurrentDictionary<string, int> tracker, int expectedLines)
        {
            var indelOutcomes = new List<string>();
            var finalIndels = new List<string>();
            var categoryOutcomes = new List<string>();
            var mockFactory = new Mock<IGeminiDataOutputFactory>();
            var indelOutcomesWriter = MockTextWriter(indelOutcomes);
            var finalIndelsWriter = MockTextWriter(finalIndels);
            var categoryOutcomesWriter = MockTextWriter(categoryOutcomes);
            mockFactory.Setup(x => x.GetTextWriter(It.Is<string>(y => y.EndsWith("IndelOutcomes.csv"))))
                .Returns(indelOutcomesWriter.Object);
            mockFactory.Setup(x => x.GetTextWriter(It.Is<string>(y => y.EndsWith("FinalIndels.csv"))))
                .Returns(finalIndelsWriter.Object);
            mockFactory.Setup(x => x.GetTextWriter(It.Is<string>(y => y.EndsWith("CategoryOutcomes.csv"))))
                .Returns(categoryOutcomesWriter.Object);

            var outcomeWriter = new OutcomesWriter("outdir", mockFactory.Object);
            outcomeWriter.CategorizeProgressTrackerAndWriteCategoryOutcomesFile(tracker);

            VerifyNumberOfLines(categoryOutcomes, categoryOutcomesWriter, expectedLines);
            VerifyNumberOfLines(indelOutcomes, indelOutcomesWriter, 0);
            VerifyNumberOfLines(finalIndels, finalIndelsWriter, 0);

            return categoryOutcomes;
        }

        private static void VerifyNumberOfLines(List<string> indelOutcomes, Mock<ITextWriter> indelOutcomesWriter, int numExpectedWrites)
        {
            Assert.Equal(numExpectedWrites, indelOutcomes.Count);
            indelOutcomesWriter.Verify(x => x.WriteLine(It.IsAny<string>()), Times.Exactly(numExpectedWrites));
        }

        private static Mock<ITextWriter> MockTextWriter(List<string> mockLines)
        {
            var mockTextWriter = new Mock<ITextWriter>();
            mockTextWriter.Setup(x => x.WriteLine(It.IsAny<string>())).Callback<string>(s => { mockLines.Add(s); });
            return mockTextWriter;
        }
    }
}