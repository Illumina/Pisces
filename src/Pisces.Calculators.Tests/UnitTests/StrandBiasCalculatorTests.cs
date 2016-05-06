using System;
using System.Collections.Generic;
using Pisces.Calculators;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Tests.UnitTests.Logic.Calculators
{
    // bring over previous unit tests
    public class StrandBiasCalculatorTests
    {
        [Fact]
        [Trait("ReqID", "SDS-42")]
        [Trait("ReqID", "SDS-43")]
        public void HappyPath()
        {
            const double fwdFreq = 0.05;
            const double revFreq = 0.0;
            const double stitchedFreq = 0.0;

            const int fwdDepth = 1000;
            const int stitchedDepth = 1;
            int revDepth = 0;

            for (var i = 0; i < 1000; i++)
            {
                var forwardStats = new Tuple<double, int>(fwdFreq, fwdDepth);
                var reverseStats = new Tuple<double, int>(revFreq, revDepth);
                var stitchedStats = new Tuple<double, int>(stitchedFreq, stitchedDepth);

                var results = ExecuteTest(forwardStats, reverseStats, stitchedStats);

                if (revDepth == 0)
                    Assert.True(results.BiasAcceptable); // no coverage
                else
                {
                    Assert.False(results.BiasAcceptable); // why false?  Need to talk to Tamsen to get valid stitched values.

                    // make sure threshold is properly applied
                    var passingResults = ExecuteTest(forwardStats, reverseStats, stitchedStats, 20, (float)(results.BiasScore + 0.00001));
                    Assert.True(passingResults.BiasAcceptable);
                }

                revDepth++;
            }

            //notes:
            //There is a 50% we will never observe a 1% freq snp in 67 coverage.
            //There is a 3% we will never observe a 5% freq snp in 67 coverage. 
            //(note quick roll off as a function of minimum freqency - if the min detectable freq is 5%,
            //our effective cutoff is depth > 13)

            //With our Bias Acceptance criteria of 0.5, and min freq of 1%
            //And in the example sps, the SNP shows up at 5% in the fwd direction with a depth of 1000. (alth the alg is not very senitive to that)
            //This means while rev depth is < 67, we accept 0-support results as unbiased.
            //when depth is greater, we will start throwing out results.
        }

        [Fact]
        [Trait("ReqID", "SDS-42")]
        [Trait("ReqID", "SDS-43")]
        public void TestVaryingCoverage()
        {
            double forwardFreq = 0.01;
            const double reverseFrequency = 0.09;
            const double stitchedFrequency = 0.09;
            const int revCoverage = 1000;
            const int stitchedCoverage = 1000;

            while (forwardFreq < 0.10)
            {
                for (var fwdCoverage = 0; fwdCoverage < 2000; )
                {
                    fwdCoverage = fwdCoverage + 100;
                    var forwardStats = new Tuple<double, int>(forwardFreq, fwdCoverage);
                    var reverseStats = new Tuple<double, int>(reverseFrequency, revCoverage);
                    var stitchedStats = new Tuple<double, int>(stitchedFrequency, stitchedCoverage);

                    var results = ExecuteTest(forwardStats, reverseStats, stitchedStats);
                    Assert.True(results.BiasAcceptable);
                }

                forwardFreq += 0.01;
            }
        }

        /// <summary>
        /// Verify variant and coverage present on both strands flag is properly set.
        /// </summary>
        [Fact]
        [Trait("ReqID", "SDS-42")]
        [Trait("ReqID", "SDS-43")]
        public void TestPresentOnBothStrands()
        {
            // ------------------------------
            // Expected: var and coverage present on both strands
            // ------------------------------

            // Detected in all directions
            ExecuteBothStrandTest(0.1f, 500, 0.1f, 500, 0.1f, 500, true, true);
            // Detected in one direction and stitched (stitched contributes to both)
            ExecuteBothStrandTest(0.1f, 500, 0f, 0, 0.1f, 500, true, true); 
            ExecuteBothStrandTest(0f, 0, 0.1f, 500, 0.1f, 500, true, true);
            //Detected in fwd and rev but not stitched
            ExecuteBothStrandTest(0.1f, 500, 0.1f, 500, 0f, 0, true, true);
            //Detected in stitched only
            ExecuteBothStrandTest(0f, 0, 0f, 0, 0.5f, 500, true, true);

            // ------------------------------
            // Expected: no var and coverage present on both strands
            // ------------------------------

            // not detected in all directions
            ExecuteBothStrandTest(0f, 0, 0f, 0, 0f, 0, false, false);
            // only detected in one direction
            ExecuteBothStrandTest(0.1f, 500, 0f, 0, 0f, 0, false, false);
            ExecuteBothStrandTest(0f, 0, 0.2f, 500, 0f, 0, false, false);

            // ------------------------------
            // Expected: cov present but not var
            // ------------------------------

            // couple permutations of above scenarios, but separately for var vs cov (not exhaustive)
            ExecuteBothStrandTest(0.1f, 500, 0f, 500, 0f, 500, false, true);
            ExecuteBothStrandTest(0.1f, 500, 0f, 0, 0f, 500, false, true);
            ExecuteBothStrandTest(0.1f, 500, 0f, 500, 0f, 0, false, true);
        }

        private void ExecuteBothStrandTest(float fwdFreq, int fwdDepth, float revFreq, int revDepth, float stitchedFreq,
            int stitchedDepth,
            bool expectedVarPresent, bool expectedCovPresent)
        {
            var forwardStats = new Tuple<double, int>(fwdFreq, fwdDepth);
            var reverseStats = new Tuple<double, int>(revFreq, revDepth);
            var stitchedStats = new Tuple<double, int>(stitchedFreq, stitchedDepth);

            var results = ExecuteTest(forwardStats, reverseStats, stitchedStats);
            Assert.Equal(expectedVarPresent, results.VarPresentOnBothStrands);
            Assert.Equal(expectedCovPresent, results.CovPresentOnBothStrands);
        }

        private StrandBiasResults ExecuteTest(Tuple<double, int> forwardStats, Tuple<double, int> reverseStats, Tuple<double, int> stitchedStats, 
            int estimatedBaseCallQuality = 20, float threshold = 0.5f, StrandBiasModel model = StrandBiasModel.Poisson)
        {
            var origForwardSupport = (int) (forwardStats.Item1*forwardStats.Item2);
            var origReverseSupport = (int) (reverseStats.Item1*reverseStats.Item2);
            var origStitchedSupport = (int) (stitchedStats.Item1*stitchedStats.Item2);
            var support = new int[]
                    {
                        origForwardSupport,
                        origReverseSupport,
                        origStitchedSupport,
                    };

            var variant = new CalledVariant(AlleleCategory.Snv)
            {
                TotalCoverageByDirection = new int[]
                        {
                            forwardStats.Item2, reverseStats.Item2, stitchedStats.Item2
                        }
            };

            StrandBiasCalculator.Compute(variant, support, estimatedBaseCallQuality, threshold, model);
            Assert.Equal(origForwardSupport + ((float)origStitchedSupport/2), variant.StrandBiasResults.ForwardStats.Support);
            Assert.Equal(origReverseSupport + ((float)origStitchedSupport / 2), variant.StrandBiasResults.ReverseStats.Support);
            return variant.StrandBiasResults;
        }

        [Fact]
        public void T_Tests()
        {
            var dataSet1 = GenerateTestData(1000, 0.2);

            var m1 = 0.2;

            var var1 = 0.0;

            foreach (var d in dataSet1)
            {
                var1 += (m1 - d) * (m1 - d); //0.16
            }

            var1 = var1 / dataSet1.Count;

            //According to Poisson, var1= lamda1 = 0.02
            var directVar = m1 * (1 - m1);

            Assert.Equal(directVar, var1, 9);  //, 0.000000001

            var s12 = MathOperations.PooledEstimatorForSigma(1000, 1000, directVar, directVar);
            Assert.Equal(var1, s12, 9);  //, 0.000000001

            var s22 = MathOperations.PooledEstimatorForSigma(10000, 1000, directVar, 0.9);
            Assert.Equal(0.23, s22, 2);  //, 0.05
        }

        private static List<double> GenerateTestData(int N1, double F1)
        {
            var dataSet = new List<double> { };
            for (int i = 0; i < N1; i++)
            {
                if (i < F1 * N1)
                {
                    dataSet.Add(1);
                }
                else
                    dataSet.Add(0);

            }
            return dataSet;
        }
    }
}
