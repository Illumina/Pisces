using System;
using System.Collections.Generic;
using Pisces.Domain.Models;
using Xunit;
using BasicRegion = Pisces.Domain.Models.Region;

namespace Pisces.Domain.Tests.UnitTests.Models
{
    public class IntervalSetTests
    {
        [Fact]
        public void Constructor()
        {
            //Happy Path
            var intervalSet = new ChrIntervalSet(new List<BasicRegion>()
            {
                new BasicRegion(4, 6),
                new BasicRegion(8, 8),
                new BasicRegion(9, 10)
            }, "chr1");

            Assert.Equal(4, intervalSet.MinPosition);
            Assert.Equal(10, intervalSet.MaxPosition);
            Assert.Equal("chr1", intervalSet.ChrName);

            intervalSet = new ChrIntervalSet(new List<BasicRegion>(), "chr1");

            Assert.Equal(0, intervalSet.MinPosition);
            Assert.Equal(0, intervalSet.MaxPosition);

            //Null Intervals
            Assert.Throws<ArgumentException>(() => new ChrIntervalSet(null, "chr1"));
            //Null Chromosome Name
            Assert.Throws<ArgumentException>(() => new ChrIntervalSet(new List<BasicRegion>(), null));
            //Empty Chromosome Name
            Assert.Throws<ArgumentException>(() => new ChrIntervalSet(new List<BasicRegion>(), ""));
            //Invalid Interval - StartPosition > End Position
            Assert.Throws<ArgumentException>(() => new ChrIntervalSet(new List<BasicRegion>()
            {
                new BasicRegion(7, 6)
            }, "chr1"));
            //Invalid Interval - StartPosition <= 0
            Assert.Throws<ArgumentException>(() => new ChrIntervalSet(new List<BasicRegion>()
            {
                new BasicRegion(0, 6)
            }, "chr1"));
            //Invalid Interval - EndPosition <= 0
            Assert.Throws<ArgumentException>(() => new ChrIntervalSet(new List<BasicRegion>()
            {
                new BasicRegion(7, 0)
            }, "chr1"));
            
        }

        [Fact]
        public void SortAndCollapse()
        {
            // happy path - good regions
            ExecuteTest_SortAndCollapse(new ChrIntervalSet(
                new List<BasicRegion>()
                {
                    new BasicRegion(4, 6),
                    new BasicRegion(8, 8),
                    new BasicRegion(9, 10)
                }, "chr1"),
                new List<BasicRegion>()
                {
                    new BasicRegion(4, 6),
                    new BasicRegion(8, 8),
                    new BasicRegion(9, 10)
                });

            // adjacent regions are kept adjacent - ok
            ExecuteTest_SortAndCollapse(new ChrIntervalSet(
                new List<BasicRegion>()
                {
                    new BasicRegion(4, 6),
                    new BasicRegion(7, 8),
                    new BasicRegion(9, 10)
                }, "chr1"),
                new List<BasicRegion>()
                {
                    new BasicRegion(4, 6),
                    new BasicRegion(7, 8),
                    new BasicRegion(9, 10)
                });

            // resorts
            ExecuteTest_SortAndCollapse(new ChrIntervalSet(
                new List<BasicRegion>()
                {
                    new BasicRegion(9, 10),
                    new BasicRegion(4, 6),
                    new BasicRegion(7, 8)
                }, "chr1"),
                new List<BasicRegion>()
                {
                    new BasicRegion(4, 6),
                    new BasicRegion(7, 8),
                    new BasicRegion(9, 10)
                });

            // merges overlapping regions + resorts everything
            ExecuteTest_SortAndCollapse(new ChrIntervalSet(
                new List<BasicRegion>()
                {
                    // overlap exact - part 1
                    new BasicRegion(500, 505), 
                    // overlap on right side
                    new BasicRegion(4, 6),  
                    new BasicRegion(6, 8),
                    // overlap bigger first
                    new BasicRegion(200, 300), 
                    new BasicRegion(250, 300),
                    // overlap on left side
                    new BasicRegion(90, 100),
                    new BasicRegion(100, 101),
                    //overlap smaller first
                    new BasicRegion(400, 402), 
                    new BasicRegion(390, 402),
                    // overlap exact - part 2
                    new BasicRegion(500, 505),
                }, "chr1"),
                new List<BasicRegion>()
                {
                    new BasicRegion(4, 8),
                    new BasicRegion(90, 101),
                    new BasicRegion(200, 300),
                    new BasicRegion(390, 402),
                    new BasicRegion(500, 505), 
                });

            // merge requires more merge
            ExecuteTest_SortAndCollapse(new ChrIntervalSet(
                new List<BasicRegion>()
                {
                    new BasicRegion(1, 5),
                    new BasicRegion(10, 20),
                    new BasicRegion(5, 10), // will merge with #1, but should merge again with #2
                    new BasicRegion(20, 23),
                }, "chr1"),
                new List<BasicRegion>()
                {
                    new BasicRegion(1, 23)
                });
        }

