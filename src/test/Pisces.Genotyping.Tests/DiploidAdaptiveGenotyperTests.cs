using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Options;
using Pisces.Genotyping;
using TestUtilities;
using Xunit;

namespace Pisces.Genotyping.Tests
{
    public class DiploidAdaptiveGenotyperTests
    {

        private readonly AdaptiveGenotypingParameters _adaptiveGenotypingParameters = new AdaptiveGenotypingParameters();
        private readonly int _minCalledVariantDepth = 100;
        private readonly int _minGQscore = 0;
        private readonly int _maxGQscore = 100;

        private void ExecuteAdaptiveGenotyperTest(
            Genotype expectedGenotype, int expectedNumAllelesToPrune,
            float? refFrequency, List<float> altFrequencies, List<FilterType> filters, int coverage)
        {
            var alleles = new List<CalledAllele>();


            if (refFrequency != null)
            {
                var variant = TestHelper.CreatePassingVariant(true);
                variant.AlleleSupport = (int)(refFrequency * coverage);
                variant.TotalCoverage = coverage;
                variant.ReferenceSupport = variant.AlleleSupport;
                alleles.Add(variant);
            }


            var refFreq = refFrequency ?? 1.0 - altFrequencies.Sum();


            foreach (float vf in altFrequencies)
            {
                var variant = TestHelper.CreatePassingVariant(false);
                variant.AlleleSupport = (int)(vf * coverage);
                variant.TotalCoverage = coverage;
                variant.ReferenceSupport = (int)(refFreq * coverage);
                alleles.Add(variant);
            }

            //set filters for at least one allele. they should affect all results.
            alleles[0].Filters = filters;

            var GTC = new DiploidAdaptiveGenotyper(_minCalledVariantDepth, _minGQscore, _maxGQscore, _adaptiveGenotypingParameters);

            var allelesToPrune = GTC.SetGenotypes(alleles);

            Assert.Equal(expectedNumAllelesToPrune, allelesToPrune.Count);
            foreach (var allele in alleles)
            {
                Assert.Equal(expectedGenotype, allele.Genotype);
            }
        }


        [Fact]
        public void ExpectRefTests()
        {
            ExecuteAdaptiveGenotyperTest(Genotype.HomozygousRef, 2, 0.95f, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 1000);
        }

        [Fact]
        public void ExpectHomozygousAltTests()
        {
            ExecuteAdaptiveGenotyperTest(Genotype.HomozygousAlt, 1, 0.02f, new List<float> { 0.95f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 10000);
        }

        [Fact]
        public void ExpectHeterozygousAltTests()
        {
            ExecuteAdaptiveGenotyperTest(Genotype.HeterozygousAltRef, 1, 0.34f, new List<float> { 0.60f, 0.06f }, new List<FilterType> { FilterType.LowDepth }, 1000);
        }

        [Fact]
        public void ExpectRefAndNoCallTests()
        {
            ExecuteAdaptiveGenotyperTest(Genotype.RefAndNoCall, 2, 0.80f, new List<float> { 0.14f, 0.06f }, new List<FilterType> { FilterType.LowDepth }, 100);
        }

        [Fact]
        public void ExpectRefLikeNoCallTests()
        {
            ExecuteAdaptiveGenotyperTest(Genotype.RefLikeNoCall, 2, 0.80f, new List<float> { 0.14f, 0.06f }, new List<FilterType> { FilterType.LowDepth }, 10);
        }

        [Fact]
        public void ExpectAltAndNoCallTests()
        {
            ExecuteAdaptiveGenotyperTest(Genotype.AltAndNoCall, 1, 0.03f, new List<float> { 0.6f, 0.06f }, new List<FilterType> { FilterType.LowDepth }, 100);
        }

        [Fact]
        public void ExpectHeterozygousAlt1Alt2Tests()
        {
            ExecuteAdaptiveGenotyperTest(Genotype.HeterozygousAlt1Alt2, 0, 0.06f, new List<float> { 0.44f, 0.50f }, new List<FilterType> { FilterType.LowDepth }, 1000);
        }

        [Fact]
        public void NoCallDueToCoverge()
        {
            ExecuteAdaptiveGenotyperTest(Genotype.RefLikeNoCall, 2, 0.80f, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 10);
        }        
    }
}