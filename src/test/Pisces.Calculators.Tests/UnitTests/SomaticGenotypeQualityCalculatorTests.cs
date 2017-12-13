using System;
using System.Collections.Generic;
using Pisces.Calculators;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;

namespace Pisces.Calculators.Tests
{
    public class SomaticGenotypeQualityCalculatorTests
    {
        /// <summary>
        /// All 'truth data' here comes from excel.
        /// </summary>
        [Fact]
        [Trait("ReqID", "PSG-1")]
        public void SomaticComputeGenotypeQualityTests()
        {
            var variant = TestHelper.CreatePassingVariant(true);
            variant.VariantQscore = 1000;
            double depth = 1000;

            //1) test hom-ref calls for a range of frequencies.
            double[] testFrequencies = new double[] { 0, 0.01, 0.05, 0.10, 0.15, 0.19 };
            int[] expectedResults = new int[] { 217, 119, 0, 0, 0, 0 };
            variant.Genotype = Genotype.HomozygousRef;
            TestCalculationsFor5PercentLOD(variant, depth, testFrequencies, expectedResults);

            //check when the LOD changes from 5% to 10%, we gain confidence 
            //that we have not missed anything under our LOD.
            expectedResults = new int[] { 434, 309, 76, 0, 0, 0 };
            TestCalculationsFor10PercentLOD(variant, depth, testFrequencies, expectedResults);


            //2) test het-alt-ref calls for a range of frequencies.
            testFrequencies = new double[] { 0.2, 0.4};
            expectedResults = new int[] { variant.VariantQscore, variant.VariantQscore };
            variant.Genotype = Genotype.HeterozygousAltRef;
            TestCalculationsFor5PercentLOD(variant, depth, testFrequencies, expectedResults);

            //check when the LOD changes from 5% to 10%, 
            //this does not affect our 0/1 calls
            expectedResults = new int[] { variant.VariantQscore, variant.VariantQscore };
            TestCalculationsFor10PercentLOD(variant, depth, testFrequencies, expectedResults);

            //3) test Homozygous alt
            testFrequencies = new double[] { 1.0, 0.99, 0.95, 0.90, 0.85, 0.19 };
            expectedResults = new int[] { 217, 119, 0, 0, 0, 0 };
            variant.Genotype = Genotype.HomozygousAlt;
            TestCalculationsFor5PercentLOD(variant, depth, testFrequencies, expectedResults);

            //check when the LOD changes from 5% to 10%, we gain confidence 
            //that we have not missed anything under our LOD.
            expectedResults = new int[] { 434, 309, 76, 0, 0, 0 };
            TestCalculationsFor10PercentLOD(variant, depth, testFrequencies, expectedResults);



            //4) test alt /NO CALLS (1/.)
            testFrequencies = new double[] { 0.2, 0.4 };
            expectedResults = new int[] { variant.VariantQscore, variant.VariantQscore };
            variant.Genotype = Genotype.AltAndNoCall;
            TestCalculationsFor5PercentLOD(variant, depth, testFrequencies, expectedResults);

            //5) test ref/no calls calls for a range of frequencies.
            testFrequencies = new double[] { 0, 0.01, 0.05, 0.10 };
            expectedResults = new int[] { variant.VariantQscore, variant.VariantQscore, variant.VariantQscore, variant.VariantQscore };
            variant.Genotype = Genotype.RefAndNoCall;
            TestCalculationsFor5PercentLOD(variant, depth, testFrequencies, expectedResults);

            //check when the LOD changes from 5% to 10%, we dont care.
            expectedResults = new int[] { variant.VariantQscore, variant.VariantQscore, variant.VariantQscore, variant.VariantQscore };
            TestCalculationsFor10PercentLOD(variant, depth, testFrequencies, expectedResults);


            //6) test various NO CALLS (./.)
            testFrequencies = new double[] { 0.2, 0.4 };
            expectedResults = new int[] {0, 0};
            variant.Genotype = Genotype.AltLikeNoCall;
            TestCalculationsFor5PercentLOD(variant, depth, testFrequencies, expectedResults);
            variant.Genotype = Genotype.RefLikeNoCall;
            TestCalculationsFor5PercentLOD(variant, depth, testFrequencies, expectedResults);
            variant.Genotype = Genotype.Alt12LikeNoCall;//defensive. this case would never get hit in somaitc mode.
            TestCalculationsFor5PercentLOD(variant, depth, testFrequencies, expectedResults);
        }

        private static void TestCalculationsFor5PercentLOD(CalledAllele variant, double depth, double[] testFrequencies, int[] expectedResults)
        {
            for (int i = 0; i < testFrequencies.Length; i++)
                TestCalculation(variant, testFrequencies[i], 0.05F, depth, expectedResults[i]);
        }

        private static void TestCalculationsFor10PercentLOD(CalledAllele variant, double depth, double[] testFrequencies, int[] expectedResults)
        {
            for (int i = 0; i < testFrequencies.Length; i++)
                TestCalculation(variant, testFrequencies[i], 0.10F, depth, expectedResults[i]);
        }

        private static void TestCalculation(CalledAllele variant, double alleleFrequency, float targetLODFreq,
            double depth, int expectedValue)
        {
            variant.TotalCoverage = (int)depth;
            variant.AlleleSupport = (int)(depth * alleleFrequency);
            variant.ReferenceSupport = (int)(depth * (1.0 - alleleFrequency));

            if ((variant.Genotype == Genotype.HomozygousRef) || (variant.Genotype == Genotype.RefAndNoCall))
            {
                variant.Type = AlleleCategory.Reference;
                variant.AlleleSupport = (int)(depth * (1.0 - alleleFrequency));
                variant.ReferenceSupport = variant.AlleleSupport;
            }
            int result = SomaticGenotypeQualityCalculator.Compute(variant, targetLODFreq, 0, int.MaxValue);
            Assert.Equal(expectedValue, result);
        }
    }
}