        [Fact]
        public void GetMinus()
        {
            // no exclusions
            ExecuteTest_Minus(new BasicRegion(10, 50), new List<BasicRegion>(), new List<BasicRegion>()
            {
                new BasicRegion(10, 50),
            });
            ExecuteTest_Minus(new BasicRegion(10, 50), null, new List<BasicRegion>()
            {
                new BasicRegion(10, 50),
            });

            // exclude whole thing
            ExecuteTest_Minus(new BasicRegion(10, 50), new List<BasicRegion>() { new BasicRegion(10, 50) }, new List<BasicRegion>());
            ExecuteTest_Minus(new BasicRegion(10, 50), new List<BasicRegion>() { new BasicRegion(9, 51) }, new List<BasicRegion>());

            // left clip
            ExecuteTest_Minus(new BasicRegion(10, 50), new List<BasicRegion>() { new BasicRegion(10, 10) }, new List<BasicRegion>()
            {
                new BasicRegion(11, 50),
            });

            // right clip
            ExecuteTest_Minus(new BasicRegion(10, 50), new List<BasicRegion>() { new BasicRegion(50, 50) }, new List<BasicRegion>()
            {
                new BasicRegion(10, 49),
            });

            // middle chunk
            ExecuteTest_Minus(new BasicRegion(10, 50), new List<BasicRegion>() { new BasicRegion(11, 49) }, new List<BasicRegion>()
            {
                new BasicRegion(10, 10),
                new BasicRegion(50, 50),
            });

            // throw the kitchen sink
            ExecuteTest_Minus(new BasicRegion(10, 50), new List<BasicRegion>()
            {
                new BasicRegion(5, 15),
                new BasicRegion(20, 30),
                new BasicRegion(40, 45),
                new BasicRegion(48, 55),
            }, new List<BasicRegion>()
            {
                new BasicRegion(16, 19),
                new BasicRegion(31, 39),
                new BasicRegion(46, 47),
            });

            // invalid keep region
            Assert.Throws<ArgumentException>(() =>
                ExecuteTest_Minus(new BasicRegion(10, 9), new List<BasicRegion>(), new List<BasicRegion>()));
            Assert.Throws<ArgumentException>(() =>
                ExecuteTest_Minus(null, new List<BasicRegion>(), new List<BasicRegion>()));

            // invalid exclusion
            Assert.Throws<ArgumentException>(() =>
                ExecuteTest_Minus(new BasicRegion(10, 15), new List<BasicRegion>()
                {
                    new BasicRegion(10, 9)
                }, new List<BasicRegion>()));
            Assert.Throws<ArgumentException>(() =>
                ExecuteTest_Minus(new BasicRegion(10, 15), new List<BasicRegion>()
                {
                    null
                }, new List<BasicRegion>()));
        }

        [Fact]
        public void GetClipped_NoExclusions()
        {
            // intervals are:
            // 5-10, 20-30, 40-50

            // keep region fully contains intervals, no exclusions
            ExecuteTest_GetClipped(new BasicRegion(5, 50), null, new List<BasicRegion>()
            {
                new BasicRegion(5, 10),
                new BasicRegion(20, 30),
                new BasicRegion(40, 50)
            });

            // 1 off
            ExecuteTest_GetClipped(new BasicRegion(6, 49), null, new List<BasicRegion>()
            {
                new BasicRegion(6, 10),
                new BasicRegion(20, 30),
                new BasicRegion(40, 49)
            });

            // inner only
            ExecuteTest_GetClipped(new BasicRegion(11, 39), null, new List<BasicRegion>()
            {
                new BasicRegion(20, 30)
            });

            ExecuteTest_GetClipped(new BasicRegion(20, 30), null, new List<BasicRegion>()
            {
                new BasicRegion(20, 30)
            });

            // inner - 1 off
            ExecuteTest_GetClipped(new BasicRegion(21, 29), null, new List<BasicRegion>()
            {
                new BasicRegion(21, 29)
            });

            // invalid clip to region
            Assert.Throws<ArgumentException>(() =>
                ExecuteTest_GetClipped(new BasicRegion(21, 20), null, new List<BasicRegion>()));
        }

        [Fact]
        public void GetClipped_WithExclusions()
        {
            // intervals are:
            // 5-10, 20-30, 40-50
            
            // exclusions already tested with GetMinus unit tests
            // throw kitchen sink at this one
            ExecuteTest_GetClipped(new BasicRegion(7, 45),
                new List<BasicRegion>()
                {
                    new BasicRegion(10, 20),
                    new BasicRegion(22, 25),
                    new BasicRegion(42, 44)
                },
                new List<BasicRegion>()
                {
                    new BasicRegion(7, 9),
                    new BasicRegion(21, 21),
                    new BasicRegion(26, 30),
                    new BasicRegion(40, 41),
                    new BasicRegion(45, 45)
                });
        }

        private void ExecuteTest_SortAndCollapse(ChrIntervalSet set, List<BasicRegion> expectedRegions)
        {
            set.SortAndCollapse();
           
            Assert.Equal(expectedRegions.Count, set.Intervals.Count);

            for (var i = 0; i < expectedRegions.Count; i ++)
            {
                Assert.Equal(expectedRegions[i], set.Intervals[i]);
            }
        }

        private void ExecuteTest_Minus(BasicRegion keepRegion, List<BasicRegion> excludeRegions, List<BasicRegion> expectedRegions)
        {
            var results = ChrIntervalSet.GetMinus(keepRegion, excludeRegions);

            Assert.Equal(expectedRegions.Count, results.Count);

            for (var i = 0; i < expectedRegions.Count; i++)
            {
                Assert.Equal(expectedRegions[i], results[i]);
            }
        }

        private void ExecuteTest_GetClipped(BasicRegion clipRegion, List<BasicRegion> excludeRegions, List<BasicRegion> expectedRegions = null)
        {
            var intervalSet = new ChrIntervalSet(
                new List<BasicRegion>()
                {
                    new BasicRegion(5, 10),
                    new BasicRegion(20, 30),
                    new BasicRegion(40, 50),
                }, "chr1");

            var results = intervalSet.GetClipped(clipRegion, excludeRegions);

            Assert.Equal(expectedRegions.Count, results.Count);

            for (var i = 0; i < expectedRegions.Count; i++)
            {
                Assert.Equal(expectedRegions[i], results[i]);
            }
        }
    }
}
