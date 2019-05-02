using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Alignment.IO.Sequencing;
using Gemini.Interfaces;
using Gemini.Realignment;
using Gemini.Types;
using Moq;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using StitchingLogic;

namespace Gemini.Tests
{
    public static class DataflowMocks
    {
    
        public static Mock<IGeminiDataOutputFactory> MockDataOutputFactory(List<BamAlignment> alignments)
        {
            var mockWriter = new Mock<IBamWriterHandle>();
            mockWriter.Setup(x => x.WriteAlignment(It.IsAny<BamAlignment>())).Callback<BamAlignment>(b => alignments.Add(b));
            var mockDataOutputFactory = new Mock<IGeminiDataOutputFactory>();
            var mockWriterSource = new Mock<IWriterSource>();
            mockWriterSource
                .Setup(x => x.BamWriterHandle(It.IsAny<string>(), It.IsAny<PairClassification>(), It.IsAny<int>()))
                .Returns(mockWriter.Object);
            mockWriterSource.Setup(x => x.GetBamFiles()).Returns(new List<string>() { });
            mockDataOutputFactory.Setup(x => x.GetWriterSource(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockWriterSource.Object);
            var mockTextWriter = new Mock<ITextWriter>();
            mockDataOutputFactory.Setup(x => x.GetTextWriter(It.IsAny<string>())).Returns(mockTextWriter.Object);
            return mockDataOutputFactory;
        }

        public static Mock<IGeminiDataSourceFactory> MockDataSourceFactory(Mock<IBamReader> mockReader, Mock<IDataSource<ReadPair>> mockReadPairSource)
        {
            var mockDataSourceFactory = new Mock<IGeminiDataSourceFactory>();
            mockDataSourceFactory.Setup(x => x.CreateBamReader(It.IsAny<string>())).Returns(mockReader.Object);
            mockDataSourceFactory.Setup(x => x.CreateReadPairSource(It.IsAny<IBamReader>(), It.IsAny<ReadStatusCounter>()))
                .Returns(mockReadPairSource.Object);
            mockDataSourceFactory.Setup(x => x.GetRefIdMapping(It.IsAny<string>()))
                .Returns(new Dictionary<int, string>() { { 1, "chr1" }, { 2, "chr2" }, { -1, "Unknown" } });
            var mockSnippetSource = new Mock<IGenomeSnippetSource>();
            var genomeSnippet = new GenomeSnippet()
            { Chromosome = "chr1", Sequence = new string('A', 1000000), StartPosition = 0 };
            mockSnippetSource.Setup(x => x.GetGenomeSnippet(It.IsAny<int>())).Returns(genomeSnippet);
            mockDataSourceFactory.Setup(x => x.CreateGenomeSnippetSource(It.IsAny<string>(), It.IsAny<ChrReference>(), It.IsAny<int>()))
                .Returns(mockSnippetSource.Object);
            mockDataSourceFactory
                .Setup(x => x.GetChromosomeIndelSource(It.IsAny<List<HashableIndel>>(),
                    It.IsAny<IGenomeSnippetSource>())).Returns<List<HashableIndel>, IGenomeSnippetSource>((x, y) => new ChromosomeIndelSource(x, y));
            return mockDataSourceFactory;
        }

        public static Mock<IBamReader> MockReader()
        {
            var mockReader = new Mock<IBamReader>();
            mockReader.Setup(x => x.GetReferenceNames()).Returns(new List<string>() { "chr1", "chr2" });
            mockReader.Setup(x => x.GetReferenceIndex("chr1")).Returns(1);
            mockReader.Setup(x => x.GetReferenceIndex("chr2")).Returns(2);
            return mockReader;
        }

        public static Mock<IDataSource<ReadPair>> MockDataSource(List<ReadPair> readPairs, List<ReadPair> lonerPairs = null)
        {
            var mockDataSource = new Mock<IDataSource<ReadPair>>();
            var i = 0;
            mockDataSource.Setup(x => x.GetNextEntryUntilNull())
                .Returns(() => { return i < readPairs.Count ? readPairs[i] : null; }).Callback(() => i++);
            mockDataSource.Setup(x => x.GetWaitingEntries(It.IsAny<int>())).Returns<int>(upToPos =>
            {
                var readsToFlush = new List<ReadPair>();
                if (lonerPairs == null)
                {
                    return readsToFlush;
                }
                foreach (var p in lonerPairs)
                {
                    if (p.MinPosition <= upToPos || upToPos < 0)
                    {
                        readsToFlush.Add(p);
                    }
                }

                foreach (var p in readsToFlush)
                {
                    lonerPairs.Remove(p);
                }

                return readsToFlush;
            });

            //mockDataSource.Setup(x => x.GetWaitingEntries(It.IsAny<int>())).Returns<int>(upToPos =>
            //    lonerPairs != null && lonerPairs.ContainsKey(upToPos) ? lonerPairs[upToPos] : new List<ReadPair>());
            return mockDataSource;
        }
    }
}