using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pisces.IO.Sequencing;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using VariantPhasing.Logic;
using VariantPhasing.Types;
using Xunit;
namespace VariantPhasing.Logic.Tests
{
    public class VariantSiteFinderTests
    {
        /*
        [Fact]
        public void FindVariantSitesTest()
        {
            var read = GetBasicRead();
            var finder = new VariantSiteFinder(0);
            var variants = finder.FindVariantSites(read, "chr1");
            Assert.Equal(1, variants[SomaticVariantType.SNP].Count);
            Assert.Equal(4, variants[SomaticVariantType.SNP].First().VcfAlternateAllele.Length);

            read = GetBasicRead("2D4M");
            variants = finder.FindVariantSites(read, "chr1");
            Assert.Equal(1, variants[SomaticVariantType.SNP].Count);
            Assert.Equal(4, variants[SomaticVariantType.SNP].First().VcfAlternateAllele.Length);
            Assert.Equal(1, variants[SomaticVariantType.Deletion].Count);
            Assert.Equal(3, variants[SomaticVariantType.Deletion].First().VcfReferenceAllele.Length);

            read = GetBasicRead("4M2D");
            variants = finder.FindVariantSites(read, "chr1");
            Assert.Equal(1, variants[SomaticVariantType.SNP].Count);
            Assert.Equal(4, variants[SomaticVariantType.SNP].First().VcfAlternateAllele.Length);
            Assert.Equal(1, variants[SomaticVariantType.Deletion].Count);
            Assert.Equal(3, variants[SomaticVariantType.Deletion].First().VcfReferenceAllele.Length);

        }

        private Read GetBasicRead(string cigar = "4M")
        {
            return new Read("chr1", new BamAlignment
            {
                Bases = "ACGT",
                Position = 4,
                CigarData = new CigarAlignment(cigar),
                Qualities = new byte[4]
            });
        }
        */
    }
}
