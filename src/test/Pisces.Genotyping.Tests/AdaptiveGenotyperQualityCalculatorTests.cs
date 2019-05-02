using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestUtilities;
using Xunit;

namespace Pisces.Genotyping.Tests
{
    public class AdaptiveGenotyperCalculatorTests
    {
        private readonly double[] Means = new double[] { 0.015, 0.5, 0.99 };
        private readonly double[] Priors = new double[] { 0.99, 0.005, 0.005 };

        /// <summary>
        /// All 'truth data' here comes from R.  Q score is capped at 100
        /// </summary>
        [Fact]
        public void ComputeGenotypeQualityTests()
        {
            var variant = TestHelper.CreatePassingVariant(true);
            double depth = 100;

            //1) test hom-ref calls for a range of frequencies.
            double[] testFrequencies = new double[] { 0, 0.01, 0.05, 0.10, 0.15, 0.19 };
            int[] expectedResults = new int[] { 97, 97, 78, 60, 24, 1 };
            variant.Genotype = Genotype.HomozygousRef;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            variant = TestHelper.CreatePassingVariant(false);
            //2) test het-alt-ref calls for a range of frequencies.
            testFrequencies = new double[] { 0.2, 0.21, 0.25, 0.30, 0.35, 0.45, 0.49, 0.50, 0.51, 0.55, 0.59, 0.60, 0.61, 0.68, 0.69 };
            expectedResults = new int[] { 1, 1, 13, 49, 67, 88, 68, 68, 68, 48, 47, 47, 47, 25, 25 };
            variant.Genotype = Genotype.HeterozygousAltRef;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //3) test hom-alt- calls for a range of frequencies.
            testFrequencies = new double[] { 0.7, 0.71, 0.75, 0.80, 0.85, 0.90, 0.95, 0.99, 1.0 };
            expectedResults = new int[] { 21, 21, 4, 1, 0, 31, 61, 75, 75 };
            variant.Genotype = Genotype.HomozygousAlt;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            /* TODO: implement some of these
            //4) test het-alt1-alt2 calls for a range of frequencies.
            testFrequencies = new double[] { 0.2, 0.21, 0.25, 0.30, 0.35, 0.45, 0.49, 0.50, 0.51, 0.55, 0.59, 0.60, 0.61, 0.68, 0.69 };
            expectedResults = new int[] { 0, 0, 18, 57, 96, 174, 205, 212, 201, 156, 122, 99, 88, 9, 0 };
            variant.Genotype = Genotype.HeterozygousAlt1Alt2;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //5) test nocalls for a range of frequencies.
            testFrequencies = new double[] { 0, 0.2, 0.5, 1.0 };
            expectedResults = new int[] { 0, 0, 0, 0 };
            variant.Genotype = Genotype.RefLikeNoCall;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //6) test nocalls for a range of frequencies.
            testFrequencies = new double[] { 0, 0.2, 0.5, 1.0 };
            expectedResults = new int[] { 0, 0, 0, 0 };
            variant.Genotype = Genotype.AltLikeNoCall;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //6) spot check different depths
            depth = 1000;

            //6-1) test hom-ref calls for a range of frequencies.
            testFrequencies = new double[] { 0, 0.19 };
            expectedResults = new int[] { 2001, 0 };
            variant.Genotype = Genotype.HomozygousRef;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //6-2) test het-alt-ref calls for a range of frequencies.
            testFrequencies = new double[] { 0.2, 0.5, 0.69 };
            expectedResults = new int[] { 0, 2129, 0 };
            variant.Genotype = Genotype.HeterozygousAltRef;
            TestCalculations(variant, depth, testFrequencies, expectedResults);
            //minor diff with excel: 2141 vs 2129

            //6-3) test hom-alt- calls for a range of frequencies.
            testFrequencies = new double[] { 0.7, 1.0 };
            expectedResults = new int[] { 0, 3653 };
            variant.Genotype = Genotype.HomozygousAlt;
            TestCalculations(variant, depth, testFrequencies, expectedResults);
            //minor diff with excel: 3641 vs 3653

            //6-4) test het-alt1-alt2 calls for a range of frequencies.
            testFrequencies = new double[] { 0.2, 0.5, 0.69 };
            expectedResults = new int[] { 0, 2129, 0 };
            variant.Genotype = Genotype.HeterozygousAlt1Alt2;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //6-5) test nocalls for a range of frequencies.
            testFrequencies = new double[] { 0, 0.2, 0.5, 1.0 };
            expectedResults = new int[] { 0, 0, 0, 0 };
            variant.Genotype = Genotype.RefLikeNoCall;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //6-6) test nocalls for a range of frequencies.
            testFrequencies = new double[] { 0, 0.2, 0.5, 1.0 };
            expectedResults = new int[] { 0, 0, 0, 0 };
            variant.Genotype = Genotype.AltLikeNoCall;
            TestCalculations(variant, depth, testFrequencies, expectedResults);
            */
        }

        [Fact]
        public void GetMultiAllelicQScores()
        {
            CalledAllele variant1 = TestHelper.CreateDummyAllele("chr1", 1000, "A", "C", 30, 12);
            CalledAllele variant2 = TestHelper.CreateDummyAllele("chr1", 1000, "A", "T", 30, 11);

            MixtureModelResult result = AdaptiveGenotyperCalculator.GetMultiAllelicQScores(variant1, variant2, 
                new List<double[]> { Means, Means });

            // The 4th GP should always be the minimum because that reflects the 1/2 call
            Assert.Equal(4, result.GenotypePosteriors.ToList().IndexOf(result.GenotypePosteriors.Min()));
        }

        private void TestCalculations(CalledAllele variant, double depth, double[] testFrequencies, int[] expectedResults)
        {
            for (int i = 0; i < testFrequencies.Length; i++)
                TestCalculation(variant, testFrequencies[i], depth, expectedResults[i]);
        }

        private void TestCalculation(CalledAllele variant, double frequency, double depth, int expectedValue)
        {
            variant.TotalCoverage = (int)depth;
            variant.AlleleSupport = (int)(depth * frequency);

            if (variant.Genotype == Genotype.HomozygousRef)
                variant.AlleleSupport = (int)(depth * (1.0 - frequency));

            MixtureModelResult result = AdaptiveGenotyperCalculator.GetGenotypeAndQScore(variant, Means, Priors);
            Assert.Equal(expectedValue, result.QScore);
        }
    }
}
