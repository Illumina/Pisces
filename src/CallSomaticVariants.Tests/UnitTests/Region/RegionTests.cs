using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CallSomaticVariants.Logic.RegionState;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Region
{
    public class RegionTests
    {
        [Fact]
        public void Constructor()
        {
            // happy path
            var testRegion = new RegionState(1, 6000);
            Assert.Equal(testRegion.StartPosition, 1);
            Assert.Equal(testRegion.EndPosition, 6000);
            Assert.Equal(6000, testRegion.Size);

            // error conditions
            // start position
            Assert.Throws<ArgumentException>(() => new RegionState(0, 6000)); // zero
            Assert.Throws<ArgumentException>(() => new RegionState(-100, 6000)); // negative
            // end position
            Assert.Throws<ArgumentException>(() => new RegionState(1, 0)); // zero
            Assert.Throws<ArgumentException>(() => new RegionState(1, -100)); // negative
            Assert.Throws<ArgumentException>(() => new RegionState(1, 1)); // equal start
            Assert.Throws<ArgumentException>(() => new RegionState(6, 1)); // less than start
        }

        [Fact]
        public void Equality()
        {
            // happy path
            var testRegion = new RegionState(1500, 7002);
            var otherRegion = new RegionState(1500, 7002);
            Assert.True(testRegion.Equals(otherRegion));
            Assert.True(otherRegion.Equals(testRegion));

            // error conditions
            otherRegion = new RegionState(1501, 7002); // diff start
            Assert.False(testRegion.Equals(otherRegion));
            otherRegion = new RegionState(1500, 7001); // diff end
            Assert.False(testRegion.Equals(otherRegion));

            Assert.False(testRegion.Equals("otherobject"));
        }

        [Fact]
        public void IsValid()
        {
            Assert.True(new CallSomaticVariants.Logic.RegionState.Region(1, 5).IsValid());
            Assert.True(new CallSomaticVariants.Logic.RegionState.Region(1, 1).IsValid());

            Assert.False(new CallSomaticVariants.Logic.RegionState.Region(8, 7).IsValid());
            Assert.False(new CallSomaticVariants.Logic.RegionState.Region(0, 5).IsValid());
            Assert.False(new CallSomaticVariants.Logic.RegionState.Region(-2, -1).IsValid());
        }

        [Fact]
        public void Contains()
        {
            var region = new RegionState(1, 5);

            for (var i = 1; i <= 5; i++)
                Assert.True(region.ContainsPosition(i));

            Assert.False(region.ContainsPosition(0));
            Assert.False(region.ContainsPosition(6));
        }

        [Fact]
        public void Union()
        {
            var region = new CallSomaticVariants.Logic.RegionState.Region(5, 10);

            Assert.Equal(new CallSomaticVariants.Logic.RegionState.Region(5, 10),
                region.Merge(new CallSomaticVariants.Logic.RegionState.Region(5, 10)));

            Assert.Equal(new CallSomaticVariants.Logic.RegionState.Region(4, 10),
                region.Merge(new CallSomaticVariants.Logic.RegionState.Region(4, 5)));

            Assert.Equal(new CallSomaticVariants.Logic.RegionState.Region(5, 11),
                region.Merge(new CallSomaticVariants.Logic.RegionState.Region(10, 11)));

            // no overlap
            Assert.Equal(null,
                region.Merge(new CallSomaticVariants.Logic.RegionState.Region(4, 4)));

            Assert.Equal(null,
                region.Merge(new CallSomaticVariants.Logic.RegionState.Region(11, 11)));
        }
 
        [Fact]
        public void Overlaps()
        {
            var region = new CallSomaticVariants.Logic.RegionState.Region(5, 10);

            Assert.True(region.Overlaps(new CallSomaticVariants.Logic.RegionState.Region(5, 10)));
            Assert.True(region.Overlaps(new CallSomaticVariants.Logic.RegionState.Region(6, 10)));
            Assert.True(region.Overlaps(new CallSomaticVariants.Logic.RegionState.Region(5, 5)));
            Assert.True(region.Overlaps(new CallSomaticVariants.Logic.RegionState.Region(4, 5)));
            Assert.True(region.Overlaps(new CallSomaticVariants.Logic.RegionState.Region(10, 11)));

            Assert.False(region.Overlaps(new CallSomaticVariants.Logic.RegionState.Region(4, 4)));
            Assert.False(region.Overlaps(new CallSomaticVariants.Logic.RegionState.Region(11, 11)));
        }

        [Fact]
        public void FullyContains()
        {
            var region = new CallSomaticVariants.Logic.RegionState.Region(5, 10);

            Assert.True(region.FullyContains(new CallSomaticVariants.Logic.RegionState.Region(5, 10)));
            Assert.True(region.FullyContains(new CallSomaticVariants.Logic.RegionState.Region(6, 10)));
            Assert.True(region.FullyContains(new CallSomaticVariants.Logic.RegionState.Region(5, 5)));

            Assert.False(region.FullyContains(new CallSomaticVariants.Logic.RegionState.Region(4, 5)));
            Assert.False(region.FullyContains(new CallSomaticVariants.Logic.RegionState.Region(10, 11)));
        }
    }
}
