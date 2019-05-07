using Pisces.Domain.Models;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models
{
    public class AmpliconCountsTests
    {
        [Fact]
        public void GetAmpliconNameIndexTest()
        {
           string[] threeAmpliconNames = new string[] { "amp1", "amp2", "amp3" };

            Assert.Equal(0, AmpliconCounts.GetAmpliconNameIndex("amp1", threeAmpliconNames).IndexForAmplicon);
            Assert.Equal(1, AmpliconCounts.GetAmpliconNameIndex("amp2", threeAmpliconNames).IndexForAmplicon);
            Assert.Equal(2, AmpliconCounts.GetAmpliconNameIndex("amp3", threeAmpliconNames).IndexForAmplicon);
            Assert.Equal(-1, AmpliconCounts.GetAmpliconNameIndex("cat", threeAmpliconNames).IndexForAmplicon);

            Assert.Equal(-1, AmpliconCounts.GetAmpliconNameIndex("amp1", threeAmpliconNames).NextOpenSlot);
            Assert.Equal(-1, AmpliconCounts.GetAmpliconNameIndex("amp2", threeAmpliconNames).NextOpenSlot);
            Assert.Equal(-1, AmpliconCounts.GetAmpliconNameIndex("amp3", threeAmpliconNames).NextOpenSlot);
            Assert.Equal(-1, AmpliconCounts.GetAmpliconNameIndex("cat", threeAmpliconNames).NextOpenSlot);

            string[] fourAmpliconNames = new string[4];
            fourAmpliconNames[0] = "rover";
            fourAmpliconNames[1] = "fido";

            Assert.Equal(-1, AmpliconCounts.GetAmpliconNameIndex("amp1", fourAmpliconNames).IndexForAmplicon);
            Assert.Equal(-1, AmpliconCounts.GetAmpliconNameIndex("amp2", fourAmpliconNames).IndexForAmplicon);
            Assert.Equal(-1, AmpliconCounts.GetAmpliconNameIndex("amp3", fourAmpliconNames).IndexForAmplicon);
            Assert.Equal(-1, AmpliconCounts.GetAmpliconNameIndex("cat", fourAmpliconNames).IndexForAmplicon);

            Assert.Equal(2, AmpliconCounts.GetAmpliconNameIndex("amp1", fourAmpliconNames).NextOpenSlot);
            Assert.Equal(2, AmpliconCounts.GetAmpliconNameIndex("amp2", fourAmpliconNames).NextOpenSlot);
            Assert.Equal(2, AmpliconCounts.GetAmpliconNameIndex("amp3", fourAmpliconNames).NextOpenSlot);
            Assert.Equal(2, AmpliconCounts.GetAmpliconNameIndex("cat", fourAmpliconNames).NextOpenSlot);

            AmpliconCounts exampleCounts1 = new AmpliconCounts() { AmpliconNames = threeAmpliconNames };
            Assert.Equal(0, exampleCounts1.GetAmpliconNameIndex("amp1").IndexForAmplicon);
            Assert.Equal(1, exampleCounts1.GetAmpliconNameIndex("amp2").IndexForAmplicon);
            Assert.Equal(2, exampleCounts1.GetAmpliconNameIndex("amp3").IndexForAmplicon);
            Assert.Equal(-1, exampleCounts1.GetAmpliconNameIndex("cat").IndexForAmplicon);
            Assert.Equal(-1, exampleCounts1.GetAmpliconNameIndex("amp1").NextOpenSlot);

            AmpliconCounts exampleCounts2 = new AmpliconCounts() { AmpliconNames = fourAmpliconNames };
            Assert.Equal(-1, exampleCounts2.GetAmpliconNameIndex("amp1").IndexForAmplicon);
            Assert.Equal(-1, exampleCounts2.GetAmpliconNameIndex("amp2").IndexForAmplicon);
            Assert.Equal(-1, exampleCounts2.GetAmpliconNameIndex("amp3").IndexForAmplicon);
            Assert.Equal(-1, exampleCounts2.GetAmpliconNameIndex("cat").IndexForAmplicon);
            Assert.Equal(2, exampleCounts2.GetAmpliconNameIndex("amp1").NextOpenSlot);
        }

        [Fact]
        public void GetCoverageForAmpliconTest()
        {
            string[] ampliconNames = new string[] { "amp1", "amp2", "amp3","","" };
            int[] ampliconCounts = new int[] { 10, 0, 3,0,0 };
            AmpliconCounts exampleCounts = new AmpliconCounts() { AmpliconNames = ampliconNames , CountsForAmplicon = ampliconCounts };

            Assert.Equal(10, exampleCounts.GetCountsForAmplicon("amp1"));
            Assert.Equal(0, exampleCounts.GetCountsForAmplicon("amp2"));
            Assert.Equal(3, exampleCounts.GetCountsForAmplicon("amp3"));
            Assert.Equal(0, exampleCounts.GetCountsForAmplicon(""));
            Assert.Equal(0, exampleCounts.GetCountsForAmplicon("foo"));
        }

        [Fact]
        public void GetEmptySummaryForAmpliconTest()
        {
            AmpliconCounts exampleCounts = AmpliconCounts.GetEmptyAmpliconCounts();

            Assert.Equal(Constants.MaxNumOverlappingAmplicons, exampleCounts.AmpliconNames.Length);
            Assert.Equal(Constants.MaxNumOverlappingAmplicons, exampleCounts.CountsForAmplicon.Length);

            Assert.Equal(0, exampleCounts.GetCountsForAmplicon("amp1"));
            Assert.Equal(0, exampleCounts.GetCountsForAmplicon("amp2"));
            Assert.Equal(0, exampleCounts.GetCountsForAmplicon("amp3"));
            Assert.Equal(0, exampleCounts.GetCountsForAmplicon(""));
            Assert.Equal(0, exampleCounts.GetCountsForAmplicon("foo"));
        }

        [Fact]
        public void CopyForAmpliconTest()
        {
            string[] ampliconNames = new string[] { "amp1", "amp2", "amp3", "", "" };
            int[] ampliconCounts = new int[] { 10, 0, 3, 0, 0 };
            AmpliconCounts exampleCounts = new AmpliconCounts() { AmpliconNames = ampliconNames, CountsForAmplicon = ampliconCounts };

            var newCounts = exampleCounts.Copy();
            Assert.NotEqual(exampleCounts, newCounts);
            Assert.Equal(10, newCounts.GetCountsForAmplicon("amp1"));
            Assert.Equal(0, newCounts.GetCountsForAmplicon("amp2"));
            Assert.Equal(3, newCounts.GetCountsForAmplicon("amp3"));
            Assert.Equal(0, newCounts.GetCountsForAmplicon(""));
            Assert.Equal(0, newCounts.GetCountsForAmplicon("foo"));
        }
    }
}
