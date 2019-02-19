using System.Collections.Generic;
using System.Linq;
using System.IO;
using Moq;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using VariantPhasing.Interfaces;
using Pisces.Domain.Options;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.IO;
using TestUtilities;
using Xunit;

namespace VariantPhasing.Tests.Models
{
    public class VcfNeighborhoodTests
    {
        [Fact]
        public void VcfNeighborhood()
        {
            var nbhd = new VcfNeighborhood(0, "chr1", new VariantSite(123), new VariantSite(124));
            Assert.Equal("NbhdNum0_chr1_123", nbhd.Id);
            Assert.Equal("chr1", nbhd.ReferenceName);
        }


        [Fact]
        public void SortSites()
        {
            var nbhd = new VcfNeighborhood(0, "chr1", new VariantSite(120) { VcfReferenceAllele = "A" }, new VariantSite(121));
            var variantSite1 = new VariantSite(123);
            variantSite1.VcfReferencePosition = 140453137;
            variantSite1.VcfReferenceAllele = "C";
            variantSite1.VcfAlternateAllele = "CGTA";
            variantSite1.OriginalAlleleFromVcf = new Pisces.Domain.Models.Alleles.CalledAllele() { ReferencePosition = 7 };
            nbhd.AddVariantSite(variantSite1);

            var variantSite2 = new VariantSite();
            variantSite2.VcfReferencePosition = 140453137;
            variantSite2.VcfReferenceAllele = "C";
            variantSite2.VcfAlternateAllele = "T";
            variantSite2.OriginalAlleleFromVcf = new Pisces.Domain.Models.Alleles.CalledAllele() { ReferencePosition = 8 };
            nbhd.AddVariantSite(variantSite2);

            var variantSite3 = new VariantSite();
            variantSite3.VcfReferencePosition = 140453130;
            variantSite3.VcfReferenceAllele = "C";
            variantSite3.VcfAlternateAllele = "T";
            variantSite3.OriginalAlleleFromVcf = new Pisces.Domain.Models.Alleles.CalledAllele() { ReferencePosition = 9 };
            nbhd.AddVariantSite(variantSite3);

            Assert.Equal(5, nbhd.VcfVariantSites.Count);

            Assert.Equal(120, nbhd.VcfVariantSites[0].VcfReferencePosition);
            Assert.Equal(120, nbhd.VcfVariantSites[0].TrueFirstBaseOfDiff);
            Assert.Equal("A", nbhd.VcfVariantSites[0].VcfReferenceAllele);
            Assert.Equal("N", nbhd.VcfVariantSites[0].VcfAlternateAllele);

            Assert.Equal(121, nbhd.VcfVariantSites[1].VcfReferencePosition);
            Assert.Equal(121, nbhd.VcfVariantSites[1].TrueFirstBaseOfDiff);
            Assert.Equal("N", nbhd.VcfVariantSites[1].VcfReferenceAllele);
            Assert.Equal("N", nbhd.VcfVariantSites[1].VcfAlternateAllele);

            Assert.Equal(140453137, nbhd.VcfVariantSites[2].VcfReferencePosition);
            Assert.Equal(140453138, nbhd.VcfVariantSites[2].TrueFirstBaseOfDiff);
            Assert.Equal("C", nbhd.VcfVariantSites[2].VcfReferenceAllele);
            Assert.Equal("CGTA", nbhd.VcfVariantSites[2].VcfAlternateAllele);

            Assert.Equal(140453137, nbhd.VcfVariantSites[3].VcfReferencePosition);
            Assert.Equal(140453137, nbhd.VcfVariantSites[3].TrueFirstBaseOfDiff);
            Assert.Equal("C", nbhd.VcfVariantSites[3].VcfReferenceAllele);
            Assert.Equal("T", nbhd.VcfVariantSites[3].VcfAlternateAllele);

            Assert.Equal(140453130, nbhd.VcfVariantSites[4].VcfReferencePosition);
            Assert.Equal(140453130, nbhd.VcfVariantSites[4].TrueFirstBaseOfDiff);
            Assert.Equal("C", nbhd.VcfVariantSites[4].VcfReferenceAllele);
            Assert.Equal("T", nbhd.VcfVariantSites[4].VcfAlternateAllele);

            nbhd.OrderVariantSitesByFirstTrueStartPosition();

            Assert.Equal(120, nbhd.VcfVariantSites[0].VcfReferencePosition);
            Assert.Equal(120, nbhd.VcfVariantSites[0].TrueFirstBaseOfDiff);
            Assert.Equal("A", nbhd.VcfVariantSites[0].VcfReferenceAllele);
            Assert.Equal("N", nbhd.VcfVariantSites[0].VcfAlternateAllele);

            Assert.Equal(121, nbhd.VcfVariantSites[1].VcfReferencePosition);
            Assert.Equal(121, nbhd.VcfVariantSites[1].TrueFirstBaseOfDiff);
            Assert.Equal("N", nbhd.VcfVariantSites[1].VcfReferenceAllele);
            Assert.Equal("N", nbhd.VcfVariantSites[1].VcfAlternateAllele);

            Assert.Equal(140453130, nbhd.VcfVariantSites[2].VcfReferencePosition);
            Assert.Equal(140453130, nbhd.VcfVariantSites[2].TrueFirstBaseOfDiff);
            Assert.Equal("C", nbhd.VcfVariantSites[2].VcfReferenceAllele);
            Assert.Equal("T", nbhd.VcfVariantSites[2].VcfAlternateAllele);
            Assert.Equal(7, nbhd.VcfVariantSites[2].OriginalAlleleFromVcf.ReferencePosition);

            Assert.Equal(140453137, nbhd.VcfVariantSites[3].VcfReferencePosition);
            Assert.Equal(140453137, nbhd.VcfVariantSites[3].TrueFirstBaseOfDiff);
            Assert.Equal("C", nbhd.VcfVariantSites[3].VcfReferenceAllele);
            Assert.Equal("T", nbhd.VcfVariantSites[3].VcfAlternateAllele);
            Assert.Equal(8, nbhd.VcfVariantSites[3].OriginalAlleleFromVcf.ReferencePosition);

            Assert.Equal(140453137, nbhd.VcfVariantSites[4].VcfReferencePosition);
            Assert.Equal(140453138, nbhd.VcfVariantSites[4].TrueFirstBaseOfDiff);
            Assert.Equal("C", nbhd.VcfVariantSites[4].VcfReferenceAllele);
            Assert.Equal("CGTA", nbhd.VcfVariantSites[4].VcfAlternateAllele);
            Assert.Equal(9, nbhd.VcfVariantSites[4].OriginalAlleleFromVcf.ReferencePosition);
        }

