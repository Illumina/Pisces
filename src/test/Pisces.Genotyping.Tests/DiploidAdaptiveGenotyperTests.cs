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

        private AdaptiveGenotypingParameters _adaptiveGenotypingParameters = new AdaptiveGenotypingParameters();
        private int _minCalledVariantDepth = 100;
        private int _minGQscore = 0;
        private int _maxGQscore = 100;

        private void ExecuteAdaptiveGenotypeerTest(
            Genotype expectedGenotype, int expectedNumAllelesToPrune,
            float? refFrequency, List<float> altFrequencies, List<FilterType> filters, int coverage)
        {
            var alleles = new List<CalledAllele>();


            if (refFrequency != null)
            {
                var variant = TestHelper.CreatePassingVariant(true);
                variant.AlleleSupport = (int)(refFrequency * coverage);
                variant.TotalCoverage = (int)coverage;
                variant.ReferenceSupport = variant.AlleleSupport;
                alleles.Add(variant);
            }


            var refFreq = refFrequency ?? 1.0 - altFrequencies.Sum();


            foreach (float vf in altFrequencies)
            {
                var variant = TestHelper.CreatePassingVariant(false);
                variant.AlleleSupport = (int)(vf * coverage);
                variant.TotalCoverage = (int)coverage;
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

     
    }


}