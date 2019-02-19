using System.IO;
using Alignment.Domain.Sequencing;
using Moq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;
using System;
using System.Collections.Generic;
using System.Text;
using TestUtilities;
using TestUtilities.MockBehaviors;
using VariantPhasing.Interfaces;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class MNVSoftClipSupportFinderTests
    {
       
        [Fact]
        public void SupplementSupportWithClippedReads()
        {
            // In this test we create reads that are either normal or clipped (identified by "clip_" in their name)
            // This test does not take cigar data into account.

            var mockClippedReadComparator = new Mock<IMNVClippedReadComparator>();
            // Mock read comparator returns true if read name starts with c
            mockClippedReadComparator.Setup(x => x.DoesClippedReadSupportMNV(It.IsAny<Read>(), It.IsAny<CalledAllele>()))
                .Returns((Read read, CalledAllele allele) => read.Name[0] == 'c' ? true : false);

            var reads = new List<Read>();
            reads.Add(CreateRead("chr1", "ACGT", 3, "read4"));
            reads.Add(CreateRead("chr1", "ACGT", 3, "clip_read4", matePosition: 3));  // +1 not in neighborhood, but still gets counted because mocked ClippedReadComparator
            reads.Add(CreateRead("chr1", "ACGT", 12, "read1", matePosition: 10));
            reads.Add(CreateRead("chr1", "ACGT", 12, "read2", matePosition: 10));
            reads.Add(CreateRead("chr1", "ACGT", 12, "read1", read2: true, matePosition: 10));
            reads.Add(CreateRead("chr1", "ACGT", 12, "read_notmapped", isMapped: false, isProperPair: false, matePosition: 10));
            reads.Add(CreateRead("chr1", "ACGT", 12, "read3", isProperPair: false, read2: true, matePosition: 10));
            reads.Add(CreateRead("chr1", "ACGT", 12, "read2", read2: true, matePosition: 10));
            reads.Add(CreateRead("chr1", "ACGT", 12, "clip_read1", matePosition: 10)); // +1 clipped read
            reads.Add(CreateRead("chr1", "ACGT", 12, "clip_read2", matePosition: 10)); // +1 clipped read
            reads.Add(CreateRead("chr1", "ACGT", 12, "clip_read1", read2: true, matePosition: 10));  // +1 clipped read
            reads.Add(CreateRead("chr1", "ACGT", 12, "clip_read_notmapped", isMapped: false, isProperPair: false, matePosition: 10));  // +1 clipped read
            reads.Add(CreateRead("chr1", "ACGT", 12, "clip_read3", isProperPair: false, read2: true, matePosition: 10));   // +1 clipped read
            reads.Add(CreateRead("chr1", "ACGT", 12, "clip_read2", read2: true, matePosition: 10));  // +1 clipped read
            reads.Add(CreateRead("chr1", "ACGT", 30, "read5"));
            reads.Add(CreateRead("chr1", "ACGT", 30, "clip_read5", matePosition: 30));  // not in neighborhood, not counted
            var mockAlignmentExtractor = new MockAlignmentExtractor(reads);
            int qNoiseLevel = 20;
            int maxQscore = 100;
            int minMNVsize = 6;
            MNVSoftClipSupportFinder mnvClippedSupportFinder = new MNVSoftClipSupportFinder(mockAlignmentExtractor, mockClippedReadComparator.Object, qNoiseLevel, maxQscore, minMNVsize);

            var mnv1 = TestHelper.CreateDummyAllele("chr1", 10, "AAAAAA", "CCC", 2000, 50);
            var neighbor1 = new VcfNeighborhood( 0, "chr", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(10){VcfReferenceAllele = "A", VcfAlternateAllele = "C", ReferenceName="chr"},
                    new VariantSite(25){VcfReferenceAllele = "T", VcfAlternateAllele = "G", ReferenceName="chr"},
                },
            };

            var callableNbhd = new CallableNeighborhood(neighbor1, new VariantCallingParameters(), null);

            callableNbhd.AddAcceptedPhasedVariant(mnv1);
            Assert.Equal(50, callableNbhd.CandidateVariants[0].AlleleSupport);
            mnvClippedSupportFinder.SupplementSupportWithClippedReads(callableNbhd);
            Assert.Equal(57, callableNbhd.CandidateVariants[0].AlleleSupport);
        }

        // Copied from AlignmentSourceTests.cs (modified to add custom CigarData)
        private Read CreateRead(string chr, string sequence, int position, string name, CigarAlignment cigar = null, bool isMapped = true,
            bool isPrimaryAlignment = true, bool isProperPair = true, bool isDuplicate = false, int mapQuality = 10, bool addCigarData = true,
            bool read2 = false, int matePosition = 0)
        {
            var alignment = new BamAlignment() { Bases = sequence, Position = position, Name = name, MapQuality = (uint)mapQuality };
            alignment.SetIsUnmapped(!isMapped);
            alignment.SetIsSecondaryAlignment(!isPrimaryAlignment);
            alignment.SetIsDuplicate(isDuplicate);
            alignment.SetIsProperPair(isProperPair);
            alignment.SetIsFirstMate(!read2);
            alignment.MatePosition = matePosition;
            alignment.CigarData = cigar ?? new CigarAlignment(sequence.Length + "M");

            return new Read(chr, alignment);
        }
    }
}