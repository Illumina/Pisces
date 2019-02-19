using Pisces.Genotyping;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;

namespace Pisces.Genotyping.Tests
{
    public class DiploidGenotypeQualityTests
    {
        /// <summary>
        /// All 'truth data' here comes from excel.
        /// </summary>
        [Fact]
        [Trait("ReqID", "??-??")]
        public void ComputeGenotypeQualityTests()
        {
            var variant = TestHelper.CreatePassingVariant(true);
            double depth = 100;

            //1) test hom-ref calls for a range of frequencies.
            double[] testFrequencies = new double[] { 0, 0.01, 0.05, 0.10, 0.15, 0.19 };
            int[] expectedResults = new int[] { 200, 188, 144, 89, 36, 0 };
            variant.Genotype = Genotype.HomozygousRef;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //2) test het-alt-ref calls for a range of frequencies.
            testFrequencies = new double[] { 0.2, 0.21, 0.25, 0.30, 0.35, 0.45, 0.49, 0.50, 0.51, 0.55, 0.59, 0.60, 0.61, 0.68, 0.69 };
            expectedResults = new int[] { 0, 0, 18, 57, 96, 174, 205, 212, 201, 156, 122, 99, 88, 9, 0 };
            variant.Genotype = Genotype.HeterozygousAltRef;
            TestCalculations(variant, depth, testFrequencies, expectedResults);

            //3) test hom-alt- calls for a range of frequencies.
            testFrequencies = new double[] { 0.7, 0.71, 0.75, 0.80, 0.85, 0.90, 0.95, 0.99, 1.0 };
            expectedResults = new int[] { 0, 7, 54, 114, 175, 237, 300, 352, 365 };
            variant.Genotype = Genotype.HomozygousAlt;
            TestCalculations(variant, depth, testFrequencies, expectedResults);
            //minor diff with excel: 114 vs 118, 238 vs 237

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

        }

        [Fact]
        [Trait("ReqID", "PICS-849")]
        public void HigFreqInsertionGT_Test()
        {
            var variant = TestHelper.CreatePassingVariant(true);
            double depth = 100;

            //1) The "1.19" means we had
            // 1.19 x allele calls vs total depth. 
            //This tests the odd case where we called more insertions than we had coverage.
            //Yes, this can happen for insertions.
            double[] testFrequencies = new double[] { 1.19, 0.00, };

            //this returned 0 where it now says MaxValue when we had the bug
            int[] expectedResults = new int[] { int.MaxValue, 0};

            variant.Genotype = Genotype.HomozygousAlt;
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

            if (variant.Genotype == Genotype.HomozygousRef)
                variant.AlleleSupport = (int)(depth * (1.0 - frequency));

            int result = DiploidGenotypeQualityCalculator.Compute(variant, 0, int.MaxValue);
            Assert.Equal(expectedValue, result);
        }
    }
}