        [Fact]
        public void AddVariantSite()
        {
            var nbhd = new VcfNeighborhood(0, "chr1", new VariantSite(120) { VcfReferenceAllele = "A" }, new VariantSite(121));
            Assert.Equal("NbhdNum0_chr1_120", nbhd.Id);

            var variantSite = new VariantSite(123);
            nbhd.AddVariantSite(variantSite);
            Assert.Equal(3, nbhd.VcfVariantSites.Count);
            Assert.Equal("NbhdNum0_chr1_120", nbhd.Id);
        }

        [Fact]
        //this unit test was made after we found bug ScyllaLoosingRefCalls_PICS-723.
        //We had a 1/. GT reported when it should be 1/0.
        //The reason for this is that all the refs (the "0"s) got incorrectly sucked up.
        //Ie, MNV ACG-> AG claimed 50 refs, so we (incorrectly) subtracted 50 refs from it.
        //The bug is that the ref counts got subtractedfrom the exact same mnv that claimed them. 
        // This should never happen, and was not the intent of the alg. 
        //
        //The affected mehtod is: CreateMnvsFromClusters in VcfNbhd
        public void CreateMnvsFromClusters_TakeUpRefCount()
        {
            var originalVcfVariant1 = TestHelper.CreateDummyAllele("chr1", 123, "ACG", "AT", 1000, 156);
            var originalVcfVariant2 = TestHelper.CreateDummyAllele("chr1", 123, "A", "TTTTTT", 1000, 200);
            // var originalVcfVariant3 = TestHelper.CreateDummyAllele("chr1", 123, "AC", "TT", 1000, 100);

            var vs1 = new VariantSite(originalVcfVariant1);
            var vs2 = new VariantSite(originalVcfVariant2);

            var caller = new VariantCaller(new VariantCallingParameters(), new BamFilterParameters());
            var nbhd = new VcfNeighborhood( 0, "chr1", vs1, vs2);
            var callableNeighborhood = new CallableNeighborhood(nbhd, new VariantCallingParameters());

            callableNeighborhood.AddAcceptedPhasedVariant(
                new CalledAllele(AlleleCategory.Snv)
                {
                    Chromosome = "chr1",
                    ReferencePosition = 123,
                    ReferenceAllele = "A",
                    AlternateAllele = "A",
                    VariantQscore = 100,
                    TotalCoverage = 1000,
                    AlleleSupport = 200,
                    ReferenceSupport = 350
                });


            callableNeighborhood.AddAcceptedPhasedVariant(
             new CalledAllele(AlleleCategory.Mnv)
             {
                 Chromosome = "chr1",
                 ReferencePosition = 123,
                 ReferenceAllele = "ACG",
                 AlternateAllele = "AT",
                 VariantQscore = 100,
                 TotalCoverage = 1000,
                 AlleleSupport = 300,
                 ReferenceSupport = 350
             });

            callableNeighborhood.AddAcceptedPhasedVariant(
         new CalledAllele(AlleleCategory.Insertion)
         {
             Chromosome = "chr1",
             ReferencePosition = 123,
             ReferenceAllele = "A",
             AlternateAllele = "AAAAA",
             VariantQscore = 100,
             TotalCoverage = 1000,
             AlleleSupport = 250,
             ReferenceSupport = 350
         });


            //default behavior, nothing gets sucked up
            callableNeighborhood.UsedRefCountsLookup = new Dictionary<int, SuckedUpRefRecord>() { };
            vs1.VcfReferencePosition = 123;
            var vead = new Vead("dummy", new VariantSite[] { vs1 });
            var vg = new VeadGroup(vead);
            var fakeCluster = new Cluster("test", new List<VeadGroup>() { vg });
            fakeCluster.ResetConsensus();
            callableNeighborhood.CreateMnvsFromClusters(new List<Cluster> { fakeCluster },
                20, 100);
            caller.CallMNVs(callableNeighborhood);
            caller.CallRefs(callableNeighborhood);

            var acceptedMNVs = callableNeighborhood.CalledVariants;
            var acceptedRefs = callableNeighborhood.CalledRefs;

            Assert.Equal(2, acceptedMNVs.Count);
            Assert.Equal(3, acceptedMNVs[123].Count);
            Assert.Equal(1, acceptedRefs.Count);

            //check the ref counts on all the MNVs. Nothing should be sucked up.
            Assert.Equal(350, acceptedMNVs[123][0].ReferenceSupport);  // Previously: total depth - allele suport. overly simple for now)
            Assert.Equal(350, acceptedMNVs[123][1].ReferenceSupport);  // Now: explicitly set ref support
            Assert.Equal(350, acceptedMNVs[123][2].ReferenceSupport);  //

            // now variant 0 will suck up 100 ref calls:
            var suckedUpRefRecord100 = new SuckedUpRefRecord() { Counts = 100, AlleleThatClaimedIt = callableNeighborhood.CandidateVariants[0] };
            callableNeighborhood.UsedRefCountsLookup = new Dictionary<int, SuckedUpRefRecord>() { { 123, suckedUpRefRecord100 } };
            callableNeighborhood.CreateMnvsFromClusters(new List<Cluster> { fakeCluster },
            20, 100);

            caller.CallMNVs(callableNeighborhood);
            caller.CallRefs(callableNeighborhood);

            acceptedMNVs = callableNeighborhood.CalledVariants;
            acceptedRefs = callableNeighborhood.CalledRefs;


            //check the ref counts on all the MNVs. refs should only be taken up by the first one
            Assert.Equal(350, acceptedMNVs[123][0].ReferenceSupport);  //Previously:  total depth - allele suport. overly simple for now)

            //old result - has bug
            //Assert.Equal(1000 - 300, acceptedMNVs[123][1].ReferenceSupport);  // Previously:  total depth - allele suport - sucked up ref)
            //Assert.Equal(1000 - 250, acceptedMNVs[123][2].ReferenceSupport);  // Now: explicitly set ref support

            //new result, fixed
            Assert.Equal(350 - 100, acceptedMNVs[123][1].ReferenceSupport);  // refSupport - sucked up ref)
            Assert.Equal(350 - 100, acceptedMNVs[123][2].ReferenceSupport);  // refSupport - sucked up ref)

        }


