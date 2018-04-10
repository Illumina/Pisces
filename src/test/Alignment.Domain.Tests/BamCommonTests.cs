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
}
   
