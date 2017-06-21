using System.Collections.Generic;
using System.Linq;
using Moq;
using Alignment.Domain.Sequencing;
using Pisces.Interfaces;
using Pisces.Logic.Alignment;
using Pisces.IO.Sequencing;
using TestUtilities.MockBehaviors;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
//using Pisces.Domain.Tests;
using StitchingLogic; 
using TestUtilities;
using Xunit;

namespace Pisces.Tests.UnitTests.Alignment
{
    public class AlignmentSourceTests
    {
        [Fact]
        public void GetAlignmentSet()
        {
            var reads = new List<Read>();
            reads.Add(CreateRead("chr1", "ACGT", 10, "read1", matePosition: 10));
            reads.Add(CreateRead("chr1", "ACGT", 10, "read2", matePosition: 10));
            reads.Add(CreateRead("chr1", "ACGT", 10, "read1", read2: true, matePosition: 10)); // mate
            reads.Add(CreateRead("chr1", "ACGT", 10, "read_notmapped", isMapped: false, isProperPair: false, matePosition: 10));
            reads.Add(CreateRead("chr1", "ACGT", 10, "read3", isProperPair: false, read2: true, matePosition: 10));  // mate
            reads.Add(CreateRead("chr1", "ACGT", 10, "read2", read2: true, matePosition: 10));

            var extractor = new MockAlignmentExtractor(reads);
        
            var config = new AlignmentSourceConfig { MinimumMapQuality = 10, OnlyUseProperPairs = false };
            var source = new AlignmentSource(extractor, null,  config);

            Read read;
            var numSets = 0;
            while ((read = source.GetNextRead()) != null)
            {
                numSets++;
                Assert.False(read.Name == "read_notmapped");
            }

            Assert.Equal(numSets, 5);
        }


       [Fact]
       public void GetAlignmentSet_NoPairingOrStitching()
       {
           ExecuteTest_ProcessReadsSeparately(true, false);
           ExecuteTest_ProcessReadsSeparately(false, true);
           ExecuteTest_ProcessReadsSeparately(false, false);
       }

       private void ExecuteTest_ProcessReadsSeparately(bool doMate, bool doStitch)
       {
           var read = CreateRead("chr1", "ACGT", 10, "read1", matePosition:100);
           var readMate = CreateRead("chr1", "ACGT", 100, "read1", read2: true, matePosition: 10);

           var extractor = new MockAlignmentExtractor(new List<Read>() {read});

           var mateFinder = new Mock<IAlignmentMateFinder>();
           mateFinder.Setup(f => f.GetMate(It.IsAny<Read>())).Returns(readMate);

           var stitcher = new Mock<IAlignmentStitcher>();

           var config = new AlignmentSourceConfig { MinimumMapQuality = 10, OnlyUseProperPairs = false };
           var source = new AlignmentSource(extractor, doMate ? mateFinder.Object : null, config);

           var alignmentSet = source.GetNextRead();

           // TODO what do we really want to test here now that we're not pairing?
       }

        [Fact]
        [Trait("ReqID","SDS-28")]
        public void Filtering()
        {
            //bool isMapped = true, bool isPrimaryAlignment = true, bool isProperPair = true, bool isDuplicate = false
            var reads = new List<Read>();
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered", false, true, true, false, 10));
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered", true, false, true, false, 10));
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered_properpair", true, true, false, false, 10));
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered", true, true, true, true, 10));
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered_quality", true, true, true, false, 9));
            reads.Add(CreateRead("chr1", "AAA", 1, "Filtered_CigarData", true, true, true, false, 10, false));

            reads.Add(CreateRead("chr1", "AAA", 1, "yay", true, true, true, false, 10));

            var extractor = new MockAlignmentExtractor(reads);

            var config = new AlignmentSourceConfig { MinimumMapQuality = 10,
                OnlyUseProperPairs = true, SkipDuplicates = true };

            var source = new AlignmentSource(extractor, null, config);

            Read read;
            var fetchedCount = 0;
            while ((read = source.GetNextRead()) != null)
            {
                fetchedCount ++;
                Assert.Equal(read.Name, "yay");

            }

            Assert.Equal(1, fetchedCount);

            // -------------------------
            // turn off proper pairs
            config.OnlyUseProperPairs = false;
            fetchedCount = 0;
            extractor.Reset();

            source = new AlignmentSource(extractor, null, config);

            while ((read = source.GetNextRead()) != null)
            {
                fetchedCount ++;

                Assert.Equal(read.Name, 
                    fetchedCount == 1 ? "filtered_properpair" : "yay");
            }

            Assert.Equal(fetchedCount, 2);

            // -------------------------
            // change quality cut off
            config.OnlyUseProperPairs = true;
            config.MinimumMapQuality = 9;
            fetchedCount = 0;

            extractor.Reset();

            source = new AlignmentSource(extractor, null, config);

            while ((read = source.GetNextRead()) != null)
            {
                fetchedCount++;

                Assert.Equal(read.Name,
                    fetchedCount == 1 ? "filtered_quality" : "yay");
            }

            Assert.Equal(fetchedCount, 2);
        }

        private Read CreateRead(string chr, string sequence, int position, string name, bool isMapped = true, 
            bool isPrimaryAlignment = true, bool isProperPair= true, bool isDuplicate = false, int mapQuality = 10, bool addCigarData = true, 
            bool read2 = false, int matePosition = 0)
        {
            var alignment = new BamAlignment() { Bases = sequence, Position = position, Name = name, MapQuality = (uint)mapQuality};
            alignment.SetIsUnmapped(!isMapped);
            alignment.SetIsSecondaryAlignment(!isPrimaryAlignment);
            alignment.SetIsDuplicate(isDuplicate);
            alignment.SetIsProperPair(isProperPair);
            alignment.SetIsFirstMate(!read2);
            alignment.MatePosition = matePosition;

            if (addCigarData) 
                alignment.CigarData = new CigarAlignment(sequence.Length + "M");
            return new Read(chr, alignment);
        }
    }
}