        [Fact]
        public void AddMnvsFromClusters()
        {
            //TODO even with mock cluster this takes too much setting up.
            var nbhd = new VcfNeighborhood( 0, "chr1", new VariantSite(120), new VariantSite(121));

            var vead = PhasedVariantTestUtilities.CreateVeadFromStringArray("r1", new[,] { { "C", "A" }, { "G", "A" }, { "T", "A" } });
            var vead2 = PhasedVariantTestUtilities.CreateVeadFromStringArray("r2", new[,] { { "C", "A" }, { "G", "A" }, { "T", "A" } });
            var vead3 = PhasedVariantTestUtilities.CreateVeadFromStringArray("r3", new[,] { { "C", "A" }, { "G", "A" }, { "T", "A" } });
            var veads = new List<Vead> { vead, vead2, vead3 };

            vead.SiteResults[0].VcfReferencePosition = 1;
            vead.SiteResults[1].VcfReferencePosition = 2;
            vead.SiteResults[2].VcfReferencePosition = 3;

            vead2.SiteResults[0].VcfReferencePosition = 1;
            vead2.SiteResults[1].VcfReferencePosition = 2;
            vead2.SiteResults[2].VcfReferencePosition = 3;

            vead3.SiteResults[0].VcfReferencePosition = 1;
            vead3.SiteResults[1].VcfReferencePosition = 2;
            vead3.SiteResults[2].VcfReferencePosition = 3;

            var mockCluster = new Mock<ICluster>();
            mockCluster.Setup(c => c.CountsAtSites).Returns(new[] { 10, 3, 5 });
            var consensus = PhasedVariantTestUtilities.CreateVeadGroup(veads);
            mockCluster.Setup(c => c.GetConsensusSites()).Returns(consensus.SiteResults);
            mockCluster.Setup(c => c.GetVeadGroups()).Returns(new List<VeadGroup>() { consensus });

            var callableNeighborhood = new CallableNeighborhood(nbhd, new VariantCallingParameters());
            callableNeighborhood.NbhdReferenceSequenceSubstring = "CGT";
            callableNeighborhood.CreateMnvsFromClusters(new List<ICluster>() { mockCluster.Object }, 20, 100);

            var allele = callableNeighborhood.CandidateVariants.First();
            Assert.Equal(6, allele.TotalCoverage);
            Assert.Equal(6, allele.AlleleSupport);
            Assert.Equal("CGT", allele.ReferenceAllele);
            Assert.Equal("AAA", allele.AlternateAllele);

            int[] depths = new int[0];
            int[] nocalls = new int[0];
            callableNeighborhood.DepthAtSites(new List<ICluster>() { mockCluster.Object }, out depths, out nocalls);
            Assert.Equal(3, depths.Length);
            Assert.Equal(3, depths[0]);
            Assert.Equal(3, depths[1]);
            Assert.Equal(3, depths[2]);
        }

