using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Options;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using Xunit;
using TestUtilities;

namespace VariantPhasing.Tests.Logic
{
    public class NeighborhoodReadFilterTests
    {
        [Fact]
        public void PastNeighborhoodTest()
        {
            var nbhdReadFilter = new NeighborhoodReadFilter(new BamFilterParameters() { MinimumMapQuality = 20 });

            // Scenario 1: neighborhood with 2 SNVs
            var neighbor1 = new VcfNeighborhood(0, "chr1", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(10){VcfReferenceAllele = "A", VcfAlternateAllele = "C"},
                    new VariantSite(15){VcfReferenceAllele = "G", VcfAlternateAllele = "A"},
                },
            };

            var callableNeighbor1 = new CallableNeighborhood(neighbor1, new VariantCallingParameters());

            var read1 = TestHelper.CreateRead("chr1", "ACGT", 6);  // ends before neighborhood starts
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read1, callableNeighbor1));
            var read2 = TestHelper.CreateRead("chr1", "ACGT", 8);  // read partially covers neighborhood from left
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read2, callableNeighbor1));
            var read3 = TestHelper.CreateRead("chr1", "ACGT", 11); // read enclosed in neighborhood
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read3, callableNeighbor1));
            var read4 = TestHelper.CreateRead("chr1", "ACGT", 14);  // read partially sticks out of neighborhood from right
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read4, callableNeighbor1));
            var read5 = TestHelper.CreateRead("chr1", "ACGT", 15);  // read starts on last variant site
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read5, callableNeighbor1));
            var read6 = TestHelper.CreateRead("chr1", "ACGT", 16);  // read starts right after neighborhood
            // Nima: Minimum lookahead is pos+1, so this is still not considered past neighborhood (maybe we should -1 but it doesn't really make a huge diff?)
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read6, callableNeighbor1));
            var read7 = TestHelper.CreateRead("chr1", "ACGT", 17);  // read starts after neighborhood lookahead
            Assert.Equal(true, nbhdReadFilter.PastNeighborhood(read7, callableNeighbor1));


            // Scenario 2: neighborhood with one SNV, and one insertion (should extend lookahead)
            var neighbor2 = new VcfNeighborhood( 0, "chr1", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(10){VcfReferenceAllele = "A", VcfAlternateAllele = "C"},
                    new VariantSite(15){VcfReferenceAllele = "G", VcfAlternateAllele = "GAAA"},
                },
            };
            
            var callableNeighbor2 = new CallableNeighborhood(neighbor2, new VariantCallingParameters(), null);

            var read8 = TestHelper.CreateRead("chr1", "ACGT", 15);  // read starts at the last variant site
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read8, callableNeighbor2));
            var read9 = TestHelper.CreateRead("chr1", "ACGT", 16);  // read starts after last variant position, but before lookahead
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read9, callableNeighbor2));
            var read10 = TestHelper.CreateRead("chr1", "ACGT", 17); // read starts after last variant position, but before lookahead
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read10, callableNeighbor2));
            var read11 = TestHelper.CreateRead("chr1", "ACGT", 18); // read starts after last variant position, but before lookahead
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read11, callableNeighbor2));
            var read12 = TestHelper.CreateRead("chr1", "ACGT", 19); // read starts after last variant position, but before lookahead
            // Nima: Minimum lookahead is pos+4, so this is still not considered past neighborhood (maybe we should -1 but it doesn't really make a huge diff?)
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read12, callableNeighbor2));
            var read13 = TestHelper.CreateRead("chr1", "ACGT", 20); // read starts after lookahead
            Assert.Equal(true, nbhdReadFilter.PastNeighborhood(read13, callableNeighbor2));


            // Scenario 3: neighborhood with one SNV, and one deletion (similar to Scenario 2)
            var neighbor3 = new VcfNeighborhood(0, "chr1", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(10){VcfReferenceAllele = "A", VcfAlternateAllele = "C"},
                    new VariantSite(15){VcfReferenceAllele = "GAAA", VcfAlternateAllele = "G"},
                },
            };
            
            var callableNeighbor3 = new CallableNeighborhood(neighbor3, new VariantCallingParameters(), null);

            var read14 = TestHelper.CreateRead("chr1", "ACGT", 18); // read starts after last variant position, but before lookahead
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read14, callableNeighbor3));
            var read15 = TestHelper.CreateRead("chr1", "ACGT", 19); // read starts after last variant position, but before lookahead
            // Nima: Minimum lookahead is pos+4, so this is still not considered past neighborhood (maybe we should -1 but it doesn't really make a huge diff?)
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read15, callableNeighbor3));
            var read16 = TestHelper.CreateRead("chr1", "ACGT", 20); // read starts after lookahead
            Assert.Equal(true, nbhdReadFilter.PastNeighborhood(read16, callableNeighbor3));


            // Scenario 4: long indel variant in the beginning of neighborhood can extend lookahead
            var neighbor4 = new VcfNeighborhood(0, "chr1", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(10){VcfReferenceAllele = "A", VcfAlternateAllele = "ATTTTTTT"},
                    new VariantSite(15){VcfReferenceAllele = "G", VcfAlternateAllele = "A"},
                },
            };
            
            var callableNeighbor4 = new CallableNeighborhood(neighbor4, new VariantCallingParameters(), null);


            var read17 = TestHelper.CreateRead("chr1", "ACGT", 16);  // read starts after last variant position, but before lookahead from first variant
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read17, callableNeighbor4));
            var read18 = TestHelper.CreateRead("chr1", "ACGT", 17);  // read starts after last variant position, but before lookahead from first variant
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read18, callableNeighbor4));
            var read19 = TestHelper.CreateRead("chr1", "ACGT", 18);  // read starts after last variant position, but before lookahead from first variant
            Assert.Equal(false, nbhdReadFilter.PastNeighborhood(read19, callableNeighbor4));
            var read20 = TestHelper.CreateRead("chr1", "ACGT", 20);  // read starts after lookahead of first variant
            Assert.Equal(true, nbhdReadFilter.PastNeighborhood(read20, callableNeighbor4));
        }

        [Fact]
        public void ShouldSkipReadTest()
        {
            var nbhdReadFilter = new NeighborhoodReadFilter(new BamFilterParameters() { MinimumMapQuality = 20 });

            var neighbor1 = new VcfNeighborhood( 0, "chr1", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(10){VcfReferenceAllele = "A", VcfAlternateAllele = "C"},
                    new VariantSite(15){VcfReferenceAllele = "G", VcfAlternateAllele = "A"},
                },
            };
            neighbor1.SetRangeOfInterest();
            var callableNeighbor1 = new CallableNeighborhood(neighbor1, new VariantCallingParameters(), null);


            var read1 = TestHelper.CreateRead("chr1", "ACGT", 6);      // Read ends before first variant
            Assert.Equal(true, nbhdReadFilter.ShouldSkipRead(read1, callableNeighbor1));
            var read2 = TestHelper.CreateRead("chr1", "ACGT", 7);      // Read covers 1 base of the nbhd
            Assert.Equal(false, nbhdReadFilter.ShouldSkipRead(read2, callableNeighbor1));
            var read3 = TestHelper.CreateRead("chr1", "ACGT", 12);      // Read partially covers neighborhood
            Assert.Equal(false, nbhdReadFilter.ShouldSkipRead(read3, callableNeighbor1));
            var read4 = TestHelper.CreateRead("chr1", "ACGT", 16);      // Read starts after neighborhood
            Assert.Equal(false, nbhdReadFilter.ShouldSkipRead(read4, callableNeighbor1));

            // Nima: we can maybe add features to CreateRead to be able to create PCR duplicate, low mapQ, and non proper pair reads
            //       but i think these conditions are somewhat trivial, and this may not be necessary.
        }

        [Fact]
        public void ClippedReadCountTest()
        {
            var neighbor1 = new VcfNeighborhood(0, "chr1", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(10){VcfReferenceAllele = "A", VcfAlternateAllele = "C"},
                    new VariantSite(15){VcfReferenceAllele = "G", VcfAlternateAllele = "A"},
                    new VariantSite(25){VcfReferenceAllele = "T", VcfAlternateAllele = "G"},
                },
            };
            neighbor1.SetRangeOfInterest();
            Assert.Equal(9, neighbor1.SoftClipEndBeforeNbhd);
            Assert.Equal(26, neighbor1.SoftClipPosAfterNbhd);

            var callableNeighbor1 = new CallableNeighborhood(neighbor1, new VariantCallingParameters(), null);

            var nbhdReadFilter = new NeighborhoodReadFilter(new BamFilterParameters() { MinimumMapQuality = 20 });

            var cigarMatch = new CigarAlignment("4M");
            var read1 = TestHelper.CreateRead("chr1", "ACGT", 6, cigarMatch);   // No clip, ends before neighborhood starts
            Assert.Equal(false, nbhdReadFilter.IsClippedWithinNeighborhood(read1, callableNeighbor1));
            var read2 = TestHelper.CreateRead("chr1", "ACGT", 8, cigarMatch);   // No clip, partially covers neighborhood
            Assert.Equal(false, nbhdReadFilter.IsClippedWithinNeighborhood(read2, callableNeighbor1));
            var read3 = TestHelper.CreateRead("chr1", "ACGT", 15, cigarMatch);   // No clip, inside neighborhood
            Assert.Equal(false, nbhdReadFilter.IsClippedWithinNeighborhood(read3, callableNeighbor1));

            // Clipped portion of read starts before neighborhood -> NOT within neighborhood
            //  POS     8  9  10 11
            //  Read    M  S  S  S
            var cigar21 = new CigarAlignment("1M3S");
            var read21 = TestHelper.CreateRead("chr1", "ACGT", 8, cigar21);
            Assert.Equal(false, nbhdReadFilter.IsClippedWithinNeighborhood(read21, callableNeighbor1));
            // Clipped portion of read starts on first variant site -> within neighborhood
            //  POS     8  9  10 11
            //  Read    M  M  S  S
            var cigar4 = new CigarAlignment("2M2S");
            var read4 = TestHelper.CreateRead("chr1", "ACGT", 8, cigar4);
            Assert.Equal(true, nbhdReadFilter.IsClippedWithinNeighborhood(read4, callableNeighbor1));
            // Clipped portion of read starts after first variant site but before end of neighborhood -> within neighborhood
            //  POS     8  9  10 11
            //  Read    M  M  M  S
            var cigar5 = new CigarAlignment("3M1S");
            var read5 = TestHelper.CreateRead("chr1", "ACGT", 8, cigar5);   // clipped end matches start of neighborhood
            Assert.Equal(true, nbhdReadFilter.IsClippedWithinNeighborhood(read5, callableNeighbor1));

            // Clipped portion of read ends before end of neighborhood -> within neighborhood
            //  POS     24 25 26 27
            //  Read    S  M  M  M
            var cigar22 = new CigarAlignment("1S3M");
            var read22 = TestHelper.CreateRead("chr1", "ACGT", 25, cigar22);
            Assert.Equal(true, nbhdReadFilter.IsClippedWithinNeighborhood(read22, callableNeighbor1));
            // Clipped portion of read ends at last variant site of neighborhood -> within neighborhood
            //  POS     24 25 26 27
            //  Read    S  S  M  M
            var cigar6 = new CigarAlignment("2S2M");
            var read6 = TestHelper.CreateRead("chr1", "ACGT", 26, cigar6);
            Assert.Equal(true, nbhdReadFilter.IsClippedWithinNeighborhood(read6, callableNeighbor1));

            // Clipped portion of read ends after neighborhood's last variant site -> NOT within neighborhood
            //  POS     24 25 26 27
            //  Read    S  S  S  M
            var cigar7 = new CigarAlignment("3S1M");
            var read7 = TestHelper.CreateRead("chr1", "ACGT", 27, cigar7);
            Assert.Equal(false, nbhdReadFilter.IsClippedWithinNeighborhood(read7, callableNeighbor1));

            // TODO (maybe test in future)
            // Nima: These borders are not very necessary given we don't check exact match in first pass over clipped reads.
            // Testing SoftClip position and End for neighborhoods with deletion
            var neighbor2 = new VcfNeighborhood(0, "chr1", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(10){VcfReferenceAllele = "ACC", VcfAlternateAllele = "A"},
                    new VariantSite(25){VcfReferenceAllele = "TCC", VcfAlternateAllele = "T"},
                },
            };
            neighbor2.SetRangeOfInterest();
            Assert.Equal(10, neighbor2.SoftClipEndBeforeNbhd);
            Assert.Equal(28, neighbor2.SoftClipPosAfterNbhd);

            // Testing SoftClip position and End for neighborhoods with insertion
            var neighbor3 = new VcfNeighborhood(0, "chr1", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(10){VcfReferenceAllele = "A", VcfAlternateAllele = "ACC"},
                    new VariantSite(25){VcfReferenceAllele = "T", VcfAlternateAllele = "TCC"},
                },
            };
            neighbor3.SetRangeOfInterest();
            Assert.Equal(10, neighbor3.SoftClipEndBeforeNbhd);
            Assert.Equal(26, neighbor3.SoftClipPosAfterNbhd);
        }
    }
}