using System;
using System.Linq;
using System.Collections.Generic;
using Pisces.Calculators;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;
namespace Pisces.Logic.Calculators.Tests
{
    public class GenotypeCalculatorTests
    {

        [Fact]
        [Trait("ReqID", "SDS-50")]
        public void SomaticGenotypeScenarios()
        {
            ExecuteSomaticGenotypeTest(99, 0.5f, false, Genotype.HeterozygousAltRef, new List<FilterType> { FilterType.LowDepth });
            ExecuteSomaticGenotypeTest(99, 0.5f, true, Genotype.HomozygousRef, new List<FilterType> { FilterType.LowDepth });
            ExecuteSomaticGenotypeTest(25, 0.5f, false, Genotype.AltLikeNoCall, new List<FilterType> { FilterType.LowDepth });
            ExecuteSomaticGenotypeTest(25, 0.5f, true, Genotype.RefLikeNoCall, new List<FilterType> { FilterType.LowDepth });

            ExecuteSomaticGenotypeTest(100, 0, true, Genotype.HomozygousRef, new List<FilterType> { }); // shouldnt matter what freqs are
            ExecuteSomaticGenotypeTest(100, 0.009f, false, Genotype.HomozygousAlt, new List<FilterType> { });
            ExecuteSomaticGenotypeTest(100, 0.01f, false, Genotype.HeterozygousAltRef, new List<FilterType> { });

        }

        [Fact]
        [Trait("ReqID", "SDS-??")]
        public void DiploidGenotypeScenarios()
        {
            //https://confluence.illumina.com/display/BIOINFO/Pisces+Germline+Variant+Calling+Requirements


            //req 1.1 0/0 situation
            ExecuteDiploidGenotypeTest(Genotype.HomozygousRef, 1, new List<float> { 0.80f }, new List<float> { 0.19f });  // '0/0'
            ExecuteDiploidGenotypeTest(Genotype.HomozygousRef, 0, new List<float> { 0.80f }, new List<float> { });  // '0/0'

            //req 1.2 0/1 situation
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { 0.80f }, new List<float> { 0.20f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { 0.70f }, new List<float> { 0.30f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { 0.21f }, new List<float> { 0.69f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 1, new List<float> { 0.69f }, new List<float> { 0.30f, 0.01f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { }, new List<float> { 0.20f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { }, new List<float> { 0.30f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 1, new List<float> { }, new List<float> { 0.30f, 0.01f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 2, new List<float> { }, new List<float> { 0.01f, 0.02f, 0.30f }); // '0/1'

            //req 1.2 1/. situation
            ExecuteDiploidGenotypeTest(Genotype.AltAndNoCall, 0, new List<float> { 0.10f }, new List<float> { 0.70f }); // '0/1'


            //req 1.3 1/1 situation
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { 0.10f }, new List<float> { 0.71f }); // '1/1'
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { 0.10f }, new List<float> { 0.99f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { 0.10f }, new List<float> { 1.0f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { }, new List<float> { 0.71f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { }, new List<float> { 0.99f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { }, new List<float> { 1.0f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 1, new List<float> { 0.10f }, new List<float> { 0.99f, 0.01f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 1, new List<float> { }, new List<float> { 0.99f, 0.01f });

            //req 2.2 ./.  Multi allelic situation -> ./.
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.20f }, new List<float> { 0.40f, 0.40f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f, 0.02f });
            ExecuteDiploidGenotypeTest(Genotype.Alt12LikeNoCall, 0, new List<float> { 0.01f }, new List<float> { 0.40f, 0.39f });
            ExecuteDiploidGenotypeTest(Genotype.Alt12LikeNoCall, 0, new List<float> { 0.0f }, new List<float> { 0.20f, 0.40f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { }, new List<float> { 0.20f, 0.40f, 0.02f });


            //req 2.3 ./. alt-like Multi allelic situation-> ./.
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f, 0.20f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.30f }, new List<float> { 0.20f, 0.30f, 0.30f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.80f }, new List<float> { 0.20f, 0.20f });


            //req 2.3 ./. alt-like Multi allelic situation-> ./.
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f, 0.20f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.30f }, new List<float> { 0.20f, 0.30f, 0.30f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.80f }, new List<float> { 0.20f, 0.20f });

            //req 2.4.a ./. alt-like Multi allelic situation -> 0/1
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 1, new List<float> { 0.60f }, new List<float> { 0.40f, 0.01f });

            //req 2.4.b ./. alt-like Multi allelic situation -> 1/2
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAlt1Alt2, 0, new List<float> { }, new List<float> { 0.50f, 0.50f });
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAlt1Alt2, 0, new List<float> { 0.01f }, new List<float> { 0.40f, 0.40f });
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAlt1Alt2, 1, new List<float> { 0.01f }, new List<float> { 0.35f, 0.55f, 0.01f });

            //req 2.5 depth less than required depth to call -> no call
            ExecuteDiploidGenotypeTest(Genotype.RefAndNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 1000);
            ExecuteDiploidGenotypeTest(Genotype.AltAndNoCall, 1, new List<float> { 0.10f }, new List<float> { 0.21f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 1000);
            ExecuteDiploidGenotypeTest(Genotype.RefLikeNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 10);
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.10f }, new List<float> { 0.21f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 10);
        }


        private void ExecuteDiploidGenotypeTest(
            Genotype expectedGenotype, int expectedNumAllelesToPrune,
           List<float> refFrequencies, List<float> altFrequencies)
        {
            ExecuteDiploidGenotypeTest(expectedGenotype, expectedNumAllelesToPrune, refFrequencies, altFrequencies, new List<FilterType> { },1000);
        }

        private void ExecuteDiploidGenotypeTest(
            Genotype expectedGenotype, int expectedNumAllelesToPrune,
            List<float> refFrequencies, List<float> altFrequencies, List<FilterType> filters, int coverage)
        {
            var alleles = new List<CalledAllele>();
            double refFreq = 0;
            foreach (float rf in refFrequencies)
            {
                var variant = TestHelper.CreatePassingVariant(true);
                variant.AlleleSupport = (int)(rf * coverage);
                variant.TotalCoverage = (int)coverage;
                variant.ReferenceSupport = variant.AlleleSupport;
                alleles.Add(variant);
                refFreq = rf;
            }

            if (refFreq == 0)
                refFreq = (1.0 - altFrequencies.Sum());

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

            var GTC = new DiploidGenotypeCalculator();
            GTC.MinDepthToGenotype = 100;
            var allelesToPrune = GTC.SetGenotypes(alleles);

            Assert.Equal(expectedNumAllelesToPrune, allelesToPrune.Count);
            foreach (var allele in alleles)
            {
                Assert.Equal(expectedGenotype, allele.Genotype);
            }
        }

        private void ExecuteSomaticGenotypeTest(int totalCoverage, float refFrequency, bool isReference,
             Genotype expectedGenotype, List<FilterType> filters)
        {
            var variant = TestHelper.CreatePassingVariant(isReference);
            variant.Filters = filters;

            variant.TotalCoverage = totalCoverage;
            if (!isReference)
            {
                var refSupport = (int)(refFrequency * totalCoverage);
                variant.AlleleSupport = totalCoverage - refSupport;
                variant.ReferenceSupport = refSupport;
            }

            var GTC = new SomaticGenotypeCalculator();
            GTC.MinDepthToGenotype = 30;
            var allelesToPrune = GTC.SetGenotypes(new List<CalledAllele> { variant });

            Assert.Equal(0, allelesToPrune.Count);
            Assert.Equal(expectedGenotype, variant.Genotype);
        }

    }
}