        [Fact]
        public void LastPositionIsNotMatch()
        {
            var nbhd = new VcfNeighborhood(0, "chr1", new VariantSite(120), new VariantSite(121));
            var variantSite = new VariantSite(123);
            nbhd.AddVariantSite(variantSite);

            var vsPositionMatch = new VariantSite(123);
            Assert.False(nbhd.LastPositionIsNotMatch(vsPositionMatch));

            var vsPositionMismatch = new VariantSite(124);
            Assert.True(nbhd.LastPositionIsNotMatch(vsPositionMismatch));
        }

        [Fact]
        public void GetOriginalVcfIndexes()
        {

            var originalVar1 = new CalledAllele() { ReferencePosition = 1 };
            var originalVar10 = new CalledAllele() { ReferencePosition = 10 };

            var nbhd = new VcfNeighborhood(0, "chr1", new VariantSite(123) { OriginalAlleleFromVcf = originalVar1 }
                , new VariantSite(123) { OriginalAlleleFromVcf = originalVar10 });

            var originalVcfIndexes =  new CallableNeighborhood( nbhd, new VariantCallingParameters()).GetOriginalVcfVariants();
            Assert.Equal(2, originalVcfIndexes.Count);
            Assert.Equal(1, originalVcfIndexes[0].ReferencePosition);
            Assert.Equal(10, originalVcfIndexes[1].ReferencePosition);
        }

