using System;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;

namespace CallSomaticVariants.Logic.Calculators
{
    public static class StrandBiasCalculator
    {
        public static void Compute(BaseCalledAllele variant, int[] supportByDirection, int qNoise, double acceptanceCriteria,
            StrandBiasModel strandBiasModel)
        {
            variant.StrandBiasResults = CalculateStrandBiasResults(variant.TotalCoverageByDirection, supportByDirection, qNoise, acceptanceCriteria, strandBiasModel);
        }

        /// <summary>
        ///     Assign a strandbias-score to a SNP.
        ///     (using only forward and reverse SNP counts.)
        /// </summary>
        private static StrandBiasResults CalculateStrandBiasResults(int[] coverageByStrandDirection,
            int[] supportByStrandDirection,
            int qNoise, double acceptanceCriteria, StrandBiasModel strandBiasModel)
        {
            var forwardSupport = supportByStrandDirection[(int)DirectionType.Forward];
            var forwardCoverage = coverageByStrandDirection[(int)DirectionType.Forward];
            var reverseSupport = supportByStrandDirection[(int)DirectionType.Reverse];
            var reverseCoverage = coverageByStrandDirection[(int)DirectionType.Reverse];
            var stitchedSupport = supportByStrandDirection[(int)DirectionType.Stitched];
            var stitchedCoverage = coverageByStrandDirection[(int)DirectionType.Stitched];

            var errorRate = Math.Pow(10, -1*qNoise/10f);

            var overallStats = new StrandBiasStats(forwardSupport + reverseSupport + stitchedSupport,
                forwardCoverage + reverseCoverage + stitchedCoverage, errorRate, errorRate, strandBiasModel);
            var forwardStats = new StrandBiasStats(forwardSupport + stitchedSupport / 2,
                forwardCoverage + stitchedCoverage / 2,
                errorRate, errorRate, strandBiasModel);
            var reverseStats = new StrandBiasStats(reverseSupport + stitchedSupport / 2,
                reverseCoverage + stitchedCoverage / 2,
                errorRate, errorRate, strandBiasModel);

            var results = new StrandBiasResults
            {
                ForwardStats = forwardStats,
                ReverseStats = reverseStats,
                OverallStats = overallStats
            };

            results.StitchedStats = new StrandBiasStats(stitchedSupport, stitchedCoverage, errorRate, errorRate,
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

            var testResults = MathOperations.GetTValue(forwardStats.Frequency, reverseStats.Frequency,
                forwardStats.Coverage,
                reverseStats.Coverage, acceptanceCriteria);

            results.TestScore = testResults[0];
            results.TestAcceptable = ValueAcceptable(acceptanceCriteria, testResults[0], testResults[1]);
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
            var p = Math.Max(forwardBias, reverseBias);

            return new[] { p, MathOperations.PtoGATKBiasScale(p) };
        }

        private static bool ValueAcceptable(double levelOfSignificance, double tvalue, double degreesOfFreedom)
        {
            var alphaOver2 = levelOfSignificance / 2.0;
            var rejectionRegion = 1.282;

            if (degreesOfFreedom < 30)
            {
                //throw new Exception("Its best if you go look this up in a table...");
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
    }
}
