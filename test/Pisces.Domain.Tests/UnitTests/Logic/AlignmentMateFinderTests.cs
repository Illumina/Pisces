using System;
using System.Linq;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Logic;
using Pisces.Domain.Models;
using Pisces.Domain.Tests;
using Xunit;

namespace Pisces.Tests.UnitTests.Alignment
{
    public class AlignmentMateFinderTests
    {
        [Fact]
        public void GetUnpaired()
        {
            var finder = new AlignmentMateFinder();

            var read1 = CreateAlignment(100, 500, "1");
            Assert.Null(finder.GetMate(read1));
            Assert.Equal(new[] {read1.Name}, finder.GetUnpairedReads().Select(x => x.Name).ToArray());
        }

        [Fact]
        public void ReadPurgedEventTriggered()
        {
            var finder = new AlignmentMateFinder(maxWindow:500);
            Read purgedRead = null;
            finder.ReadPurged += read => purgedRead = read;

            // reads whose mates will never be encountered because they are before the current position
            var read1 = CreateAlignment(5000, 100, "1");
            Assert.Equal(finder.GetMate(read1), null);
            Assert.Equal(read1.Name, purgedRead.Name);

            // reads whose mates are more than the max distance before a read being added (and thus will not be encountered)
            var read2 = CreateAlignment(5000, 5100, "2");
            Assert.Equal(finder.GetMate(read2), null);
            Assert.Equal(finder.GetMate(CreateAlignment(6000, 6100, "3")), null);

            Assert.Equal(read2.Name, purgedRead.Name);

        }


        [Fact]
        public void GetMate()
        {
            var finder = new AlignmentMateFinder();
            
            var read1 = CreateAlignment(100, 500, "1");
            var read1Mate = CreateAlignment(500, 100, "1");
            var read2 = CreateAlignment(200, 400, "2");
            var read2Mate = CreateAlignment(400, 200, "2");
            var read3 = CreateAlignment(201, 600, "3");
            var read3Mate = CreateAlignment(600, 201, "3");
            var read4 = CreateAlignment(1000, 2000, "4");
            var read4Mate = CreateAlignment(2000, 1000, "4");
            var read5 = CreateAlignment(2500, 3501, "5");
            var read5Mate = CreateAlignment(3501, 2500, "5");

            Assert.Equal(finder.LastClearedPosition, null);
            Assert.Equal(finder.NextMatePosition, null);

            Assert.Equal(finder.GetMate(read1), null);
            Assert.Equal(finder.LastClearedPosition, 99);
            Assert.Equal(finder.NextMatePosition, 500);

            Assert.Equal(finder.GetMate(read2), null);
            Assert.Equal(finder.LastClearedPosition, 99);
            Assert.Equal(finder.NextMatePosition, 400);

            Assert.Equal(finder.GetMate(read3), null);
            Assert.Equal(finder.LastClearedPosition, 99);

            ReadTests.CompareReads(finder.GetMate(read2Mate), read2);
            Assert.Equal(finder.LastClearedPosition, 99);
            Assert.Equal(finder.NextMatePosition, 500);

            ReadTests.CompareReads(finder.GetMate(read1Mate), read1);
            Assert.Equal(finder.LastClearedPosition, 200);
            Assert.Equal(finder.NextMatePosition, 600);

            ReadTests.CompareReads(finder.GetMate(read3Mate), read3);
            Assert.Equal(finder.LastClearedPosition, null);

            Assert.Equal(finder.GetMate(read4), null);
            Assert.Equal(finder.LastClearedPosition, 999);

            ReadTests.CompareReads(finder.GetMate(read4Mate), read4);
            Assert.Equal(finder.LastClearedPosition, null);

            Assert.Equal(finder.GetMate(read5), null);
            Assert.Equal(finder.LastClearedPosition, 2499);

            Assert.Equal(finder.GetMate(read5Mate), null); // out of window gets tossed
            Assert.Equal(finder.LastClearedPosition, null);
            Assert.Equal(finder.NextMatePosition, null);
            Assert.Equal(2, finder.ReadsUnpairable);
            
            Assert.Throws<ArgumentException>(() => finder.GetMate(CreateAlignment(2500, 2500, null))); // null name
            Assert.Throws<ArgumentException>(() => finder.GetMate(CreateAlignment(2500, 2500, ""))); // empty name
            Assert.Throws<ArgumentException>(() => finder.GetMate(CreateAlignment(2500, -1, null))); // invalid mate position


        }

        [Fact]
        public void BadPairs()
        {
            var finder = new AlignmentMateFinder();

            // mismatch on mate positions
            var read6 = CreateAlignment(2500, 2600, "6");
            var read6Mate = CreateAlignment(2600, 2501, "6");
            var read7 = CreateAlignment(2500, 2601, "7");
            var read7Mate = CreateAlignment(2602, 2500, "7");

            Assert.Equal(finder.GetMate(read6), null);
            Assert.Equal(finder.LastClearedPosition, 2499);
            Assert.Equal(finder.GetMate(read6Mate), null);
            Assert.Equal(finder.LastClearedPosition, null); // both cleared out

            Assert.Equal(finder.GetMate(read7), null);
            Assert.Equal(finder.LastClearedPosition, 2499); 
            Assert.Equal(finder.GetMate(read7Mate), null);
            Assert.Equal(finder.LastClearedPosition, null); // both cleared out


        }

        private Read CreateAlignment(int position, int matePosition, string name)
        {
            return new Read("chr1", new BamAlignment
            {
                Bases = "BLAH",
                Position = position - 1,
                MatePosition = matePosition - 1,
                Name = name,
                Qualities = new byte[0]
            });
        }
    }
}
