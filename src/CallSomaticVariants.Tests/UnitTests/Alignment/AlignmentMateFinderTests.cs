using System;
using CallSomaticVariants.Logic.Alignment;
using CallSomaticVariants.Models;
using CallSomaticVariants.Tests.Utilities;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Alignment
{
    public class AlignmentMateFinderTests
    {
        [Fact]
        public void GetMate()
        {
            var finder = new AlignmentMateFinder(1001);

            var read1 = CreateAlignment(100, 500, "1");
            var read1Mate = CreateAlignment(500, 100, "1");
            var read2 = CreateAlignment(200, 500, "2");
            var read2Mate = CreateAlignment(500, 200, "2");
            var read3 = CreateAlignment(201, 600, "3");
            var read3Mate = CreateAlignment(600, 201, "3");
            var read4 = CreateAlignment(1000, 2001, "4");
            var read4Mate = CreateAlignment(2001, 1000, "4");
            var read5 = CreateAlignment(2500, 3502, "5");
            var read5Mate = CreateAlignment(3502, 2500, "5");

            Assert.Equal(finder.GetMate(read1), null);
            Assert.Equal(finder.GetMate(read2), null);
            Assert.Equal(finder.GetMate(read3), null);
            TestHelper.CompareReads(finder.GetMate(read2Mate), read2);
            TestHelper.CompareReads(finder.GetMate(read1Mate), read1);
            TestHelper.CompareReads(finder.GetMate(read3Mate), read3);
            Assert.Equal(finder.GetMate(read4), null);
            TestHelper.CompareReads(finder.GetMate(read4Mate), read4);

            Assert.Equal(finder.GetMate(read5), null);
            
            // jg - turned this behavior off for now because it's really inefficient
            // Assert.Throws<Exception>(() => finder.GetMate(read5Mate)); // out of window

            Assert.Throws<ArgumentException>(() => finder.GetMate(CreateAlignment(2500, 2500, null))); // null name
            Assert.Throws<ArgumentException>(() => finder.GetMate(CreateAlignment(2500, 2500, ""))); // empty name
            Assert.Throws<ArgumentException>(() => finder.GetMate(CreateAlignment(2500, -1, null))); // invalid mate position
        }

        private Read CreateAlignment(int position, int matePosition, string name)
        {
            return new Read("chr1", new BamAlignment
            {
                Bases = "BLAH",
                Position = position,
                MatePosition = matePosition,
                Name = name,
                Qualities = new byte[0]
            });
        }
    }
}
