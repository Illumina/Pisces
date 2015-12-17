using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Models;
using CallSomaticVariants.Tests.MockBehaviors;
using CallSomaticVariants.Tests.Utilities;
using Moq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic.Alignment;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Alignment
{
    public class AlignmentSourceTests
    {
        [Fact]
        public void GetAlignmentSet()
        {
            var reads = new List<Read>();
            reads.Add(CreateRead("chr1", "ACGT", 1, "read1"));
            reads.Add(CreateRead("chr1", "ACGT", 1, "read2"));
            reads.Add(CreateRead("chr1", "ACGT", 1, "read1_mate"));
            reads.Add(CreateRead("chr1", "ACGT", 1, "read3_unmapped", isMapped:false, isProperPair:false));
            reads.Add(CreateRead("chr1", "ACGT", 1, "read3_mate", isProperPair:false));
            reads.Add(CreateRead("chr1", "ACGT", 1, "read2_mate"));

            var extractor = new MockAlignmentExtractor(reads);

            var mateFinder = new Mock<IAlignmentMateFinder>();
            mateFinder.Setup(f => f.GetMate(It.IsAny<Read>())).Returns(
                (Read r) =>
                    r.Name.EndsWith("_mate")
                    ? reads.FirstOrDefault(o => o.Name == r.Name.Replace("_mate", string.Empty))
                    : null);

            var stitcher = new Mock<IAlignmentStitcher>();
            stitcher.Setup(s => s.TryStitch(It.IsAny<AlignmentSet>())).Callback((AlignmentSet s) => s.IsStitched = true);
        
            var config = new AlignmentSourceConfig { MinimumMapQuality = 10, OnlyUseProperPairs = false };
            var source = new AlignmentSource(extractor, mateFinder.Object, stitcher.Object, config);

            AlignmentSet set;
            var numSets = 0;
            while ((set = source.GetNextAlignmentSet()) != null)
            {
                numSets++;
                if (!set.IsFullPair)
                {
                    Assert.True(set.PartnerRead1.Name.Equals("read3_mate"));
                    Assert.True(set.PartnerRead2 == null);
                    Assert.False(set.IsStitched);
                }
                else
                {
                    Assert.True(set.PartnerRead2.Name.Equals(set.PartnerRead1.Name + "_mate"));
                    Assert.True(set.IsStitched);
                }
            }

            Assert.Equal(numSets, 3);
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
            var read = CreateRead("chr1", "ACGT", 1, "read1");
            var readMate = CreateRead("chr1", "ACGT", 10, "read1mate");

            var extractor = new MockAlignmentExtractor(new List<Read>() {read});

            var mateFinder = new Mock<IAlignmentMateFinder>();
            mateFinder.Setup(f => f.GetMate(It.IsAny<Read>())).Returns(readMate);

            var stitcher = new Mock<IAlignmentStitcher>();

            var config = new AlignmentSourceConfig { MinimumMapQuality = 10, OnlyUseProperPairs = false };
            var source = new AlignmentSource(extractor, doMate ? mateFinder.Object : null, doStitch ? stitcher.Object : null, config);

            var alignmentSet = source.GetNextAlignmentSet();

            // verify stitching relies on pairing
            stitcher.Verify(s => s.TryStitch(It.IsAny<AlignmentSet>()), Times.Never);
            if (!doMate)
            {
                TestHelper.CompareReads(read, alignmentSet.PartnerRead1);
                Assert.Equal(null, alignmentSet.PartnerRead2);
                Assert.Equal(1, alignmentSet.ReadsForProcessing.Count);
                TestHelper.CompareReads(read, alignmentSet.ReadsForProcessing.First());
            }
            else
            {
                TestHelper.CompareReads(read, alignmentSet.PartnerRead1);
                TestHelper.CompareReads(readMate, alignmentSet.PartnerRead2);
                Assert.Equal(2, alignmentSet.ReadsForProcessing.Count);
                TestHelper.CompareReads(read, alignmentSet.ReadsForProcessing.First());
                TestHelper.CompareReads(readMate, alignmentSet.ReadsForProcessing.Last());
            }
        }

        [Fact]
        [Trait("ReqID","SDS-28")]
        public void Filtering()
        {
            var reads = new List<Read>();
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered", false, true, true, false, 10));
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered", true, false, true, false, 10));
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered_properpair", true, true, false, false, 10));
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered", true, true, true, true, 10));
            reads.Add(CreateRead("chr1", "AAA", 1, "filtered_quality", true, true, true, false, 9));
            reads.Add(CreateRead("chr1", "AAA", 1, "Filtered_CigarData", true, true, true, false, 10, false));

            reads.Add(CreateRead("chr1", "AAA", 1, "yay", true, true, true, false, 10));

            var extractor = new MockAlignmentExtractor(reads);

            var config = new AlignmentSourceConfig { MinimumMapQuality = 10, OnlyUseProperPairs = true };

            var source = new AlignmentSource(extractor, null, null, config);

            AlignmentSet set;
            var fetchedCount = 0;
            while ((set = source.GetNextAlignmentSet()) != null)
            {
                fetchedCount ++;
                Assert.Equal(set.PartnerRead1.Name, "yay");

            }

            Assert.Equal(1, fetchedCount);

            // -------------------------
            // turn off proper pairs
            config.OnlyUseProperPairs = false;
            fetchedCount = 0;
            extractor.Reset();

            source = new AlignmentSource(extractor, null, null, config);

            while ((set = source.GetNextAlignmentSet()) != null)
            {
                fetchedCount ++;

                Assert.Equal(set.PartnerRead1.Name, 
                    fetchedCount == 1 ? "filtered_properpair" : "yay");
            }

            Assert.Equal(fetchedCount, 2);

            // -------------------------
            // change quality cut off
            config.OnlyUseProperPairs = true;
            config.MinimumMapQuality = 9;
            fetchedCount = 0;

            extractor.Reset();

            source = new AlignmentSource(extractor, null, null, config);

            while ((set = source.GetNextAlignmentSet()) != null)
            {
                fetchedCount++;

                Assert.Equal(set.PartnerRead1.Name,
                    fetchedCount == 1 ? "filtered_quality" : "yay");
            }

            Assert.Equal(fetchedCount, 2);
        }

        private Read CreateRead(string chr, string sequence, int position, string name, bool isMapped = true, 
            bool isPrimaryAlignment = true, bool isProperPair= true, bool isDuplicate = false, int mapQuality = 10, bool addCigarData = true)
        {
            var alignment = new BamAlignment() { Bases = sequence, Position = position, Name = name, MapQuality = (uint)mapQuality};
            alignment.SetIsUnmapped(!isMapped);
            alignment.SetIsSecondaryAlignment(!isPrimaryAlignment);
            alignment.SetIsDuplicate(isDuplicate);
            alignment.SetIsProperPair(isProperPair);
            if (addCigarData) 
                alignment.CigarData = new CigarAlignment(sequence.Length + "M");
            return new Read(chr, alignment);
        }
    }
}
