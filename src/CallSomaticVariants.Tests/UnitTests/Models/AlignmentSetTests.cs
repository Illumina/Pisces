using System;
using CallSomaticVariants.Models;
using CallSomaticVariants.Tests.Utilities;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Models
{
    public class AlignmentSetTests
    {
        [Fact]
        public void Constructor()
        {
            var read1 = TestHelper.CreateRead("chr1", "ACT", 100);
            var read2 = TestHelper.CreateRead("chr1", "TCCT", 200);
            var alignmentSet = new AlignmentSet(read1, read2);

            Assert.Equal(read1, alignmentSet.PartnerRead1);
            Assert.Equal(read2, alignmentSet.PartnerRead2);
            Assert.True(alignmentSet.IsFullPair);

            // read1 must be provided
            Assert.Throws<ArgumentException>(() => new AlignmentSet(null, read1));

            // allow single read
            alignmentSet = new AlignmentSet(read1, null); 
            Assert.Equal(read1, alignmentSet.PartnerRead1);
            Assert.Equal(null, alignmentSet.PartnerRead2);
            Assert.False(alignmentSet.IsFullPair);

            // make sure reads are ordered
            alignmentSet = new AlignmentSet(read2, read1);
            Assert.Equal(read1, alignmentSet.PartnerRead1);
            Assert.Equal(read2, alignmentSet.PartnerRead2);
            Assert.True(alignmentSet.IsFullPair);
        }
    }
}
