using System;
using System.Collections.Generic;
using Pisces.Calculators;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;
namespace Pisces.Calculators.Tests
{
    public class QualityCalculatorTests
    {

        /// <summary>
        ///     Direct port of PValue test from old Pisces.
        /// </summary>
        [Fact]
        [Trait("ReqID", "SDS-41")]
        public void Pisces_AssignPValue()
        {
            List<int[]> SampleValues = new List<int[]>(){ //coverage,var calls}
            new int[]{100,0},
            new int[]{100,1},
            new int[]{100,5},
            new int[]{200,10},
            new int[]{500,25},
            new int[]{5000,250},
            };

            List<double[]> ReturnedValues = new List<double[]>(); { }//p,Q

            foreach (int[] item in SampleValues)
            {
                double pValue = VariantQualityCalculator.AssignPValue(item[1], item[0], 20);
                double Qscore = MathOperations.PtoQ(pValue);
                double FinalQValue = VariantQualityCalculator.AssignPoissonQScore(item[1], item[0], 20, 100);
                double[] Result = new double[] { pValue, Qscore, FinalQValue };
                ReturnedValues.Add(Result);

            }

            Assert.Equal(ReturnedValues[0][0], 1, 4);
            Assert.Equal(ReturnedValues[0][2], 0, 4);

            Assert.Equal(ReturnedValues[1][0], 0.6321, 4);
            Assert.Equal(ReturnedValues[1][2], 2, 4);

            Assert.Equal(ReturnedValues[2][0], 0.003659, 4);
            Assert.Equal(ReturnedValues[2][2], 24, 4);

            Assert.Equal(ReturnedValues[3][0], 4.65 * Math.Pow(10, -5), 5);
            Assert.Equal(ReturnedValues[3][2], 43, 4);

            Assert.Equal(ReturnedValues[4][0], 1.599 * Math.Pow(10, -10), 10);
            Assert.Equal(ReturnedValues[4][2], 98, 4);

            Assert.Equal(ReturnedValues[5][0], 0.0, 10);
            Assert.Equal(ReturnedValues[5][2], 100, 4);

        }

        [Fact]
        [Trait("ReqID", "SDS-41")]
        public void Compute()
        {
            // Based on Tamsen's original PValue test, just extended to our Compute method
            List<int[]> SampleValues_ExpectedQScore = new List<int[]>(){ //coverage,var calls}
            new int[]{100,0, 0},
            new int[]{100,1, 2},
            new int[]{100,5, 24},
            new int[]{200,10, 43},
            new int[]{500,25, 98},
            new int[]{5000,250, 890 },
            new int[]{10000,250, 356},
            new int[]{10000,500, 1770},
            new int[]{10000,9995, 156912}, //ok, this is a silly number. but we are checking the range..
            };

            foreach (int[] item in SampleValues_ExpectedQScore)
            {
                var variant = new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 1,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    TotalCoverage = item[0],
                    AlleleSupport = item[1],
                };

                VariantQualityCalculator.Compute(variant, int.MaxValue, 20);
                Assert.Equal(item[2],variant.VariantQscore);

                //check upped bd works:
                VariantQualityCalculator.Compute(variant, 100, 20);
                Assert.Equal(Math.Min(100,item[2]), variant.VariantQscore);
            }
        }

        [Fact]
        [Trait("ReqID", "SDS-41")]
        public void AssignPoissonQScore()
        {
            //If rounded qScore is above maxQScore, should return maxQScore
            var coverage = 500;
            var varSupport = 25;
            var estQuality = 20;
            var expectedActualQValue = 98;
            var maxQValue = expectedActualQValue + 1;
            maxQValue = 1000;

            double finalQValue = VariantQualityCalculator.AssignPoissonQScore(varSupport, coverage, estQuality, maxQValue);
            Assert.Equal(expectedActualQValue,finalQValue);

            maxQValue = expectedActualQValue - 1;
            finalQValue = VariantQualityCalculator.AssignPoissonQScore(varSupport, coverage, estQuality, maxQValue);
            Assert.Equal(maxQValue, finalQValue);

        }

        [Fact]
        [Trait("ReqID", "SDS-41")]
        public void AssignPValue()
        {
            var callCount = 1;
            double pValue = VariantQualityCalculator.AssignPValue(callCount, 100, 20);
            Assert.Equal(0.6321, pValue, 4);

            //If observed call count is 0, return 1
            callCount = 0;
            pValue = VariantQualityCalculator.AssignPValue(callCount, 100, 20);
            Assert.Equal(1, pValue, 4);

        }
    
    }
}
