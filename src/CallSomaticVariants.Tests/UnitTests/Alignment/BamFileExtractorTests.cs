using System;
using System.IO;
using System.Runtime.InteropServices;
using CallSomaticVariants.Logic.Alignment;
using CallSomaticVariants.Models;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Alignment
{
    public class BamFileExtractorTests
    {
        [Fact]
        public void ReadFile()
        {
            var smallBam = Path.Combine(UnitTestPaths.TestDataDirectory, "small.bam");
            ReadFileTest(smallBam, false, 1000, false);

            var bwaXCbam = Path.Combine(UnitTestPaths.TestDataDirectory, "bwaXC.bam");
            ReadFileTest(bwaXCbam, false, 4481+8171, true);

        }

        [Fact]
        public void ReadFile_StitchingEnabled()
        {
            var smallBam = Path.Combine(UnitTestPaths.TestDataDirectory, "small.bam");
            ReadFileTest(smallBam, true, 1000, false);

            var bwaXCbam = Path.Combine(UnitTestPaths.TestDataDirectory, "bwaXC.bam");
            var ex = Assert.Throws<Exception>(()=>ReadFileTest(bwaXCbam, true, 4481 + 8171, true));
            Assert.Contains("CIGAR", ex.Message, StringComparison.InvariantCultureIgnoreCase); 
        }

        private void ReadFileTest(string bamfile, bool stitchReads, int expectedReads, bool bamHasXc)
        {
            var extractor = new BamFileAlignmentExtractor(bamfile, stitchReads);

            var read = new Read();
            var lastPosition = -1;
            var numReads = 0;

            bool hasAnyStitchedCigars = false;
            while (extractor.GetNextAlignment(read))
            {
                Assert.True(read.Position >= lastPosition); // make sure reads are read in order
                Assert.False(string.IsNullOrEmpty(read.Name));
                Assert.False(string.IsNullOrEmpty(read.Chromosome));

                if(!stitchReads) Assert.Equal(null, read.StitchedCigar);
                if (read.StitchedCigar!=null && read.StitchedCigar.Count > 0) hasAnyStitchedCigars = true;
                lastPosition = read.Position;
                numReads++;
            }

            if (stitchReads && bamHasXc) Assert.True(hasAnyStitchedCigars);
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

            Assert.Throws<ArgumentException>(() => new BamFileAlignmentExtractor(nonExistantBam, false));

            var missingIndexBam = Path.Combine(UnitTestPaths.TestDataDirectory, "missing_bai.bam");

            Assert.Throws<ArgumentException>(() => new BamFileAlignmentExtractor(missingIndexBam, false));
        }

        // jg - todo need to hunt down bam with multiple chromosomes, but not too big
        //[Fact]
        //public void Jump()
        //{

        //}
    }
}
