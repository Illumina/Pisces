using System;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Calculators
{
    public static class VariantQualityCalculator
    {
        /// <summary>
        ///     Assign a q-score to a SNP, given (CallCount / Coverage) frequency.
        /// </summary>
        public static void Compute(CalledAllele allele, int maxQScore, int estimatedBaseCallQuality)
        {
            allele.NoiseLevelApplied = estimatedBaseCallQuality;

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

        // var poissonDist = new MathNet.Numerics.Distributions.Poisson(errorRate * coverage);
        public static double AssignRawPoissonQScore(int callCount, int coverage, int estimatedBaseCallQuality)
        // ReSharper restore InconsistentNaming
        {
            
            double errorRate = MathOperations.QtoP(estimatedBaseCallQuality);
            double callCountMinusOne = callCount - 1;
            double callCountDouble = callCount;

            var lambda = errorRate * coverage;
            var poissonDist = new MathNet.Numerics.Distributions.Poisson(lambda);

            var pValue = 1 - poissonDist.CumulativeDistribution(callCountMinusOne);

            if (pValue > 0)
            {
               return MathOperations.PtoQ(pValue);
            }
            else
            {
                //Approximation to get around precision issues.
                double A = poissonDist.ProbabilityLn((int)callCountMinusOne);
                double correction = (callCountDouble - lambda ) / callCountDouble;              
                var qScore = -10.0 * (A - Math.Log(2.0 * correction)) / Math.Log(10.0); 
                return qScore;
            }
        }

        public static int AssignPoissonQScore(int callCount, int coverage, int estimatedBaseCallQuality, int maxQScore)
        // ReSharper restore InconsistentNaming
        {
            if ((callCount <= 0) || (coverage <=0))
                return 0;

            var rawQ = AssignRawPoissonQScore(callCount, coverage, estimatedBaseCallQuality);
            var qScore = Math.Min(maxQScore, rawQ);
             qScore = Math.Max(qScore, 0);
            var intQScore = (int)Math.Round(qScore);
            return intQScore;
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
