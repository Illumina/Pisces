using Pisces.Domain.Types;
using Pisces.Genotyping;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace AdaptiveGenotyper.Tests
{
    public class MixtureModelUtilsTests
    {
        [Fact]
        public void GenotypeCategoryTests()
        {
            var mm = new MixtureModelResult { GenotypeCategory = SimplifiedDiploidGenotype.HeterozygousAltRef };
            Assert.Equal(1, mm.GenotypeCategoryAsInt);

            mm.GenotypeCategory = SimplifiedDiploidGenotype.HomozygousAlt;
            Assert.Equal(2, mm.GenotypeCategoryAsInt);

            mm.GenotypeCategoryAsInt = 0;
            Assert.Equal(SimplifiedDiploidGenotype.HomozygousRef, mm.GenotypeCategory);

            Assert.Throws<ArgumentException>(() => mm.GenotypeCategoryAsInt = 3);
        }
    }
}
