//using System.IO;
//using Alignment.IO.Sequencing;
//using Common.IO.Sequencing;
//using Alignment.Domain.Sequencing;
//using Moq;
//using Xunit;
//using System.Collections.Generic;

//TODO comment out for .net core issues
//namespace Alignment.IO.Tests
//{
//    public class BamWriterInMemTests
//    {
//        [Fact]
//        public void TestMerge()
//        {
//            BamAlignment bamAlignment = new BamAlignment()
//            {
//                Bases = "ACGT",
//                Bin = 0,
//                CigarData = new CigarAlignment("4M"),
//                Name = "Should have a constructor which initializes the members",
//                Position = 1,
//                Qualities = new byte[4],
//                TagData = new byte[4]
//            };

//            List<BamAlignment> bamAlignments = new List<BamAlignment>();
//            bamAlignments.Add(new BamAlignment(bamAlignment));

//            bamAlignment.Position = 2;
//            bamAlignments.Add(new BamAlignment(bamAlignment));

//            bamAlignment.Position = 10;
//            bamAlignments.Add(new BamAlignment(bamAlignment));

//            bamAlignment.Position = 11;
//            bamAlignments.Add(new BamAlignment(bamAlignment));

//            MemoryStream memoryBuffer = new MemoryStream();

//            var str = new Mock<MemoryStream>();
//            str.Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((buffer, offset, count) =>
//                { memoryBuffer.Write(buffer, offset, count); });
//            str.SetupGet(x => x.CanWrite).Returns(true);

//            using (BamWriterInMem bamWriter = new BamWriterInMem(
//                   str.Object,
//                   "",
//                   new System.Collections.Generic.List<GenomeMetadata.SequenceMetadata>(),
//                   0.5f,
//                   2)) // 2 threads
//            {
//                var handles = bamWriter.GenerateHandles();

//                // Write 2 alignments on the first handle
//                // The positions are 1 and 2
//                handles[0].WriteAlignment(bamAlignments[0]);
//                handles[0].WriteAlignment(bamAlignments[1]);

//                // Write 2 alignments on the second handle
//                // The positions are 10 and 11
//                handles[1].WriteAlignment(bamAlignments[2]);
//                handles[1].WriteAlignment(bamAlignments[3]);

//                // This will sort and merge the alignments, and write the results to the stream
//                bamWriter.SortAndWrite();
//            }

//            memoryBuffer.Position = 0;
//            BamReader bamReader = new BamReader();
//            bamReader.Open(memoryBuffer);

//            // Verify that all BamAlignment objects are found
//            // and they are in the right order.
//            for (int i = 0; i < 4; ++i)
//            {
//                BamAlignment al = new BamAlignment();
//                Assert.True(bamReader.GetNextAlignment(ref al, false));
//                Assert.Equal(al.Position, bamAlignments[i].Position);
//            }
//        }
//    }
//}
