using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CallSomaticVariants.Logic.Calculators;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;
using Xunit;
namespace CallSomaticVariants.Logic.Calculators.Tests
{
    public class QualityCalculatorTests
    {

        /// <summary>
        ///     Direct port of PValue test from old CallSomaticVariants.
        /// </summary>
        [Fact]
        [Trait("ReqID", "SDS-41")]
        public void CallSomaticVariants_AssignPValue()
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
                double pValue = QualityCalculator.AssignPValue(item[1], item[0], 20);
                double Qscore = MathOperations.PtoQ(pValue);
                double FinalQValue = QualityCalculator.AssignPoissonQScore(item[1], item[0], 20, 100);
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
            new int[]{5000,250, 100},
            };

            foreach (int[] item in SampleValues_ExpectedQScore)
            {
                var variant = new CalledVariant(AlleleCategory.Snv)
                {
                    Coordinate = 1,
                    Reference = "A",
                    Alternate = "T",
                    TotalCoverage = item[0],
                    AlleleSupport = item[1],
                };

                QualityCalculator.Compute(variant, 100, 20);

                Assert.Equal(item[2],variant.Qscore);
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

            double finalQValue = QualityCalculator.AssignPoissonQScore(varSupport, coverage, estQuality, maxQValue);
            Assert.Equal(expectedActualQValue,finalQValue);

            maxQValue = expectedActualQValue - 1;
            finalQValue = QualityCalculator.AssignPoissonQScore(varSupport, coverage, estQuality, maxQValue);
            Assert.Equal(maxQValue, finalQValue);

        }

        [Fact]
        [Trait("ReqID", "SDS-41")]
        public void AssignPValue()
        {
            var callCount = 1;
            double pValue = QualityCalculator.AssignPValue(callCount, 100, 20);
            Assert.Equal(0.6321, pValue, 4);

            //If observed call count is 0, return 1
            callCount = 0;
            pValue = QualityCalculator.AssignPValue(callCount, 100, 20);
            Assert.Equal(1, pValue, 4);

        }
    
    }
}
