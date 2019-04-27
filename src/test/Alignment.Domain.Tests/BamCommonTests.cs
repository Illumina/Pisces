using Alignment.Domain.Sequencing;
using Xunit;

namespace Alignment.Domain.Tests
{
   public class BamCommonTests
    {

        [Fact]
        ///This unit tests documents current behavior. 
        /// We might want to revisit the default behavior of this constructor.
        /// Its not wrong, but its not very defensive.
        public void TestBamAlignmentConstructor()
        {
            var alignment = new BamAlignment();
            Assert.Equal(null, alignment.Bases);
            Assert.Equal(null, alignment.Qualities);
            Assert.Equal(-1, alignment.MatePosition);
           Assert.Equal(-1, alignment.MateRefID);
            Assert.Equal(0, alignment.RefID);     //tjd, not super happy about this behavior.
            Assert.Equal(0, alignment.Position);  //tjd, not super happy about this behavior.
            Assert.Equal(-1, alignment.EndPosition);
        }
    }

    public class CigarAlignmentTests
    {
        [Fact]
        public void HasIndels()
        {
            var cigar = new CigarAlignment("1M1I5M");
            Assert.True(cigar.HasIndels);

            cigar = new CigarAlignment("1M1D5M");
            Assert.True(cigar.HasIndels);

            cigar = new CigarAlignment("5M");
            Assert.False(cigar.HasIndels);

            cigar.Add(new CigarOp('I', 1));
            Assert.True(cigar.HasIndels);

            cigar.Clear();
            Assert.False(cigar.HasIndels);

            cigar.Add(new CigarOp('I', 1));
            Assert.True(cigar.HasIndels);
        }

        [Fact]
        public void HasSoftclips()
        {
            var cigar = new CigarAlignment("1S5M");
            Assert.True(cigar.HasSoftclips);

            cigar = new CigarAlignment("5M1S");
            Assert.True(cigar.HasSoftclips);

            cigar = new CigarAlignment("5M");
            Assert.False(cigar.HasSoftclips);

            cigar.Add(new CigarOp('S', 1));
            Assert.True(cigar.HasSoftclips);

            cigar.Clear();
            Assert.False(cigar.HasSoftclips);

            cigar.Add(new CigarOp('S', 1));
            Assert.True(cigar.HasSoftclips);
        }
    }
}
   
