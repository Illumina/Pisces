using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;
using Pisces.Domain.Models;
using Xunit;

namespace Pisces.IO.Tests
{
    public class BamFileExtractorTests
    {
        [Fact]
        public void ReadFile()
        {
            var smallBam = Path.Combine(UnitTestPaths.TestDataDirectory, "small.bam");
            ReadFileTest(smallBam, 1000, false);

            var bwaXCbam = Path.Combine(UnitTestPaths.TestDataDirectory, "bwaXC.bam");
            var ex = Assert.Throws<Exception>(()=>ReadFileTest(bwaXCbam, 4481 + 8171, true));
            Assert.Contains("CIGAR", ex.Message, StringComparison.InvariantCultureIgnoreCase); 
        }

        private void ReadFileTest(string bamfile, int expectedReads, bool bamHasXc)
        {
            var extractor = new BamFileAlignmentExtractor(bamfile);

            var read = new Read();
            var lastPosition = -1;
            var numReads = 0;

            bool hasAnyStitchedCigars = false;
            while (extractor.GetNextAlignment(read))
            {
                Assert.True(read.Position >= lastPosition); // make sure reads are read in order
                Assert.False(string.IsNullOrEmpty(read.Name));
                Assert.False(string.IsNullOrEmpty(read.Chromosome));

                if(!bamHasXc) Assert.Equal(null, read.StitchedCigar);
                if (read.StitchedCigar!=null && read.StitchedCigar.Count > 0) hasAnyStitchedCigars = true;
                lastPosition = read.Position;
                numReads++;
            }

            if (bamHasXc) Assert.True(hasAnyStitchedCigars);
            Assert.Equal(expectedReads, numReads);
            extractor.Dispose();

            // make sure can't read after dispose
            Assert.Throws<Exception>(() => extractor.GetNextAlignment(read));
        }

        [Fact]
        [Trait("ReqID", "SDS-5")]
        [Trait("ReqID", "SDS-6")]
        public void Constructor()
        {
            var nonExistantBam = Path.Combine(UnitTestPaths.TestDataDirectory, "non_existant.bam");

            Assert.Throws<ArgumentException>(() => new BamFileAlignmentExtractor(nonExistantBam));

            var missingIndexBam = Path.Combine(UnitTestPaths.TestDataDirectory, "missing_bai.bam");

            Assert.Throws<ArgumentException>(() => new BamFileAlignmentExtractor(missingIndexBam));
        }

        [Fact]
        public void IntervalJumping_Middle()
        {
            var smallBam = Path.Combine(UnitTestPaths.TestDataDirectory, "Ins-L3-var12_S12.bam");
            var intervals = new Dictionary<string, List<Region>>();
            var chrIntervals = new List<Region>
            {
                new Region(28607838, 28607838),
                new Region(28608631, 28608631)
            };
            intervals.Add("chr13", chrIntervals);
            var extractor = new BamFileAlignmentExtractor(smallBam, bamIntervals: intervals);

            var read = new Read();
            // verify we skip over the middle
            while (extractor.GetNextAlignment(read))
            {
                Assert.True(read.Position < 28607840 || read.BamAlignment.GetEndPosition() >= 28608631);
            }
        }

