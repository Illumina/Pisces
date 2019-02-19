using System;
using System.Linq;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Models
{
    public class VariantSiteTests
    {

        [Fact]
        public void IsProximal()
        {
            var variantSite1 = new VariantSite(123);
            var variantSite2 = new VariantSite(126);

            // Must be less than phasing distance
            Assert.True(NeighborhoodBuilder.IsProximal(variantSite1, variantSite2, 4));
            Assert.False(NeighborhoodBuilder.IsProximal(variantSite1, variantSite2, 3));

            // Works both ways
            Assert.True(NeighborhoodBuilder.IsProximal(variantSite1, variantSite2, 4));
            Assert.False(NeighborhoodBuilder.IsProximal(variantSite1, variantSite2, 3));
        }

        [Fact]
        public void MergeProfile1Into2()
        {
            var variantSites1 = new[] { new VariantSite(126), new VariantSite(127) };
            var variantSites2 = new[] { new VariantSite(124), new VariantSite(125) };
            var variantSites3 = new[] { new VariantSite(123) };
            VeadGroupMerger.MergeProfile1Into2(variantSites1, variantSites2);
            Assert.Equal(0, variantSites2.Count(v=>v.HasAltData()));
            Assert.Equal(0, variantSites2.Count(v => v.HasRefData()));

            Assert.Throws<System.IO.InvalidDataException>(() => VeadGroupMerger.MergeProfile1Into2(variantSites1, variantSites3));
        }

    }
}
