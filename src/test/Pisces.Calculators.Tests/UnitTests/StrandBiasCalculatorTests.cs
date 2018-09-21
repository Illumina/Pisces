using System;
using System.Collections.Generic;
using Pisces.Calculators;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Calculators.Tests
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
        /// Verify SB can be calculated differently for Somatic and Diploid cases, with the expected behavior
        /// </summary>
        [Fact]
        public void TestSBCalculationsForSomaticAndDiploidSettings()
        {
            double fwdCov = 10000;
            double revCov = 10000;
            double testVariantFreqA = 0.05;
            double testVariantFreqB = 0.25;
            double testVariantFreqC = 0.020;
            double testVariantFreqD = 0.005;

            var CoverageByStrandDirection = new int[] { (int)fwdCov, (int)revCov, 0 }; //forward,reverse,stitched
            var EqualSupportByStrandDirectionA =  new int[] { (int)(fwdCov * testVariantFreqA), (int)(revCov * testVariantFreqA), 0 };
            var EqualSupportByStrandDirectionB = new int[] { (int)(fwdCov * testVariantFreqB), (int)(revCov * testVariantFreqB), 0 };

            //happy path, no bias

            BiasResults SB_somatic = StrandBiasCalculator.CalculateStrandBiasResults(
                CoverageByStrandDirection, EqualSupportByStrandDirectionB, 20, 0.01, 0.5, StrandBiasModel.Extended);

            BiasResults SB_diploid = StrandBiasCalculator.CalculateStrandBiasResults(
           CoverageByStrandDirection, EqualSupportByStrandDirectionB, 20, 0.20, 0.5, StrandBiasModel.Diploid);

            Assert.Equal(SB_somatic.BiasScore, 0);
            Assert.Equal(SB_somatic.GATKBiasScore, double.NegativeInfinity);
            Assert.Equal(SB_somatic.BiasAcceptable, true);

            Assert.Equal(SB_diploid.BiasScore, 0);
            Assert.Equal(SB_diploid.GATKBiasScore, double.NegativeInfinity);
            Assert.Equal(SB_diploid.BiasAcceptable, true);
            
            //bias if you are looking for a 20% variant (only one side is sufficient to call),
            //but not biased in the somatic case (both show up sufficiently)

            var SupportByStrandDirection_bias20 = new int[] { (int)(fwdCov * testVariantFreqA), (int)(revCov * testVariantFreqB), 0 };
            SB_somatic = StrandBiasCalculator.CalculateStrandBiasResults(
                CoverageByStrandDirection, SupportByStrandDirection_bias20, 20, 0.01, 0.5, StrandBiasModel.Extended);
            SB_diploid = StrandBiasCalculator.CalculateStrandBiasResults(
                CoverageByStrandDirection, SupportByStrandDirection_bias20, 20, 0.20, 0.5, StrandBiasModel.Diploid);

            Assert.Equal(SB_somatic.BiasScore, 0);
            Assert.Equal(SB_somatic.GATKBiasScore, double.NegativeInfinity);
            Assert.Equal(SB_somatic.BiasAcceptable, true);

            Assert.Equal(Math.Log10( SB_diploid.BiasScore), 74.3, 1); // a great big bias
            Assert.Equal(SB_diploid.GATKBiasScore, 743.5, 1);
            Assert.Equal(SB_diploid.BiasAcceptable, false);

            //bias if you are looking for even a 1% variant or a 20% variant

            var SupportByStrandDirection_bias01 = new int[] { (int)(fwdCov * testVariantFreqC), (int)(revCov * testVariantFreqD), 0 };
            SB_somatic = StrandBiasCalculator.CalculateStrandBiasResults(
                CoverageByStrandDirection, SupportByStrandDirection_bias01, 20, 0.01, 0.5, StrandBiasModel.Extended);
            SB_diploid = StrandBiasCalculator.CalculateStrandBiasResults(
                CoverageByStrandDirection, SupportByStrandDirection_bias01, 20, 0.20, 0.5, StrandBiasModel.Diploid);

            Assert.Equal(SB_somatic.BiasScore, 1.000, 3);
            Assert.Equal(SB_somatic.GATKBiasScore, 0.002, 3);
            Assert.Equal(SB_somatic.BiasAcceptable, false);

            Assert.Equal(SB_diploid.BiasScore, 1.000, 3);// a great big bias
            Assert.Equal(SB_diploid.GATKBiasScore, 0.000, 3);
            Assert.Equal(SB_diploid.BiasAcceptable, false);

        }

        [Fact]
        public void TestDistributionFxn()
        {


            //trickier case, when we barely see it but we dont have enough reads...
            var binomialHetAltExpected = new MathNet.Numerics.Distributions.Binomial(0.20, 100);


            //sps you saw a variant at {15%,20%,25%}. is that real? given diploid expectations?
            double ChanceYouGetUpTo15 = binomialHetAltExpected.CumulativeDistribution(15); //should be about half the time
            double ChanceYouGetUpTo20 = binomialHetAltExpected.CumulativeDistribution(20); //should be about half the time
            double ChanceYouGetUpTo25 = binomialHetAltExpected.CumulativeDistribution(25); //should be about half the time

            Assert.Equal(ChanceYouGetUpTo15, 0.129, 3);
            Assert.Equal(ChanceYouGetUpTo20, 0.559, 3);
            Assert.Equal(ChanceYouGetUpTo25, 0.913, 3);
        }

        [Fact]
        public  void TestPopulateDiploidStats()
        {

            double noiseFreq = 0.01;
            double diploidThreshold = 0.20;

            //Cases where the variant obviously exisits

            StrandBiasStats stats = new StrandBiasStats(100, 100); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 1, 3); 
            Assert.Equal(stats.ChanceFalsePos, 0, 3); 
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 1, 3);


            stats = new StrandBiasStats(50, 100); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 1, 3);
            Assert.Equal(stats.ChanceFalsePos, 0, 3); 
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 1, 3);


            stats = new StrandBiasStats(20, 100); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 1, 3); 
            Assert.Equal(stats.ChanceFalsePos, 0, 3); 
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 1, 3);

            //

            //Cases where the variant becomes less obvious

            stats = new StrandBiasStats(15, 100); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 0.129, 3); //Chance this is a real variant ( a false neg if we filtered it)//it could happen that this is still real
            Assert.Equal(stats.ChanceFalsePos, 0.049, 3); //chance this is due to noise ( a false pos if we left it in). not very likely
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 0.129, 3);


            stats = new StrandBiasStats(10, 100); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 0.006, 3); //Chance this is a real variant ( a false neg if we filtered it)//it could happen that this is still real
            Assert.Equal(stats.ChanceFalsePos, 0.417, 3); //chance this is due to noise ( a false pos if we left it in). not very likely
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 0.006, 3);


            stats = new StrandBiasStats(1, 100); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 0, 3); //Chance this is a real variant ( a false neg if we filtered it)//it could happen that this is still real
            Assert.Equal(stats.ChanceFalsePos, 1, 3); //chance this is due to noise ( a false pos if we left it in). not very likely
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 0, 3);

            //a few pathological cases

            stats = new StrandBiasStats(0, 100); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 0, 3); 
            Assert.Equal(stats.ChanceFalsePos, 1, 3); 
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 0, 3);


            stats = new StrandBiasStats(10, 0); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 1, 3); 
            Assert.Equal(stats.ChanceFalsePos, 0, 3);
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 1, 3);


            stats = new StrandBiasStats(0, 0); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 1, 3);   //not a meaningful answer, but at least nothing explodes.
            Assert.Equal(stats.ChanceFalsePos, 0, 3); 
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 1, 3);


            stats = new StrandBiasStats(101, 100); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 1, 3); 
            Assert.Equal(stats.ChanceFalsePos, 0, 3); 
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 1, 3);

            //check it reacts properly to depth. Ie, a 15% variant in N of 20 isnt a big deal,
            //but a 15% varaint in N of 100000 seems rather low.

            stats = new StrandBiasStats((20.0*0.15), 20); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 0.411, 3);  //note, the believability of this variant goes up from 0.129
            Assert.Equal(stats.ChanceFalsePos, 0.143, 3);   //but its also more possible to be noise. Basically, the whole picture is more murky
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 0.411, 3);

            stats = new StrandBiasStats(15, 100); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 0.129, 3); 
            Assert.Equal(stats.ChanceFalsePos, 0.049, 3); 
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 0.129, 3);

            //slightly more lilkey to be a variant than noise, but neither hypothesis fits.
            stats = new StrandBiasStats((500.0 * 0.15), 500); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 0.002, 3);
            Assert.Equal(stats.ChanceFalsePos, 0, 3);
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 0.002, 3);

            //it doesnt look like noise or a varaint. no hypothesis is reasonable.
            stats = new StrandBiasStats((100000.0 * 0.15), 100000); //#observations, coverage
            StrandBiasCalculator.PopulateDiploidStats(stats, noiseFreq, diploidThreshold);
            Assert.Equal(stats.ChanceFalseNeg, 0, 3); 
            Assert.Equal(stats.ChanceFalsePos, 0, 3); 
            Assert.Equal(stats.ChanceVarFreqGreaterThanZero, 0, 3);

        }

        /// <summary>
        /// Verify SB can be calculated on forcedGT variants that may not (shock!) be present on any strand at the 1% cutoff.
        /// </summary>
        [Fact]
        public void TestSBCalculationsForForcedVariants()
        {
            
            var CoverageByStrandDirection = new int[] {70038,65998,0 }; //forward,reverse,stitched
            var SupportByStrandDirection = new int[] { 54,11,0 };

            BiasResults SB = StrandBiasCalculator.CalculateStrandBiasResults(
                CoverageByStrandDirection, SupportByStrandDirection, 20, 0.01, 0.5, StrandBiasModel.Poisson);

            Assert.Equal(SB.BiasScore, 1.0);

            Assert.Equal(SB.GATKBiasScore, 0);

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

        private BiasResults ExecuteTest(Tuple<double, int> forwardStats, Tuple<double, int> reverseStats, Tuple<double, int> stitchedStats, 
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

            var variant = new CalledAllele(AlleleCategory.Snv)
            {
                EstimatedCoverageByDirection = new int[]
                        {
                            forwardStats.Item2, reverseStats.Item2, stitchedStats.Item2
                        }
            };

            StrandBiasCalculator.Compute(variant, support, estimatedBaseCallQuality, 0.01, threshold, model);
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
