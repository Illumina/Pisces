using System.IO;
using Alignment.IO.Sequencing;
using Common.IO.Sequencing;
using Alignment.Domain.Sequencing;
using Moq;
using Xunit;
using System.Collections.Generic;

namespace Alignment.IO.Tests
{
    public class BamWriterMultithreadedTests
    {
        [Fact]
        public void TestMultithreaded()
        {
            //Assert.True(false, "looks like running this causes Alignment.IO.Tests.dll to lock");

            //Severity Code    Description Project File Line    Suppression State
            //Error C:\Projects_tdunn\git\Pisces5_Ent\Pisces5\test\Alignment.IO.Tests\error CS2012: Cannot open 'C:\Projects_tdunn\git\Pisces5_Ent\Pisces5\test\Alignment.IO.Tests\bin\Debug\netcoreapp1.0\Alignment.IO.Tests.dll' for writing-- 'The process cannot access the file 'C:\Projects_tdunn\git\Pisces5_Ent\Pisces5\test\Alignment.IO.Tests\bin\Debug\netcoreapp1.0\Alignment.IO.Tests.dll' because it is being used by another process.'  Alignment.IO.Tests  C:\Program Files(x86)\MSBuild\Microsoft\VisualStudio\v14.0\DotNet\Microsoft.DotNet.Common.Targets  262



          BamAlignment bamAlignment = new BamAlignment()
            {
                Bases = "ACGT",
                Bin = 0,
                CigarData = new CigarAlignment("4M"),
                Name = "Should have a constructor which initializes the members",
                Position = 1,
                Qualities = new byte[4],
                TagData = new byte[4]
            };

            List<BamAlignment> bamAlignments = new List<BamAlignment>();
            bamAlignments.Add(new BamAlignment(bamAlignment));

            bamAlignment.Position = 2;
            bamAlignments.Add(new BamAlignment(bamAlignment));

            bamAlignment.Position = 10;
            bamAlignments.Add(new BamAlignment(bamAlignment));

            bamAlignment.Position = 11;
            bamAlignments.Add(new BamAlignment(bamAlignment));

            MemoryStream memoryBuffer = new MemoryStream();

            var str = new Mock<MemoryStream>();
            str.Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((buffer, offset, count) =>
            { memoryBuffer.Write(buffer, offset, count); });
            str.SetupGet(x => x.CanWrite).Returns(true);

            using (var bamWriter = new BamWriterMultithreaded(
                   str.Object,
                   "",
                   new System.Collections.Generic.List<GenomeMetadata.SequenceMetadata>(),
                   2)) // 2 threads
            {
                var handles = bamWriter.GenerateHandles();

                // Write 2 alignments on the first handle
                // The positions are 1 and 10
                handles[0].WriteAlignment(bamAlignments[0]);
                handles[0].WriteAlignment(bamAlignments[2]);

                // Write 2 alignments on the second handle
                // The positions are 2 and 11
                handles[1].WriteAlignment(bamAlignments[1]);
                handles[1].WriteAlignment(bamAlignments[3]);

                // This will sort and merge the alignments, and write the results to the stream
                bamWriter.Flush();
            }

            memoryBuffer.Position = 0;
            BamReader bamReader = new BamReader();
            bamReader.Open(memoryBuffer);

            var bamAlignmentsWritten = new List<BamAlignment>();

            // Verify that all BamAlignment objects are found
            // and they are in the right order.
            for (int i = 0; i < 4; ++i)
            {
                BamAlignment al = new BamAlignment();
                Assert.True(bamReader.GetNextAlignment(ref al, false));

                bamAlignmentsWritten.Add(new BamAlignment(al));
            }

            bamReader.Close();
            bamReader.Dispose();

            bamAlignmentsWritten.Sort((al1, al2) => (al1.Position.CompareTo(al2.Position)));
            for (int i = 0; i < 4; ++i)
            {
                Assert.Equal(bamAlignmentsWritten[i].Position, bamAlignments[i].Position);
            }
        }
    }
}