        [Fact]
        public void SetDepthAtSites()
        {
            var nbhd = new VcfNeighborhood(0, "chr1", new VariantSite(120), new VariantSite(121));

            var vead = PhasedVariantTestUtilities.CreateVeadFromStringArray("r1", new[,] { { "C", "A" }, { "G", "A" }, { "T", "A" } });
            var vead2 = PhasedVariantTestUtilities.CreateVeadFromStringArray("r2", new[,] { { "C", "A" }, { "G", "A" }, { "T", "A" } });
            var vead3 = PhasedVariantTestUtilities.CreateVeadFromStringArray("r3", new[,] { { "C", "A" }, { "G", "A" }, { "T", "A" } });
            var veads = new List<Vead> { vead, vead2, vead3 };
        }



        [Fact]
       
        public void SetRangeOfInterestTests()
        {
            /// <summary>
            /// This method sets the NbdhReferenceSequenceSubstring, and the first/last positions of interest
            /// </summary>

            var refName = "chr";

            //test with no Genome given
            var nbhd = new VcfNeighborhood( 0, refName, new VariantSite(120), new VariantSite(121));

            Assert.Equal(-1, nbhd.FirstPositionOfInterest);
            Assert.Equal(-1, nbhd.LastPositionOfInterestInVcf);
            Assert.Equal(-1, nbhd.LastPositionOfInterestWithLookAhead);

            var readyNbhd = new CallableNeighborhood(nbhd, new VariantCallingParameters());
            Assert.Equal("RR", readyNbhd.NbhdReferenceSequenceSubstring);
            Assert.Equal(120, nbhd.FirstPositionOfInterest);
            Assert.Equal(121, nbhd.LastPositionOfInterestInVcf);
            Assert.Equal(122, nbhd.LastPositionOfInterestWithLookAhead);

            //test with a genome given
            var genomePath = Path.Combine(TestPaths.SharedGenomesDirectory, "Bacillus_cereus", "Sequence", "WholeGenomeFasta");
            Genome genome = new Genome(genomePath, new List<string>() { refName });
            ChrReference chrReference = genome.GetChrReference(refName);

            nbhd = new VcfNeighborhood( 0, refName, new VariantSite(120), new VariantSite(121));

            Assert.Equal(-1, nbhd.FirstPositionOfInterest);
            Assert.Equal(-1, nbhd.LastPositionOfInterestInVcf);
            Assert.Equal(-1, nbhd.LastPositionOfInterestWithLookAhead);

            readyNbhd = new CallableNeighborhood(nbhd, new VariantCallingParameters(), chrReference);

            Assert.Equal("TG", readyNbhd.NbhdReferenceSequenceSubstring);
            Assert.Equal(120, readyNbhd.FirstPositionOfInterest);
            Assert.Equal(121, readyNbhd.LastPositionOfInterestInVcf);
            Assert.Equal(122, readyNbhd.LastPositionOfInterestWithLookAhead);

        }
    }
}