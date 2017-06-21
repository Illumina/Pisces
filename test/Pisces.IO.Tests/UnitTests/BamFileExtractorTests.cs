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
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "small.bam");
            ReadFileTest(smallBam, 1000, false);

            var bwaXCbam = Path.Combine(TestPaths.LocalTestDataDirectory, "bwaXC.bam");
            var ex = Assert.Throws<InvalidDataException>(() => ReadFileTest(bwaXCbam, 4481 + 8171, true));
            Assert.Contains("CIGAR", ex.Message, StringComparison.CurrentCultureIgnoreCase);
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

                if (!bamHasXc) Assert.Equal(null, read.StitchedCigar);
                if (read.StitchedCigar != null && read.StitchedCigar.Count > 0) hasAnyStitchedCigars = true;
                lastPosition = read.Position;
                numReads++;
            }

            if (bamHasXc) Assert.True(hasAnyStitchedCigars);
            Assert.Equal(expectedReads, numReads);
            extractor.Dispose();

            // make sure can't read after dispose
            Assert.Throws<IOException>(() => extractor.GetNextAlignment(read));
        }

        [Fact]
        [Trait("ReqID", "SDS-5")]
        [Trait("ReqID", "SDS-6")]
        public void Constructor()
        {
            var nonExistantBam = Path.Combine(TestPaths.LocalTestDataDirectory, "non_existant.bam");

            Assert.Throws<ArgumentException>(() => new BamFileAlignmentExtractor(nonExistantBam));

            var missingIndexBam = Path.Combine(TestPaths.LocalTestDataDirectory, "missing_bai.bam");

            Assert.Throws<ArgumentException>(() => new BamFileAlignmentExtractor(missingIndexBam));
        }

        [Fact]
        public void SanityCheckSequenceOrdering()
        {
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "Ins-L3-var12_S12.bam");
            var intervals = new Dictionary<string, List<Region>>();
            var chrIntervals = new List<Region>
            {
                new Region(28607838, 28607838),
                new Region(28608631, 28608631)
            };
            var expectedSQorder = new List<string> { "chr10", "chr11", "chr12", "chr13" }; //I dont know why it starts with 10, thats just how it is in the bam. thats what makes it a good test case.

            intervals.Add("chr13", chrIntervals);
            var extractor = new BamFileAlignmentExtractor(smallBam, bamIntervals: intervals);
            List<string> sequencesInTheBamOrder = extractor.SourceReferenceList;

            Assert.Equal(expectedSQorder[0], sequencesInTheBamOrder[0]);
            Assert.Equal(expectedSQorder[1], sequencesInTheBamOrder[1]);
            Assert.Equal(expectedSQorder[3], sequencesInTheBamOrder[3]);
            Assert.Equal(25, sequencesInTheBamOrder.Count);

            //happyPath 
            Assert.False( extractor.SequenceOrderingIsNotConsistent(new List<string> {"chr1", "chr2" }));
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr1", "chr3", "chr4" }));
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr14", "chr9" })); //only b/c the bam header is silly.

            //not OK
            Assert.True(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr2", "chr1" }));
            Assert.True(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr9", "chr14" }));
            Assert.True(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr22", "chr21" }));

            //genome has chr not in bam, be ok with it
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr1", "chrMotherGoose" }));

            //bam has chr not in genome, be ok with it
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr1" }));

            //empty lists
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> {  }));
            Assert.False(extractor.SequenceOrderingIsNotConsistent(null));

        }
        [Fact]
        public void IntervalJumping_Middle()
        {
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "Ins-L3-var12_S12.bam");
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
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "Ins-L3-var12_S12.bam");
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


        /// <summary>
        /// Tests:
        ///     - we successfully jump forward past reads to the first interval.
        ///     - we bail out early if the remaining intervals are in regions with no data.
        /// </summary>
        [Fact]
        public void IntervalJumping_Ends()
        {
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "Ins-L3-var12_S12.bam");
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
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "small.bam");
            var intervals = new Dictionary<string, List<Region>>();
            intervals.Add("chr7", new List<Region>());

            Assert.Throws<InvalidDataException>(() => new BamFileAlignmentExtractor(smallBam, "chr1", intervals));
        }

        [Fact]
        public void Intervals_BeforeReads()
        {
            var bamfile = Path.Combine(TestPaths.LocalTestDataDirectory, "small.bam");

            var regions = new Dictionary<string, List<Region>>();

            regions.Add("chr1", new List<Region>()
            {
                new Region(100, 1000),
                new Region(2000, 2100),
                new Region(4000, 4100)
            });

            // First read in small.bam starts at position: 115251017, so no reads should be returned by the extractor since they all fall outside the regions of interest.

            var extractor = new BamFileAlignmentExtractor(bamfile, null, regions);

            var read = new Read();

            Assert.False(extractor.GetNextAlignment(read));
        }

        [Fact]
        public void IntervalJumping_Boundaries()
        {
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "Ins-L3-var12_S12.bam");
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

        [Fact]
        public void UnalignedReads()
        {
            var extractor = new BamFileAlignmentExtractor(Path.Combine(TestPaths.LocalTestDataDirectory, "unaligned.bam"));

            var read = new Read();
            var count = 0;
            while (extractor.GetNextAlignment(read))
            {
                count++;
            }

            Assert.Equal(138826, count);
            Assert.Equal(null, read.Chromosome); // last reads are unaligned
        }

        [Fact]
        public void TestIfBamIsStitched()
        {
            //test some generic bam
            var extractor = new BamFileAlignmentExtractor(Path.Combine(TestPaths.LocalTestDataDirectory, "unaligned.bam"));
            Assert.Equal(false, extractor.SourceIsStitched);


            //test to be robust to crazy bams.

            Assert.Equal(false,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched(""));

            Assert.Equal(false,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched("@PG @PG"));

            Assert.Equal(false,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched("blah"));

            Assert.Equal(false,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched(null));

            //test some real normal headers

            Assert.Equal(true,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched(GetPiscesStitchedHeader()));

            Assert.Equal(false,
                BamFileAlignmentExtractor.CheckIfBamHasBeenStitched(GetRegularHeader()));
        }

        public string GetRegularHeader()
        {
            return
            @"@HD VN:1.4 SO:coordinate
@PG ID: Isis PN:Isis VN:2.4.61.97
@SQ SN:chrM LN:16571 M5:
            d2ed829b8a1628d16cbeee88e88e39eb
@SQ SN: chr1 LN:249250621 M5: 1b22b98cdeb4a9304cb5d48026a85128
@SQ SN: chr2 LN:243199373 M5:
            a0d9851da00400dec1098a9255ac712e
..
@SQ SN: chr21 LN:48129895 M5: 2979a6085bfe28e3ad6f552f361ed74d
@SQ SN: chr22 LN:51304566 M5:
            a718acaa6135fdca8357d5bfe94211dd
@SQ SN: chrX LN:155270560 M5: 7e0e2e580297b7764e31dbc80c2540dd
@SQ SN: chrY LN:59373566 M5: 1fa3474750af0948bdf97d5a0ee52e51
@RG ID: AMHS - MixB - 22030 PL: ILLUMINA SM:AMHS - MixB - 22030";

        }

        public string GetPiscesStitchedHeader()
        {
            return
            @"@HD VN:1.4 SO:coordinate
@PG ID: Isis PN:Isis VN:2.4.61.97
@PG ID:Pisces PN:Stitcher VN:5.1.5.2
@SQ SN:chrM LN:16571 M5:
            d2ed829b8a1628d16cbeee88e88e39eb
@SQ SN: chr1 LN:249250621 M5: 1b22b98cdeb4a9304cb5d48026a85128
@SQ SN: chr2 LN:243199373 M5:
            a0d9851da00400dec1098a9255ac712e
..
@SQ SN: chr21 LN:48129895 M5: 2979a6085bfe28e3ad6f552f361ed74d
@SQ SN: chr22 LN:51304566 M5:
            a718acaa6135fdca8357d5bfe94211dd
@SQ SN: chrX LN:155270560 M5: 7e0e2e580297b7764e31dbc80c2540dd
@SQ SN: chrY LN:59373566 M5: 1fa3474750af0948bdf97d5a0ee52e51
@RG ID: AMHS - MixB - 22030 PL: ILLUMINA SM:AMHS - MixB - 22030";

        }
    }
}
