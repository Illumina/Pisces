using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models.Alleles
{
    public class AlleleTests
    {
        [Fact]
        public void CalledVariant_Tests()
        {
            var calledVariant1 = new CalledVariant(AlleleCategory.Snv);
            calledVariant1.TotalCoverage = 5;
            calledVariant1.ReferenceSupport = 20;

            Assert.Equal(Genotype.HeterozygousAltRef, calledVariant1.Genotype);
            Assert.Equal(1, calledVariant1.RefFrequency);
            Assert.Equal(AlleleCategory.Snv ,calledVariant1.Type);
            
            var calledVariant2 = new CalledVariant(AlleleCategory.Mnv);
            calledVariant2.TotalCoverage = 20;
            calledVariant2.ReferenceSupport = 5;

            Assert.Equal(Genotype.HeterozygousAltRef, calledVariant2.Genotype);
            Assert.Equal(0.25, calledVariant2.RefFrequency);
            Assert.Equal(AlleleCategory.Mnv, calledVariant2.Type);

            var calledVariant3 = new CalledVariant(AlleleCategory.Insertion);

            Assert.Equal(Genotype.HeterozygousAltRef, calledVariant3.Genotype);
            Assert.Equal(0f, calledVariant3.RefFrequency);
            Assert.Equal(AlleleCategory.Insertion, calledVariant3.Type);
        }

        [Fact]
        public void CalledReference_Tests()
        {
            var calledReference = new CalledReference();
            Assert.Equal(Genotype.HomozygousRef, calledReference.Genotype);
            Assert.Equal(AlleleCategory.Reference, calledReference.Type);
        }

        [Fact]
        public void BaseCalledAllele_Tests()
        {
            var baseCalledAllele =  new BaseCalledAllele();
            Assert.Equal(3, baseCalledAllele.TotalCoverageByDirection.Count());
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