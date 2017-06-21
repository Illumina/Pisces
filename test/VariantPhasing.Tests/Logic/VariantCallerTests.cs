using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Options;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class VariantCallerTests
    {

        [Fact]
        public void CallThroughAnEmptyNbhd()
        {
            var originalVcfVariant = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 123, "A", "T", 1000, 156);
            var originalVcfVariant2 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 124, "A", "T", 1000, 156);
            var vs1 = new VariantSite(originalVcfVariant);
            var vs2 = new VariantSite(originalVcfVariant2);

            var caller = new VariantCaller(new VariantCallingParameters(), new BamFilterParameters());

            //since there is an alt at position 124 ( a call of 156 alt / 1000 total, that means 844 original ref calls.
            //Of which we said, 100 will get sucked up. So that leaves 744 / 1000 calls for a reference.
            //So, we can still make a confident ref call. (we will call it 0/., since we know its not a homozygous ref)

            var nbhd = new VcfNeighborhood(new VariantCallingParameters(), "chr1", vs1, vs2, "");
            nbhd.SetRangeOfInterest();

            caller.CallMNVs(nbhd);
            caller.CallRefs(nbhd);

            var acceptedMNVs = nbhd.CalledVariants;
            var acceptedRefs = nbhd.CalledRefs;

            Assert.Equal(0, acceptedMNVs.Count);
            Assert.Equal(2, acceptedRefs.Count);

            Assert.Equal(Genotype.RefAndNoCall, acceptedRefs[123].Genotype);
            Assert.Equal(Genotype.RefAndNoCall, acceptedRefs[124].Genotype);
            Assert.Equal(123, acceptedRefs[123].ReferencePosition);
            Assert.Equal(124, acceptedRefs[124].ReferencePosition);

        }

        [Fact]
        public void VarCallsBecomeRefsAndNulls()
        {

            var originalVcfVariant = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 123, "A", "T", 1000, 156);
            var originalVcfVariant2 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 124, "A", "T", 1000, 156);
            var vs1 = new VariantSite(originalVcfVariant);
            var vs2 = new VariantSite(originalVcfVariant2);

            var caller = new VariantCaller(new VariantCallingParameters(), new BamFilterParameters());

            //since there is an alt at position 124 ( a call of 156 alt / 1000 total, that means 844 original ref calls.
            //Of which we said, 100 will get sucked up. So that leaves 744 / 1000 calls for a reference.
            //So, we can still make a confident ref call. 

            var nbhd = new VcfNeighborhood(new VariantCallingParameters(), "chr1", vs1, vs2, "");
            nbhd.SetRangeOfInterest();
            nbhd.AddAcceptedPhasedVariant(
                new CalledAllele(AlleleCategory.Snv) {
                    Chromosome = "chr1", ReferencePosition = 123, ReferenceAllele = "A", AlternateAllele = "T", VariantQscore = 100, TotalCoverage = 1000, AlleleSupport = 500 });
        nbhd.UsedRefCountsLookup = new Dictionary<int, int>() { };

            caller.CallMNVs(nbhd);
            caller.CallRefs(nbhd);

            var acceptedMNVs = nbhd.CalledVariants;
            var acceptedRefs = nbhd.CalledRefs;

            Assert.Equal(1, acceptedMNVs.Count);
            Assert.Equal(1, acceptedMNVs[123].Count);

            Assert.Equal(2, acceptedRefs.Count);


            var vcfVariant2asRef = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 124,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>()
                { new Dictionary<string, string>() {{"GT", "0/."},{"DP", "1000"},{"AD", "844"} }},
            };

            VcfMergerTests.CheckVariantsMatch(originalVcfVariant, acceptedMNVs[123][0]);
            VcfMergerTests.CheckVariantsMatch(vcfVariant2asRef, acceptedRefs[124]);

            // If one has been sucked up and there are refs remaining, we should output it as a ref. 
            nbhd.UsedRefCountsLookup = new Dictionary<int, int>() { { 124, 100 } };


            caller.CallMNVs(nbhd);
            caller.CallRefs(nbhd);

            acceptedMNVs = nbhd.CalledVariants;
            acceptedRefs = nbhd.CalledRefs;

            Assert.Equal(1, acceptedMNVs.Count);
            Assert.Equal(1, acceptedMNVs[123].Count);

            Assert.Equal(2, acceptedRefs.Count);

            vcfVariant2asRef = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 124,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>()
                { new Dictionary<string, string>() {{"GT", "0/."},{"DP", "1000"},{"AD", "744"} }},
            };

            VcfMergerTests.CheckVariantsMatch(originalVcfVariant, acceptedMNVs[123][0]);
            VcfMergerTests.CheckVariantsMatch(vcfVariant2asRef, acceptedRefs[124]);


            // If one has been sucked up all the way 
            // we should output it as a null. 
            nbhd.UsedRefCountsLookup = new Dictionary<int, int>() { { 124, 1000 } };


            caller.CallMNVs(nbhd);
            caller.CallRefs(nbhd);

            acceptedMNVs = nbhd.CalledVariants;
            acceptedRefs = nbhd.CalledRefs;

            Assert.Equal(1, acceptedMNVs.Count);
            Assert.Equal(1, acceptedMNVs[123].Count);

            Assert.Equal(2, acceptedRefs.Count);

            var vcfVariant2asNull = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 124,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>()
                { new Dictionary<string, string>() {{"GT", "./."},{"DP", "1000"},{"AD", "0"} }},
            };

            VcfMergerTests.CheckVariantsMatch(originalVcfVariant, acceptedMNVs[123][0]);
            VcfMergerTests.CheckVariantsMatch(vcfVariant2asNull, acceptedRefs[124]);



        }

        // new test-request from code review:
        //If we found a new MNV, not in a sucked-up position,
        //make sure we do not over-write an existing MNV that was not used for variant calling 
        //
        //In this example, original variants are at positions123, 124, and two at position 234
        //We will say one sucked up variant is at 123, another at 234, one variant to keep is at 234.
        //And one new MNV is at position 229. Let 124 become a no call.
       
        [Fact]
        public void CallAVariantInANewLocation()
        {
            //set up the original variants
            var originalVcfVariant1 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 123, "A", "T", 1000, 156);
            var originalVcfVariant2 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 124, "A", "T", 1000, 156);
            var originalVcfVariant3 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 234, "A", "T", 1000, 156);
            var originalVcfVariant4 = PhasedVariantTestUtilities.CreateDummyAllele("chr1", 234, "A", "T", 1000, 156);

            var vs1 = new VariantSite(originalVcfVariant1);
            var vs2 = new VariantSite(originalVcfVariant2);
            var vs3 = new VariantSite(originalVcfVariant3);
            var vs4 = new VariantSite(originalVcfVariant4);

            var caller = new VariantCaller(new VariantCallingParameters(), new BamFilterParameters());
            var nbhd = new VcfNeighborhood(new VariantCallingParameters(), "chr1", vs1, vs2, "");
            nbhd.AddVariantSite(vs3, "RRRRR"); //note, we do not add vs4, that is not goig to get used for phasing. Sps it is a variant that failed filters.
            nbhd.SetRangeOfInterest();

            //now stage one candidate MNV:
            var newMNV = new CalledAllele(AlleleCategory.Snv) {
                Chromosome = "chr1", ReferencePosition = 129, ReferenceAllele = "A", AlternateAllele = "TT", VariantQscore = 100, TotalCoverage = 1000, AlleleSupport = 500 };


            nbhd.AddAcceptedPhasedVariant(newMNV);
            nbhd.UsedRefCountsLookup = new Dictionary<int, int>() { { 124, 1000 } };

            caller.CallMNVs(nbhd);
            caller.CallRefs(nbhd);

            var acceptedMNVs = nbhd.CalledVariants;
            var acceptedRefs = nbhd.CalledRefs;


            var vcfVariant0asRef = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 123,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() { { "GT", "0/." } } },
            };

            var vcfVariant3asRef = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 234,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() { { "GT", "0/." } } },
            };

            var vcfVariant2asNull = new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = 124,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "." },
                Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() { { "GT", "./." } } },
            };

            Assert.Equal(1, acceptedMNVs.Count);
            Assert.Equal(1, acceptedMNVs[129].Count);

            Assert.Equal(3, acceptedRefs.Count);

            VcfMergerTests.CheckVariantsMatch(vcfVariant0asRef, acceptedRefs[123]);
            VcfMergerTests.CheckVariantsMatch(vcfVariant2asNull, acceptedRefs[124]);
            VcfMergerTests.CheckVariantsMatch(newMNV, acceptedMNVs[129][0]);
            VcfMergerTests.CheckVariantsMatch(vcfVariant3asRef, acceptedRefs[234]);

        }
    }
}