        [Fact]
        public void IntervalJumping_SmallIntervals()
        {
            var smallBam = Path.Combine(UnitTestPaths.TestDataDirectory, "Ins-L3-var12_S12.bam");
            var intervals = new Dictionary<string, List<Region>>();
            var chrIntervals = new List<Region>
            {
                new Region(28607838, 28607838),
                new Region(28607908, 28607908),
                new Region(28608631, 28608631)
            };
            intervals.Add("chr13", chrIntervals);
            var extractor = new BamFileAlignmentExtractor(smallBam, bamIntervals: intervals);

            var read = new Read();
            var lastPosition = 0;
            // verify we are always moving forward and not backwards (not re-reading alignments)
            while (extractor.GetNextAlignment(read))
            {
                Assert.True(read.Position >= lastPosition);
                lastPosition = read.Position;
            }
        }
        //[Fact]
        //public void IntervalJumping_ReadsSpanIntervals()
        //{
        //    // TODO: This test is commented out because the data neessary to reproduce it are
        //    // just too large to put on source control
        //    var smallBam = @"d:\ReCoInput\chr19.bam";
        //    var intervals = new Dictionary<string, List<Region>>();
        //    var chrIntervals = new List<Region>
        //    {
        //        new Region(3110147, 3110331),
        //        new Region(3113328, 3113330)
        //    };
        //    intervals.Add("chr19", chrIntervals);
        //    var extractor = new BamFileAlignmentExtractor(smallBam, bamIntervals: intervals);

        //    var read = new Read();
        //    var flags = new Dictionary<string, List<uint>>();

        //    // verify we are always moving forward and not backwards (not re-reading alignments)
        //    while (extractor.GetNextAlignment(read))
        //    {
        //        List<uint> f;
        //        if (flags.TryGetValue(read.Name, out f))
        //            f.Add(read.BamAlignment.AlignmentFlag);
        //        else
        //            flags[read.Name] = new List<uint> { read.BamAlignment.AlignmentFlag};
        //    }
        //    foreach (var kvp in flags)
        //        Assert.Equal(kvp.Value.Count, kvp.Value.Distinct().Count());
        //}


        /// <summary>
        /// Tests:
        ///     - we successfully jump forward past reads to the first interval.
        ///     - we bail out early if the remaining intervals are in regions with no data.
        /// </summary>
        [Fact]
        public void IntervalJumping_Ends()
        {
            var smallBam = Path.Combine(UnitTestPaths.TestDataDirectory, "Ins-L3-var12_S12.bam");
            var intervals = new Dictionary<string, List<Region>>();
            var chrIntervals = new List<Region>
            {
                new Region(28608100, 28608100),  // interval in the middle of coverage
                new Region(29608700, 29608800)   // interval out in the boonies where there's no data
            };
            intervals.Add("chr13", chrIntervals);
            var extractor = new BamFileAlignmentExtractor(smallBam, bamIntervals: intervals);

            var read = new Read();
            var numReadsLessThan = 0;
            var numReadsGreaterThan = 0;

            // verify we are always moving forward and not backwards (not re-reading alignments)
            while (extractor.GetNextAlignment(read))
            {
                if (read.EndPosition + 1 < 28608100) // bam reader is off by one, see note in BamFileExtractor.Jump
                {
                    numReadsLessThan++;
                }
                else if (read.Position > 28608100)
                {
                    numReadsGreaterThan++;
                }
            }

            Assert.Equal(1, numReadsLessThan);  // this should be just the first read (before we figure out we're not in range)
            Assert.Equal(0, numReadsGreaterThan);  
        }

        [Fact]
        public void NoIntervals()
        {
            var smallBam = Path.Combine(UnitTestPaths.TestDataDirectory, "small.bam");
            var intervals = new Dictionary<string, List<Region>>();
            intervals.Add("chr7", new List<Region>());

            Assert.Throws<Exception>(() => new BamFileAlignmentExtractor(smallBam, "chr1", intervals));
        }

        [Fact]
        public void IntervalJumping_Boundaries()
        {
            var smallBam = Path.Combine(UnitTestPaths.TestDataDirectory, "Ins-L3-var12_S12.bam");
            var intervals = new Dictionary<string, List<Region>>();
            var chrIntervals = new List<Region>
            {
                new Region(115169880, 115169880)  // feeding in an interval that's past reference max shouldnt cause it to blow up
            };
            intervals.Add("chr13", chrIntervals);
            var extractor = new BamFileAlignmentExtractor(smallBam, bamIntervals: intervals);

            var read = new Read();
            while (extractor.GetNextAlignment(read))
            {
            }
        }
    }
}
