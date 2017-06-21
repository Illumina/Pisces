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
        [Trait("ReqID", "??-??")]
        public void SomaticComputeGenotypeQualityTests()
        {
            var variant = TestHelper.CreatePassingVariant(true);
            variant.VariantQscore = 1000;
            double depth = 1000;

            //1) test hom-ref calls for a range of frequencies.
            double[] testFrequencies = new double[] { 0, 0.01, 0.05, 0.10, 0.15, 0.19 };
            int[] expectedResults = new int[] { 217, 119, 0, 0, 0, 0 };
            variant.Genotype = Genotype.HomozygousRef;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //2) test het-alt-ref calls for a range of frequencies.
            testFrequencies = new double[] { 0.2, 0.4};
            expectedResults = new int[] { variant.VariantQscore, variant.VariantQscore };
            variant.Genotype = Genotype.HeterozygousAltRef;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //3) test Homozygous alt
            testFrequencies = new double[] { 1.0, 0.99, 0.95, 0.90, 0.85, 0.19 };
            expectedResults = new int[] { 217, 119, 0, 0, 0, 0 };
            variant.Genotype = Genotype.HomozygousAlt;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //4) test alt /NO CALLS (1/.)
            testFrequencies = new double[] { 0.2, 0.4 };
            expectedResults = new int[] { variant.VariantQscore, variant.VariantQscore };
            variant.Genotype = Genotype.AltAndNoCall;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //5) test ref/no calls calls for a range of frequencies.
            testFrequencies = new double[] { 0, 0.01, 0.05, 0.10 };
            expectedResults = new int[] { variant.VariantQscore, variant.VariantQscore, variant.VariantQscore, variant.VariantQscore };
            variant.Genotype = Genotype.RefAndNoCall;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //6) test various NO CALLS (./.)
            testFrequencies = new double[] { 0.2, 0.4 };
            expectedResults = new int[] {0, 0};
            variant.Genotype = Genotype.AltLikeNoCall;
            TestCalculations(variant, depth, testFrequencies, expectedResults);
            variant.Genotype = Genotype.RefLikeNoCall;
            TestCalculations(variant, depth, testFrequencies, expectedResults);
            variant.Genotype = Genotype.Alt12LikeNoCall;//defensive. this case would never get hit in somaitc mode.
            TestCalculations(variant, depth, testFrequencies, expectedResults);
        }

        private static void TestCalculations(CalledAllele variant, double depth, double[] testFrequencies, int[] expectedResults)
        {
            for (int i = 0; i < testFrequencies.Length; i++)
                TestCalculation(variant, testFrequencies[i], depth, expectedResults[i]);
        }

        private static void TestCalculation(CalledAllele variant, double frequency, double depth, int expectedValue)
        {
            variant.TotalCoverage = (int)depth;
            variant.AlleleSupport = (int)(depth * frequency);
            variant.ReferenceSupport = (int)(depth * (1.0 - frequency));

            if ((variant.Genotype == Genotype.HomozygousRef) || (variant.Genotype == Genotype.RefAndNoCall))
            {
                variant.Type = AlleleCategory.Reference;
                variant.AlleleSupport = (int)(depth * (1.0 - frequency));
                variant.ReferenceSupport = variant.AlleleSupport;
            }
            int result = SomaticGenotypeQualityCalculator.Compute(variant, 0.05F, 0, int.MaxValue);
            Assert.Equal(expectedValue, result);
        }
    }
}
