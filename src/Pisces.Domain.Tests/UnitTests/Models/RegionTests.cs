using System;
using Pisces.Domain.Models;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models
{
    public class RegionTests
    {
        [Fact]
        public void Constructor()
        {
            // happy path
            var testRegion = new Region(1, 6000);
            Assert.Equal(testRegion.StartPosition, 1);
            Assert.Equal(testRegion.EndPosition, 6000);
            Assert.Equal(6000, testRegion.Size);

            // error conditions
            // start position
            Assert.Throws<ArgumentException>(() => new Region(0, 6000)); // zero
            Assert.Throws<ArgumentException>(() => new Region(-100, 6000)); // negative
            // end position
            Assert.Throws<ArgumentException>(() => new Region(1, 0)); // zero
            Assert.Throws<ArgumentException>(() => new Region(1, -100)); // negative
            Assert.Throws<ArgumentException>(() => new Region(6, 5)); // less than start
        }

        [Fact]
        public void Equality()
        {
            // happy path
            var testRegion = new Region(1500, 7002);
            var otherRegion = new Region(1500, 7002);
            Assert.True(testRegion.Equals(otherRegion));
            Assert.True(otherRegion.Equals(testRegion));

            // error conditions
            otherRegion = new Region(1501, 7002); // diff start
            Assert.False(testRegion.Equals(otherRegion));
            otherRegion = new Region(1500, 7001); // diff end
            Assert.False(testRegion.Equals(otherRegion));

            Assert.False(testRegion.Equals("otherobject"));
        }

        [Fact]
        public void IsValid()
        {
            Assert.True(new Region(1, 5, false).IsValid());
            Assert.True(new Region(1, 1, false).IsValid());

            Assert.False(new Region(8, 7, false).IsValid());
            Assert.False(new Region(0, 5, false).IsValid());
            Assert.False(new Region(-2, -1, false).IsValid());
        }

        [Fact]
        public void Contains()
        {
            var region = new Region(1, 5);

            for (var i = 1; i <= 5; i++)
                Assert.True(region.ContainsPosition(i));

            Assert.False(region.ContainsPosition(0));
            Assert.False(region.ContainsPosition(6));
        }

        [Fact]
        public void Union()
        {
            var region = new Region(5, 10);

            Assert.Equal(new Region(5, 10),
                region.Merge(new Region(5, 10)));

            Assert.Equal(new Region(4, 10),
                region.Merge(new Region(4, 5)));

            Assert.Equal(new Region(5, 11),
                region.Merge(new Region(10, 11)));

            // no overlap
            Assert.Equal(null,
                region.Merge(new Region(4, 4)));

            Assert.Equal(null,
                region.Merge(new Region(11, 11)));
        }
 
        [Fact]
        public void Overlaps()
        {
            var region = new Region(5, 10);

            Assert.True(region.Overlaps(new Region(5, 10)));
            Assert.True(region.Overlaps(new Region(6, 10)));
            Assert.True(region.Overlaps(new Region(5, 5)));
            Assert.True(region.Overlaps(new Region(4, 5)));
            Assert.True(region.Overlaps(new Region(10, 11)));

            Assert.False(region.Overlaps(new Region(4, 4)));
            Assert.False(region.Overlaps(new Region(11, 11)));
        }

        [Fact]
        public void FullyContains()
        {
            var region = new Region(5, 10);

            Assert.True(region.FullyContains(new Region(5, 10)));
            Assert.True(region.FullyContains(new Region(6, 10)));
            Assert.True(region.FullyContains(new Region(5, 5)));

            Assert.False(region.FullyContains(new Region(4, 5)));
            Assert.False(region.FullyContains(new Region(10, 11)));
        }
    }
}
