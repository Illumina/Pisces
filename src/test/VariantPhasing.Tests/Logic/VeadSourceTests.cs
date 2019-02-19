using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;
using TestUtilities.MockBehaviors;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Options;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class VeadSourceTests
    {
        [Fact]
        public void GetVeads()
        {

            var vcfNeighborhood =new VcfNeighborhood(0, "chr1", new VariantSite(10000), new VariantSite(200000))
            {
                VcfVariantSites = new List<VariantSite>
                {
                    new VariantSite(100){VcfReferenceAllele = "A", VcfAlternateAllele = "C"},
                    new VariantSite(400){VcfReferenceAllele = "A", VcfAlternateAllele = "C"},
                    new VariantSite(505){VcfReferenceAllele = "A", VcfAlternateAllele = "C"},
                    new VariantSite(703){VcfReferenceAllele = "A", VcfAlternateAllele = "T"},
                    new VariantSite(800){VcfReferenceAllele = "A", VcfAlternateAllele = "C"},
                }
            };

            var callableNeighborhood = new CallableNeighborhood(vcfNeighborhood, new VariantCallingParameters());

            var reads = new List<Read>();
            reads.Add(CreateRead("chr1", "ACGT", 10)); // Before neighborhood
            reads.Add(CreateRead("chr1", "ACGT", 96)); // Ends right before neighborhood's first variant site
            reads.Add(CreateRead("chr1", "ACGT", 100)); // Match (100)
            reads.Add(CreateRead("chr1", "ACGT", 300)); // Within neighborhood but no VariantSite
            reads.Add(CreateRead("chr1", "ACGT", 400, qualityForAll: 19)); // Within neighbhorhood but low quals
            reads.Add(CreateRead("chr1", "ACGT", 500)); // Within neighborhood but no VariantSite (ends right before 505)
            reads.Add(CreateRead("chr1", "ACGT", 700)); // Match (703)
            reads.Add(CreateRead("chr1", "ACGT", 800)); // Match (800)
            reads.Add(CreateRead("chr1", "ACGT", 805)); // Past neighborhood
            reads.Add(CreateRead("chr1", "ACGT", 900)); // Past neighborhood
            reads.Add(CreateRead("chr2", "ACGT", 100)); // Wrong chromosome




            var alignmentExtractor = new MockAlignmentExtractor(reads);


            var veadSource = new VeadGroupSource(alignmentExtractor,
                new BamFilterParameters() { MinimumMapQuality = 20 }, false, "");

            var veadGroups = veadSource.GetVeadGroups(callableNeighborhood);

            // Collect all reads that could relate to the neighborhood
            // - Skip anything that has quality less than MinimumMapQuality
            // - Skip anything that ends before neighborhood begins
            // - Stop collecting once we've passed the end of the neighborhood

            // We should have collected the reads at 100, 700, and 800.
            Assert.Equal(801, callableNeighborhood.LastPositionOfInterestWithLookAhead);
            Assert.Equal(3, veadGroups.Count());
            Assert.Equal(1, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("100")));
            Assert.Equal(1, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("700")));
            Assert.Equal(1, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("800")));
            Assert.Equal(0, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("805")));
            Assert.Equal(0, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("900")));
            foreach (var veadGroup in veadGroups)
            {
                Assert.Equal(1, veadGroup.NumVeads);
            }

            vcfNeighborhood.VcfVariantSites.Add(
                new VariantSite(790) { VcfReferenceAllele = "ACAGTGAAAGACTTGTGAC", VcfAlternateAllele = "C" });

            callableNeighborhood = new CallableNeighborhood(vcfNeighborhood, new VariantCallingParameters());


            Assert.Equal(809, callableNeighborhood.LastPositionOfInterestWithLookAhead);

            alignmentExtractor = new MockAlignmentExtractor(reads);


            veadSource = new VeadGroupSource(alignmentExtractor,
                new BamFilterParameters() { MinimumMapQuality = 20 }, false, "");

            veadGroups = veadSource.GetVeadGroups(callableNeighborhood);

            Assert.Equal(3, veadGroups.Count());
            Assert.Equal(1, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("100")));
            Assert.Equal(1, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("700")));
            Assert.Equal(1, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("800")));
            Assert.Equal(0, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("805")));
            Assert.Equal(0, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("900")));

            // Boundary case - read ends exactly at neighborhood's first variant site

            reads = new List<Read>();
            reads.Add(CreateRead("chr1", "ACGT", 10)); // Before neighborhood
            reads.Add(CreateRead("chr1", "ACGT", 96)); // Ends right before neighborhood's first variant site
            reads.Add(CreateRead("chr1", "ACGT", 97)); // Ends exactly at neighborhood's first variant site

            alignmentExtractor = new MockAlignmentExtractor(reads);

            veadSource = new VeadGroupSource(alignmentExtractor, new BamFilterParameters() { MinimumMapQuality = 20 }, false, "");

            veadGroups = veadSource.GetVeadGroups(callableNeighborhood);

            // The veadgroup for 97 should be the only one
            Assert.Equal(1, veadGroups.Count());
            Assert.Equal(1, veadGroups.Count(v => v.RepresentativeVead.Name.EndsWith("97")));
            foreach (var veadGroup in veadGroups)
            {
                Assert.Equal(1, veadGroup.NumVeads);
            }

        }

        [Fact]
        public void GroupVeads()
        {
            // ALL READS ARE AT THE SAME POSITION IN THE FOLLOWING TESTS. 
            // ----------------------------------------------------
            // Four Ns
            //  - This is from original "FourNs Test"
            // ----------------------------------------------------

            // All 
            var veads = new List<Read>()
            {
                CreateReadFromStringArray(new string[2,2]{{"C","C"},{"G","N"}}),
                CreateReadFromStringArray(new string[2,2]{{"C","C"},{"G","N"}}),
                CreateReadFromStringArray(new string[2,2]{{"C","C"},{"G","N"}}),
                CreateReadFromStringArray(new string[2,2]{{"C","C"},{"G","N"}}),
            };


            ExecuteGroupingTest(veads, new List<int>() { 4 }, new List<Tuple<int, string, string>>()
            {
                new Tuple<int, string, string>(100,"C","C"),
                new Tuple<int, string, string>(101,"G","N")
            });

            // ----------------------------------------------------
            // Real Data
            //  - This data is from Sample 129 (original "Sample129Test")
            // ----------------------------------------------------

            veads = new List<Read>()
            {
                CreateReadFromStringArray(new string[2,2]{{"A","G"},{"N","N"}}),
                CreateReadFromStringArray(new string[2,2]{{"A","G"},{"C","C"}}),
                CreateReadFromStringArray(new string[2,2]{{"A","A"},{"C","C"}}),
                CreateReadFromStringArray(new string[2,2]{{"A","G"},{"C","A"}}),
                CreateReadFromStringArray(new string[2,2]{{"N","N"},{"C","C"}}),
                CreateReadFromStringArray(new string[2,2]{{"N","N"},{"C","A"}}),
            };

            ExecuteGroupingTest(veads, new List<int>() { 1, 1, 1, 1, 1, 1 }, new List<Tuple<int, string, string>>()
            {
                new Tuple<int, string, string>(100,"A","G"),
                new Tuple<int, string, string>(100,"A","A"),
                new Tuple<int, string, string>(100,"N","N"),
                new Tuple<int, string, string>(101,"N","N"),
                new Tuple<int, string, string>(101,"C","A"),
                new Tuple<int, string, string>(101,"C","C"),
            });

            // ----------------------------------------------------
            // Ten grouped reads
            //  - This is from original "10 ReadsTest"
            // ----------------------------------------------------

            veads = new List<Read>()
            {
                CreateReadFromStringArray(new[,]{{"N","N"},{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"C","A"}}),
                CreateReadFromStringArray(new string[6,2]{{"N","N"},{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"C","A"}}),
                CreateReadFromStringArray(new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
                CreateReadFromStringArray(new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
                CreateReadFromStringArray(new string[6,2]{{"N","N"},{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"C","A"}}),
                CreateReadFromStringArray(new string[6,2]{{"C","C"},{"C","C"},{"C","C"},{"C","C"},{"C","C"},{"C","C"}}),
                CreateReadFromStringArray(new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
                CreateReadFromStringArray(new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
                CreateReadFromStringArray(new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
                CreateReadFromStringArray(new string[6,2]{{"C","A"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
            };

            ExecuteGroupingTest(veads, new List<int>() { 3, 5, 1, 1 }, new List<Tuple<int, string, string>>
            {
                new Tuple<int, string, string>(100,"C","C"),
                new Tuple<int, string, string>(100,"C","A"),
                new Tuple<int, string, string>(100,"N","N"),

                new Tuple<int, string, string>(101,"N","N"),
                new Tuple<int, string, string>(101,"C","A"),
                new Tuple<int, string, string>(101,"C","C"),

                new Tuple<int, string, string>(102,"C","A"),
                new Tuple<int, string, string>(103,"C","A"),

                new Tuple<int, string, string>(104,"N","N"),
                new Tuple<int, string, string>(104,"C","A"),
                new Tuple<int, string, string>(104,"C","C"),

                new Tuple<int, string, string>(105,"C","A"),

            });

        }

        private void ExecuteGroupingTest(List<Read> reads, List<int> expectedGroupMemberships, IEnumerable<Tuple<int, string, string>> variants)
        {
            var variantSites = new List<VariantSite>();
            foreach (var variant in variants)
            {
                variantSites.Add(new VariantSite(variant.Item1) { VcfReferenceAllele = variant.Item2, VcfAlternateAllele = variant.Item3 });
            }

            var alignmentExtractor = new MockAlignmentExtractor(reads);

            var veadSource = new VeadGroupSource(alignmentExtractor, new BamFilterParameters() { MinimumMapQuality = 20 }, false, "");
            var vcfNeighborhood = new VcfNeighborhood(0, "chr1", new VariantSite(120), new VariantSite(121))
            {
                VcfVariantSites = variantSites
            };

            var callableNeighborhood = new CallableNeighborhood(vcfNeighborhood, new VariantCallingParameters());

            var veadGroups = veadSource.GetVeadGroups(callableNeighborhood).ToList();

            Assert.Equal(expectedGroupMemberships.Count, veadGroups.Count());
            for (var i = 0; i < veadGroups.Count(); i++)
            {
                Assert.Equal(expectedGroupMemberships[i], veadGroups[i].NumVeads);
            }
        }


        private static Read CreateReadFromStringArray(string[,] variantSites, int position = 100)
        {
            var readSequence = "";
            for (var index00 = 0; index00 < variantSites.GetLength(0); index00++)
            {
                readSequence += variantSites[index00, 1];
            }

            return CreateRead("chr1", readSequence, position);
        }

        public static Read CreateRead(string chr, string sequence, int position,
    CigarAlignment cigar = null, byte[] qualities = null, int matePosition = 0, byte qualityForAll = 30)
        {
            return new Read(chr,
                new BamAlignment
                {
                    Bases = sequence,
                    Position = position - 1,
                    CigarData = cigar ?? new CigarAlignment(sequence.Length + "M"),
                    Qualities = qualities ?? Enumerable.Repeat(qualityForAll, sequence.Length).ToArray(),
                    MatePosition = matePosition - 1,
                    MapQuality = qualityForAll
                }
                );
        }

    }
}