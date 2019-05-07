using System.Collections.Generic;
using Alignment.IO;
using Gemini.IO;
using Gemini.Types;
using Moq;
using Xunit;

namespace Gemini.Tests
{
    public class WriterSourceRecycledTests
    {
        [Fact]
        public void BamWriterHandle()
        {
            var writerFactory = new Mock<IBamWriterFactory>();
            var writers = new List<Mock<IBamWriterHandle>>();
            writerFactory.Setup(x => x.CreateSingleBamWriter(It.IsAny<string>())).Returns<string>( s =>
            {
                var bamWriter = new Mock<IBamWriterHandle>();
                writers.Add(bamWriter);
                return bamWriter.Object;
            });
            var writerSource = new WriterSourceRecycled("outbam", writerFactory.Object);

            var handle1 = writerSource.BamWriterHandle("chr1", PairClassification.Disagree, 1);
            writerFactory.Verify(x=>x.CreateSingleBamWriter(It.IsAny<string>()), Times.Exactly(1));

            var handle2 = writerSource.BamWriterHandle("chr1", PairClassification.Disagree, 1);
            writerFactory.Verify(x => x.CreateSingleBamWriter(It.IsAny<string>()), Times.Exactly(2));
            Assert.NotEqual(handle1, handle2);

            writerSource.DoneWithWriter("chr1", PairClassification.Disagree, 1, 10, handle1);

            // Handle1 should be recycled
            var handle3 = writerSource.BamWriterHandle("chr1", PairClassification.Disagree, 1);
            writerFactory.Verify(x => x.CreateSingleBamWriter(It.IsAny<string>()), Times.Exactly(2));
            Assert.Equal(handle1, handle3);

        }

        [Fact]
        public void Finish()
        {
            var writerFactory = new Mock<IBamWriterFactory>();
            var writers = new List<Mock<IBamWriterHandle>>();
            writerFactory.Setup(x => x.CreateSingleBamWriter(It.IsAny<string>())).Returns<string>(s =>
            {
                var bamWriter = new Mock<IBamWriterHandle>();
                writers.Add(bamWriter);
                return bamWriter.Object;
            });
            var writerSource = new WriterSourceRecycled("outbam", writerFactory.Object);

            var handle1 = writerSource.BamWriterHandle("chr1", PairClassification.Disagree, 1);
            writerFactory.Verify(x => x.CreateSingleBamWriter(It.IsAny<string>()), Times.Exactly(1));

            var handle2 = writerSource.BamWriterHandle("chr1", PairClassification.Disagree, 1);
            writerFactory.Verify(x => x.CreateSingleBamWriter(It.IsAny<string>()), Times.Exactly(2));

            Assert.Equal(2, writers.Count);
            foreach (var writer in writers)
            {
                writer.Verify(x=>x.WriteAlignment(null), Times.Never);
            }
            writerSource.Finish();
            foreach (var writer in writers)
            {
                writer.Verify(x => x.WriteAlignment(null), Times.Once);
            }

            Assert.Equal(2, writerSource.GetBamFiles().Count);

        }

    }
}