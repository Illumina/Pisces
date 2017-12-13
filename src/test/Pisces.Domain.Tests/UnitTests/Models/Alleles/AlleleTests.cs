using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models.Alleles
{
    public class AlleleTests
    {
        [Fact]
        public void BaseCalledAllele_Tests()
        {
            var BaseCalledAllele1 = new CalledAllele(AlleleCategory.Snv);
            BaseCalledAllele1.TotalCoverage = 5;
            BaseCalledAllele1.ReferenceSupport = 20;

            Assert.Equal(Genotype.HeterozygousAltRef, BaseCalledAllele1.Genotype);
            Assert.Equal(1, BaseCalledAllele1.RefFrequency);
            Assert.Equal(AlleleCategory.Snv ,BaseCalledAllele1.Type);
            
            var BaseCalledAllele2 = new CalledAllele(AlleleCategory.Mnv);
            BaseCalledAllele2.TotalCoverage = 20;
            BaseCalledAllele2.ReferenceSupport = 5;

            Assert.Equal(Genotype.HeterozygousAltRef, BaseCalledAllele2.Genotype);
            Assert.Equal(0.25, BaseCalledAllele2.RefFrequency);
            Assert.Equal(AlleleCategory.Mnv, BaseCalledAllele2.Type);

            var BaseCalledAllele3 = new CalledAllele(AlleleCategory.Insertion);

            Assert.Equal(Genotype.HeterozygousAltRef, BaseCalledAllele3.Genotype);
            Assert.Equal(0f, BaseCalledAllele3.RefFrequency);
            Assert.Equal(AlleleCategory.Insertion, BaseCalledAllele3.Type);
        }

        [Fact]
        public void CalledReference_Tests()
        {
            var alleleCall = new CalledAllele();
            Assert.Equal(Genotype.HomozygousRef, alleleCall.Genotype);
            Assert.Equal(AlleleCategory.Reference, alleleCall.Type);
        }

        [Fact]
        public void CalledVariant_Tests()
        {
            var baseCalledAllele =  new CalledAllele();
            Assert.Equal(3, baseCalledAllele.EstimatedCoverageByDirection.Count());
            Assert.Equal(3, baseCalledAllele.SupportByDirection.Count());
            Assert.Equal(0f, baseCalledAllele.Frequency);

            baseCalledAllele.AlleleSupport = 50;
            baseCalledAllele.TotalCoverage = 5;
            Assert.Equal(1, baseCalledAllele.Frequency);

            baseCalledAllele.AlleleSupport = 5;
            baseCalledAllele.TotalCoverage = 50;
            Assert.Equal(0.1f, baseCalledAllele.Frequency);
        }
    }
}