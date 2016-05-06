using System;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Calculators
{
    public static class VariantQualityCalculator
    {
        /// <summary>
        ///     Assign a q-score to a SNP, given (CallCount / Coverage) frequency.
        /// </summary>
        public static void Compute(BaseCalledAllele allele, int maxQScore, int estimatedBaseCallQuality)
        {
            if (allele.TotalCoverage == 0)
            {
                allele.VariantQscore = 0;
            }
            else
            {
                allele.VariantQscore = AssignPoissonQScore(allele.AlleleSupport, allele.TotalCoverage, estimatedBaseCallQuality,
                    maxQScore);                
            }
        }

        public static int AssignPoissonQScore(int callCount, int coverage, int estimatedBaseCallQuality, int maxQScore)
        // ReSharper restore InconsistentNaming
        {
            var pValue = AssignPValue(callCount, coverage, estimatedBaseCallQuality);
            if (pValue <= 0) return maxQScore;
            var qScore = Math.Min(maxQScore, MathOperations.PtoQ(pValue));
            var intQScore = (int)Math.Round(qScore);
            return Math.Min(maxQScore, intQScore);
        }

        public static double AssignPValue(int observedCallCount, int coverage, int estimatedBaseCallQuality)
        {
            double errorRate = MathOperations.QtoP(estimatedBaseCallQuality);
            if (observedCallCount == 0)
                return 1.0;

            return (1 - Poisson.Cdf(observedCallCount - 1.0, coverage * errorRate));
        }
    }
}
