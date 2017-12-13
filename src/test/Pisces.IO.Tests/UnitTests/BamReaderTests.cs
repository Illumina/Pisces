using System.IO;
using Alignment.IO.Sequencing;
using Alignment.Domain.Sequencing;
using Xunit;

namespace Pisces.IO.Tests.UnitTests
{
    public class BamReaderTests
    {
        [Fact]
        public void TestJump()
        {
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "bwaXC.bam");
            using (var reader = new BamReader(smallBam))
            {
                BamAlignment al = new BamAlignment();

                Assert.True(reader.Jump(reader.GetReferenceIndex("chr1"), 20200));
                Assert.True(reader.GetNextAlignment(ref al, true));
                Assert.True(al.Position > 18000);
                Assert.True(reader.Jump(reader.GetReferenceIndex("chr1"), 200));
                Assert.True(reader.GetNextAlignment(ref al, true));
                Assert.True(al.Position < 250);

                // now, forward-only jumping
                Assert.True(reader.JumpForward(reader.GetReferenceIndex("chr1"), 20200));
                Assert.True(reader.GetNextAlignment(ref al, true));
                Assert.True(al.Position > 18000); // a good forward jump
                var position = reader.Tell();
                Assert.True(reader.JumpForward(reader.GetReferenceIndex("chr1"), 200));
                Assert.Equal(position, reader.Tell()); // we stayed put
                Assert.True(reader.GetNextAlignment(ref al, true));
                Assert.True(al.Position > 18000); 
                
            }
        }
    }
}
