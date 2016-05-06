using System;
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
            ExecuteSomaticGenotypeTest(99, 0.5f, false, GenotypeModel.Thresholding, Genotype.AltLikeNoCall, new List<FilterType> {FilterType.LowDepth});
            ExecuteSomaticGenotypeTest(99, 0.5f, true, GenotypeModel.Thresholding, Genotype.RefLikeNoCall, new List<FilterType> {FilterType.LowDepth});

            ExecuteSomaticGenotypeTest(100, 0, true, GenotypeModel.Thresholding, Genotype.HomozygousRef, new List<FilterType> {}); // shouldnt matter what freqs are

            ExecuteSomaticGenotypeTest(100, 0.009f, false, GenotypeModel.Thresholding, Genotype.HomozygousAlt, new List<FilterType> { });
            ExecuteSomaticGenotypeTest(100, 0.24f, false, GenotypeModel.None, Genotype.HeterozygousAltRef, new List<FilterType> { });

            ExecuteSomaticGenotypeTest(100, 0.01f, false, GenotypeModel.Thresholding,  Genotype.HeterozygousAltRef, new List<FilterType> { });
            ExecuteSomaticGenotypeTest(100, 0.25f, false, GenotypeModel.None, Genotype.HeterozygousAltRef, new List<FilterType> { });
            ExecuteSomaticGenotypeTest(100, 0, false, GenotypeModel.None, Genotype.HeterozygousAltRef, new List<FilterType> { });
        
        }

        [Fact]
        [Trait("ReqID", "SDS-??")]
        public void DiploidGenotypeScenarios()
        {
            //https://confluence.illumina.com/display/BIOINFO/Pisces+Germline+Variant+Calling+Requirements
            

            //req 1.1 0/0 situation
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousRef, 1, new List<float> { 0.80f }, new List<float> { 0.19f });  // '0/0'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousRef, 0, new List<float> { 0.80f }, new List<float> { });  // '0/0'

            //req 1.2 0/1 situation
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 0, new List<float> { 0.80f }, new List<float> { 0.20f }); // '0/1'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 0, new List<float> { 0.70f }, new List<float> { 0.30f }); // '0/1'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 0, new List<float> { 0.21f }, new List<float> { 0.69f }); // '0/1'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 0, new List<float> { 0.10f }, new List<float> { 0.70f }); // '0/1'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 1, new List<float> { 0.69f }, new List<float> { 0.30f, 0.01f }); // '0/1'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 0, new List<float> { }, new List<float> { 0.20f }); // '0/1'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 0, new List<float> { }, new List<float> { 0.30f }); // '0/1'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 1, new List<float> { }, new List<float> { 0.30f, 0.01f }); // '0/1'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 2, new List<float> { }, new List<float> { 0.01f, 0.02f, 0.30f }); // '0/1'

            //req 1.3 1/1 situation
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousAlt, 0, new List<float> { 0.10f }, new List<float> { 0.71f }); // '1/1'
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousAlt, 0, new List<float> { 0.10f }, new List<float> { 0.99f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousAlt, 0, new List<float> { 0.10f }, new List<float> { 1.0f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousAlt, 0, new List<float> { }, new List<float> { 0.71f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousAlt, 0, new List<float> { }, new List<float> { 0.99f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousAlt, 0, new List<float> { }, new List<float> { 1.0f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousAlt, 1, new List<float> { 0.10f }, new List<float> { 0.99f, 0.01f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HomozygousAlt, 1, new List<float> { }, new List<float> { 0.99f, 0.01f });

            //req 2.2 ./.  Multi allelic situation -> ./.
            
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.AltLikeNoCall, 0, new List<float> { 0.20f }, new List<float> { 0.40f, 0.40f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.AltLikeNoCall, 0, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.AltLikeNoCall, 1, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f, 0.02f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.Alt12LikeNoCall, 0, new List<float> { 0.01f }, new List<float> { 0.40f, 0.39f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.Alt12LikeNoCall, 0, new List<float> { 0.0f }, new List<float> { 0.20f, 0.40f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.Alt12LikeNoCall, 1, new List<float> { }, new List<float> { 0.20f, 0.40f, 0.02f });

            
            //req 2.3 ./. alt-like Multi allelic situation-> ./.
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.AltLikeNoCall, 0, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f, 0.20f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.AltLikeNoCall, 0, new List<float> { 0.30f }, new List<float> { 0.20f, 0.30f, 0.30f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.AltLikeNoCall, 0, new List<float> { 0.80f }, new List<float> { 0.20f, 0.20f });

            
            //req 2.3 ./. alt-like Multi allelic situation-> ./.
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.Alt12LikeNoCall, 0, new List<float> { 0.01f }, new List<float> { 0.20f, 0.40f, 0.20f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.Alt12LikeNoCall, 1, new List<float> { }, new List<float> { 0.10f, 0.30f, 0.30f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.Alt12LikeNoCall, 0, new List<float> { 0.10f }, new List<float> { 0.20f, 0.20f });
            
            //req 2.4.a ./. alt-like Multi allelic situation -> 0/1
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAltRef, 1, new List<float> { 0.60f}, new List<float> { 0.40f, 0.01f} );

            //req 2.4.b ./. alt-like Multi allelic situation -> 1/2
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAlt1Alt2, 0, new List<float> {}, new List<float> { 0.50f, 0.50f} );
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAlt1Alt2, 0, new List<float> { 0.01f }, new List<float> { 0.40f, 0.40f });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.HeterozygousAlt1Alt2, 1, new List<float> { 0.01f }, new List<float> { 0.35f, 0.55f, 0.01f });

            //req 2.5 filter by depth. -> no call
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.RefLikeNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth });
            ExecuteDiploidGenotypeTest(GenotypeModel.Thresholding, Genotype.AltLikeNoCall, 1, new List<float> { 0.10f }, new List<float> { 0.21f, 0.01f }, new List<FilterType> { FilterType.LowDepth });
        }


        private void ExecuteDiploidGenotypeTest(
           GenotypeModel genotypeModel, Genotype expectedGenotype, int expectedNumAllelesToPrune,
           List<float> refFrequencies, List<float> altFrequencies)
        {
            ExecuteDiploidGenotypeTest(genotypeModel, expectedGenotype, expectedNumAllelesToPrune, refFrequencies, altFrequencies, new List<FilterType> { });
        }

        private void ExecuteDiploidGenotypeTest(
            GenotypeModel genotypeModel, Genotype expectedGenotype, int expectedNumAllelesToPrune,
            List<float> refFrequencies, List<float> altFrequencies, List<FilterType> filters)
        {
            float coverage = 100;
            var alleles = new List<BaseCalledAllele>();

            foreach (float rf in refFrequencies)
            {
                var variant = TestHelper.CreatePassingVariant(true);
                variant.AlleleSupport = (int) (rf*coverage);
                variant.TotalCoverage = (int) coverage;
                alleles.Add(variant);
            }

            foreach (float vf in altFrequencies)
            {
                var variant = TestHelper.CreatePassingVariant(false);
                variant.AlleleSupport = (int) (vf * coverage);
                variant.TotalCoverage = (int)coverage;
                alleles.Add(variant);
            }

            //set filters for at least one allele. they should affect all results.
            alleles[0].Filters = filters;

            var GTC = new GenotypeCalculator(genotypeModel, PloidyModel.Diploid, 0.01f, new GenotypeCalculator.DiploidThresholdingParameters());
            var allelesToPrune = GTC.SetGenotypes(alleles);

            Assert.Equal(expectedNumAllelesToPrune, allelesToPrune.Count);
            foreach (var allele in alleles)
            {
                Assert.Equal(expectedGenotype, allele.Genotype);
            }
        }

        private void ExecuteSomaticGenotypeTest(int totalCoverage, float refFrequency, bool isReference, 
            GenotypeModel genotypeModel, Genotype expectedGenotype, List<FilterType> filters)
        {
            var ploidyModel = PloidyModel.Somatic;
            var variant = TestHelper.CreatePassingVariant(isReference);
            variant.Filters = filters;

            variant.TotalCoverage = totalCoverage;
            if (!isReference)
            {
                var refSupport = (int)(refFrequency * totalCoverage);
                variant.AlleleSupport = totalCoverage - refSupport;
                ((CalledVariant)variant).ReferenceSupport = refSupport;
            }

            var GTC = new GenotypeCalculator(genotypeModel, ploidyModel, 0.01f, new GenotypeCalculator.DiploidThresholdingParameters());
            var allelesToPrune = GTC.SetGenotypes(new List<BaseCalledAllele> {variant});

            Assert.Equal(0, allelesToPrune.Count);
            Assert.Equal(expectedGenotype, variant.Genotype);
        }

    }
}