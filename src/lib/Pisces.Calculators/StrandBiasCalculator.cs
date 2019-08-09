using System;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Calculators
{
    public static class StrandBiasCalculator
    {
        public static void Compute(CalledAllele variant, int[] supportByDirection, int qNoise, double minVariantFrequency, double acceptanceCriteria,
            StrandBiasModel strandBiasModel)
        {
            variant.StrandBiasResults = CalculateStrandBiasResults(
                variant.EstimatedCoverageByDirection, supportByDirection, qNoise,  minVariantFrequency, acceptanceCriteria, strandBiasModel);
        }

        /// <summary>
        ///     Assign a strandbias-score to a SNP.
        ///     (using only forward and reverse SNP counts.)
        /// </summary>
        public static BiasResults CalculateStrandBiasResults(int[] coverageByStrandDirection,
            int[] supportByStrandDirection,
            int qNoise, double minVariantFreq, double acceptanceCriteria, StrandBiasModel strandBiasModel)
        {
            var forwardSupport = supportByStrandDirection[(int)DirectionType.Forward];
            var forwardCoverage = coverageByStrandDirection[(int)DirectionType.Forward];
            var reverseSupport = supportByStrandDirection[(int)DirectionType.Reverse];
            var reverseCoverage = coverageByStrandDirection[(int)DirectionType.Reverse];
            var stitchedSupport = supportByStrandDirection[(int)DirectionType.Stitched];
            var stitchedCoverage = coverageByStrandDirection[(int)DirectionType.Stitched];

            var errorRate = Math.Pow(10, -1*qNoise/10f);

            var overallStats = CreateStats(forwardSupport + reverseSupport + stitchedSupport,
                forwardCoverage + reverseCoverage + stitchedCoverage, errorRate, minVariantFreq, strandBiasModel);
            var forwardStats = CreateStats(forwardSupport + stitchedSupport / 2,
                forwardCoverage + stitchedCoverage / 2,
                errorRate, minVariantFreq, strandBiasModel);
            var reverseStats = CreateStats(reverseSupport + stitchedSupport / 2,
                reverseCoverage + stitchedCoverage / 2,
                errorRate, minVariantFreq, strandBiasModel);

            var results = new BiasResults
            {
                ForwardStats = forwardStats,
                ReverseStats = reverseStats,
                OverallStats = overallStats
            };

            results.StitchedStats = CreateStats(stitchedSupport, stitchedCoverage, errorRate, minVariantFreq,
                strandBiasModel);

            var biasResults = AssignBiasScore(overallStats, forwardStats, reverseStats);

            results.BiasScore = biasResults[0];
            results.GATKBiasScore = biasResults[1];
            results.CovPresentOnBothStrands = ((forwardStats.Coverage > 0) && (reverseStats.Coverage > 0));
            results.VarPresentOnBothStrands = ((forwardStats.Support > 0) && (reverseStats.Support > 0));

            //not really fair to call it biased if coverage is in one direction..
            //its ambiguous if variant is found in only one direction.
            if (!results.CovPresentOnBothStrands)
            {
                results.BiasScore = 0;
                results.GATKBiasScore = double.NegativeInfinity;
            }

        
            results.BiasAcceptable = (results.BiasScore < acceptanceCriteria);

            return results;
        }

        /// <summary>
        ///     http://www.broadinstitute.org/gsa/wiki/index.php/Understanding_the_Unified_Genotyper%27s_VCF_files
        ///     See section on Strand Bias
        /// </summary>
        // From GATK source:
        //double forwardLod = forwardLog10PofF + reverseLog10PofNull - overallLog10PofF;
        //double reverseLod = reverseLog10PofF + forwardLog10PofNull - overallLog10PofF;
        //
        //// strand score is max bias between forward and reverse strands
        //double strandScore = Math.max(forwardLod, reverseLod);
        //
        //// rescale by a factor of 10
        //strandScore *= 10.0;
        //
        //attributes.put("SB", strandScore);
        private static double[] AssignBiasScore(StrandBiasStats overallStats, StrandBiasStats fwdStats, StrandBiasStats rvsStats)
        {
            var forwardBias = (fwdStats.ChanceVarFreqGreaterThanZero * rvsStats.ChanceFalsePos) /
                                 overallStats.ChanceVarFreqGreaterThanZero;
            var reverseBias = (rvsStats.ChanceVarFreqGreaterThanZero * fwdStats.ChanceFalsePos) /
                                 overallStats.ChanceVarFreqGreaterThanZero;

            if (overallStats.ChanceVarFreqGreaterThanZero == 0)
            {
                forwardBias = 1;
                reverseBias = 1;
            }

            var p = Math.Max(forwardBias, reverseBias);

            return new[] { p, MathOperations.PtoGATKBiasScale(p) };
        }

        private static bool ValueAcceptable(double levelOfSignificance, double tvalue, double degreesOfFreedom)
        {
            var alphaOver2 = levelOfSignificance / 2.0;
            var rejectionRegion = 1.282;

            if (degreesOfFreedom < 30)
            {
                return false; //just don't call anything for now.
            }

            //From "Mathematical Statistics With Applications" 6th ed.
            if (alphaOver2 < 0.005)
                rejectionRegion = 2.576;
            else if (alphaOver2 < 0.01)
                rejectionRegion = 2.576;
            else if (alphaOver2 < 0.025)
                rejectionRegion = 2.326;
            else if (alphaOver2 < 0.05)
                rejectionRegion = 1.960;
            else if (alphaOver2 < 0.1)
                rejectionRegion = 1.645;

            if (Math.Abs(tvalue) > rejectionRegion)
            {
                return false;
            }

            return true;
        }

        public static StrandBiasStats CreateStats(double support, double coverage, double noiseFreq, double minDetectableSNP,
            StrandBiasModel strandBiasModel)
        {

            if (strandBiasModel != StrandBiasModel.Diploid)
                minDetectableSNP = noiseFreq;

            var stats = new StrandBiasStats(support, coverage);
            PopulateStats(stats, noiseFreq, minDetectableSNP, strandBiasModel);

            return stats;
        }

        public static void PopulateDiploidStats(StrandBiasStats stats, double noiseFreq, double minDetectableSNP)
        {
            //expectation we ought to see the 20% variant on this strand:

            //save ourself some time here..
            if (stats.Frequency >= minDetectableSNP)
            {
                stats.ChanceFalseNeg = 1; // TP if we called it
                stats.ChanceFalsePos = 0; //FP if we called if
                stats.ChanceVarFreqGreaterThanZero = 1;
                return;
            }

            //trickier case, when we barely see it but we dont have enough reads...
            var binomialHetAltExpected = new MathNet.Numerics.Distributions.Binomial(minDetectableSNP, (int)stats.Coverage);

            //this is a real variant ( a false neg if we filtered it)
            stats.ChanceFalseNeg = Math.Max(binomialHetAltExpected.CumulativeDistribution(stats.Support), 0); //if this was a het variant, would we ever see it this low?

            //chance this is due to noise ( a false pos if we left it in)
            stats.ChanceFalsePos = Math.Max(0.0, 1 - Poisson.Cdf(stats.Support, stats.Coverage * 0.1)); //chance this varaint is due to noise, we could see this much or more

            stats.ChanceVarFreqGreaterThanZero = stats.ChanceFalseNeg;
        }

        public static void PopulateStats(StrandBiasStats stats, double noiseFreq, double minDetectableSNP,
            StrandBiasModel strandBiasModel)
        {
            if (stats.Support == 0)
            {
                if (strandBiasModel == StrandBiasModel.Poisson)
                {
                    stats.ChanceFalsePos = 1;
                    stats.ChanceVarFreqGreaterThanZero = 0;
                    stats.ChanceFalseNeg = 0;
                }
                else if ((strandBiasModel == StrandBiasModel.Extended) || (strandBiasModel == StrandBiasModel.Diploid))
                {


                    //the chance that we observe the SNP is (minDetectableSNPfreq) for one observation.
                    //the chance that we do not is (1- minDetectableSNPfreq) for one observation.
                    //the chance that we do not observe it, N times in a row is:
                    stats.ChanceVarFreqGreaterThanZero = (Math.Pow(1 - minDetectableSNP, stats.Coverage)); //used in SB metric

                    //liklihood that variant really does not exist
                    //= 1 - chance that it does but you did not see it
                    stats.ChanceFalsePos = 1 - stats.ChanceVarFreqGreaterThanZero; //used in SB metric

                    //Chance a low freq variant is at work in the model, and we did not observe it:
                    stats.ChanceFalseNeg = stats.ChanceVarFreqGreaterThanZero;
                }
            }
            else
            {
                if (strandBiasModel == StrandBiasModel.Diploid)
                {
                    PopulateDiploidStats(stats, noiseFreq, minDetectableSNP);
                }
                else
                {
                    // chance of these observations or less, given min observable variant distribution
                    stats.ChanceVarFreqGreaterThanZero = Math.Max(0, Poisson.Cdf(stats.Support - 1, stats.Coverage * noiseFreq)); //used in SB metric
                    stats.ChanceFalsePos = Math.Max(0, 1 - stats.ChanceVarFreqGreaterThanZero); //used in SB metric
                    stats.ChanceFalseNeg = Math.Max(0, Poisson.Cdf(stats.Support, stats.Coverage * minDetectableSNP));

                    //NOTE:
                    // Q: Why the Math.Max? 
                    // A: B/c with the forced GT feature, we began calculating SB for variants whose chance of existing (given the observations)
                    // was zero. (or subltly negative, given numerical limitations of the CDF alg).
                    //Since the SB calculation compares the chance that a variant is present on both strands vs the chance that it is present on exactly one strand* not on the other.When a forced allele is very low frequency, the chance that it is one both strands AND the chance that its on exactly one strand are BOTH essentially zero, and the algorithm falls apart.
                }
            }

            //Note:
            //
            // Type 1 error is when we rejected the null hypothesis when we should not have. (we have noise, but called a SNP)
            // Type 2 error is when we accepected the alternate when we should not have. (we have a variant, but we did not call it.)
            //
            // Type 1 error is our this.ChanceFalsePos aka p-value.
            // Type 2 error is out this.ChanceFalseNeg
        }
    }
}
