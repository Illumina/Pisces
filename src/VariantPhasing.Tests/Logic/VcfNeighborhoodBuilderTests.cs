using System.Collections.Generic;
using System.Linq;
using System.IO;
using Moq;
using Pisces.IO.Sequencing;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using TestUtilities;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class VcfNeighborhoodBuilderTests
    {

        [Fact]
        public void GetNeighborhoodsFromMessyVCF()
        {
            var vcfFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "verymutated.genome.vcf");
            var outFolder = Path.Combine(UnitTestPaths.TestDataDirectory, "Out");

            var neighborhoodManager = CreateBuilder(new List<VcfVariant>() { });
            Assert.Equal(0, neighborhoodManager.GetNeighborhoods().Count());

            List<VcfVariant> LotsOfCoLocatedVariants = VcfReader.GetAllVariantsInFile(vcfFilePath);
            neighborhoodManager = CreateBuilder(LotsOfCoLocatedVariants);

            var neighborhoods = neighborhoodManager.GetNeighborhoods();
            Assert.Equal(1, neighborhoods.Count());
            Assert.Equal(12, neighborhoods.First().VcfVariantSites.Count());

        }
        [Fact]
        public void GetNeighborhoods()
        {
            var variant = CreateVariant(123);
            var secondVariant = CreateVariant(124);
            var thirdVariant = CreateVariant(125);
            var fourthVariant = CreateVariant(128);
            var fifthVariant = CreateVariant(129);
            var thirdVariantFailing = CreateVariant(125);
            thirdVariantFailing.Filters = "LowQ";

            // -------------------------------------------------
            // Add two variants that should be chained together
            // - When there are no neighborhoods
            // - When there is a neighborhood that they can join
            // - When there are neighborhoods available but they cannot join
            // -------------------------------------------------
            var neighborhoodManager = CreateBuilder(new List<VcfVariant>());
            Assert.Equal(0, neighborhoodManager.GetNeighborhoods().Count());

            neighborhoodManager = CreateBuilder(new List<VcfVariant>
            {
                variant,
                secondVariant
            });

            var neighborhoods = neighborhoodManager.GetNeighborhoods();
            Assert.Equal(1, neighborhoods.Count());
            Assert.True(neighborhoods.First().HasVariants);
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { variant, secondVariant }, neighborhoods.First());

            neighborhoodManager = CreateBuilder(new List<VcfVariant>() { variant, secondVariant, thirdVariant });
            neighborhoods = neighborhoodManager.GetNeighborhoods();

            Assert.Equal(1, neighborhoods.Count());
            Assert.True(neighborhoods.First().HasVariants);
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { variant, secondVariant, thirdVariant }, neighborhoods.First());

            neighborhoodManager = CreateBuilder(new List<VcfVariant>() { variant, secondVariant, thirdVariant, fourthVariant, fifthVariant });
            neighborhoods = neighborhoodManager.GetNeighborhoods();
            Assert.Equal(2, neighborhoods.Count());
            var neighborhood1 = neighborhoods.Last();
            Assert.True(neighborhood1.HasVariants);
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { variant, secondVariant, thirdVariant }, neighborhoods.First());
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { fourthVariant, fifthVariant }, neighborhood1);

            // Different phasing distance
            neighborhoodManager = CreateBuilder(new List<VcfVariant> { variant, secondVariant, fourthVariant, fifthVariant }, 5);
            neighborhoods = neighborhoodManager.GetNeighborhoods();
            Assert.Equal(1, neighborhoods.Count());
            Assert.True(neighborhoods.First().HasVariants);
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { variant, secondVariant, fourthVariant, fifthVariant }, neighborhoods.First());

            // -------------------------------------------------
            // Passing/Failing Variants Allowed
            // -------------------------------------------------

            // Passing Only
            neighborhoodManager = CreateBuilder(new List<VcfVariant> { variant, secondVariant, thirdVariantFailing, fourthVariant, fifthVariant });
            neighborhoods = neighborhoodManager.GetNeighborhoods();
            Assert.Equal(2, neighborhoods.Count());
            Assert.True(neighborhoods.First().HasVariants);
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { variant, secondVariant }, neighborhoods.First());
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { fourthVariant, fifthVariant }, neighborhoods.Last());

            // Passing Only, Larger phasing distance
            neighborhoodManager = CreateBuilder(new List<VcfVariant> { variant, secondVariant, thirdVariantFailing, fourthVariant, fifthVariant }, 5);
            neighborhoods = neighborhoodManager.GetNeighborhoods();
            Assert.Equal(1, neighborhoods.Count());
            Assert.True(neighborhoods.First().HasVariants);
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { variant, secondVariant, fourthVariant, fifthVariant }, neighborhoods.First());

            // Passing Only = false
            neighborhoodManager = CreateBuilder(new List<VcfVariant> { variant, secondVariant, thirdVariantFailing, fourthVariant, fifthVariant }, passingOnly: false);
            neighborhoods = neighborhoodManager.GetNeighborhoods();
            Assert.Equal(2, neighborhoods.Count());
            Assert.True(neighborhoods.First().HasVariants);
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { variant, secondVariant, thirdVariantFailing }, neighborhoods.First());
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { fourthVariant, fifthVariant }, neighborhoods.Last());

            // Passing Only = false, Larger phasing distance
            neighborhoodManager = CreateBuilder(new List<VcfVariant> { variant, secondVariant, thirdVariantFailing, fourthVariant, fifthVariant }, 5, false);
            neighborhoods = neighborhoodManager.GetNeighborhoods();
            Assert.Equal(1, neighborhoods.Count());
            Assert.True(neighborhoods.First().HasVariants);
            NeighborhoodTestHelpers.CheckNeighborhoodVariants(new List<VcfVariant>() { variant, secondVariant, thirdVariantFailing, fourthVariant, fifthVariant }, neighborhoods.First());

        }

        private VcfNeighborhoodBuilder CreateBuilder(List<VcfVariant> stagedVariants, int phasingDistance = 2, bool passingOnly = true)
        {
            var variantSource = new Mock<IVcfVariantSource>();
            variantSource.Setup(s => s.GetVariants()).Returns(stagedVariants);

            return new VcfNeighborhoodBuilder(
                new PhasableVariantCriteria() {ChrToProcessArray= new string[] { },PassingVariantsOnly = passingOnly, PhasingDistance=phasingDistance}, 
                new VariantCallingParameters(), variantSource.Object);
        }

        private VcfVariant CreateVariant(int position)
        {
            return new VcfVariant()
            {
                ReferenceName = "chr1",
                ReferencePosition = position,
                ReferenceAllele = "A",
                VariantAlleles = new[] { "T" },
                Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() { { "GT", "0/1" } } },
                InfoFields = new Dictionary<string, string>() { {"DP","1000" } },
                Filters = "PASS"
            };
        }
    }

    public static class NeighborhoodTestHelpers
    {
        public static void CheckNeighborhoodVariants(List<VcfVariant> expectedVariants, VcfNeighborhood neighborhood)
        {
            var variants = expectedVariants.Select(expectedVariant =>
                new VariantSite()
                {
                    VcfReferencePosition = expectedVariant.ReferencePosition,
                    ReferenceName = expectedVariant.ReferenceName,
                    VcfReferenceAllele = expectedVariant.ReferenceAllele,
                    VcfAlternateAllele = expectedVariant.VariantAlleles.First()
                }).ToList();
            CheckNeighborhoodVariants(variants, neighborhood);
        }

        public static void CheckNeighborhoodVariants(List<VariantSite> expectedVariantSites, VcfNeighborhood neighborhood)
        {
            Assert.Equal(expectedVariantSites.Count, neighborhood.VcfVariantSites.Count);
            foreach (var expectedVariantSite in expectedVariantSites)
            {
                Assert.True(neighborhood.VcfVariantSites.Any(v => v.ReferenceName == expectedVariantSite.ReferenceName && v.VcfReferencePosition == expectedVariantSite.VcfReferencePosition &&
                    v.VcfReferenceAllele == expectedVariantSite.VcfReferenceAllele && v.VcfAlternateAllele == expectedVariantSite.VcfAlternateAllele));
            }
        }
    }

}
