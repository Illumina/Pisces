using System;
using System.Collections.Generic;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Genotyping
{
    /// <summary>
    /// AdaptiveGenotyperQualityCalculator class is the basic data structure containing relevant values for recalibration of Q scores
    /// For the standalone Adaptive GT program, this class is mainly used as a data structure to store relevant values.  However,
    /// it provides many functions that are called by the Pisces Variant Caller when calculating Q scores when using the
    /// binomial mixture model.
    /// </summary>
    public static class AdaptiveGenotyperCalculator
    {
        public static int MaxEffectiveDepth { get; } = 1000;

        public static SimplifiedDiploidGenotype GetSimplifiedGenotype(CalledAllele allele, double[] model, double[] priors)
        {
            var (alleleSupport, totalDepth) = PreprocessCalledAllele(allele);
            return MixtureModel.GetSimplifiedGenotype(alleleSupport, totalDepth, model, priors);
        }

        /// <summary>
        /// Calculates Q score of a called allele.
        /// </summary>
        /// <param name="allele"> Called allele from Pisces variant caller. </param>
        /// <param name="model"> Means of binomial model to be used to estimate Q score.  If this has more than three elements, 
        /// the elements closest to 0, 0.5 and 1 are used as the alleles true category.  </param>
        /// <param name="priors"> Prior probability for that category.  The length must match the length of model.  </param>
        /// <returns> RecalibratedVariant that holds the Q scores and posteriors of the allele.  </returns>
        public static MixtureModelResult GetGenotypeAndQScore(CalledAllele allele, double[] model, double[] priors)
        {
            var (alleleSupport, totalDepth) = PreprocessCalledAllele(allele);
            return MixtureModel.CalculateQScoreAndGenotypePosteriors(alleleSupport, totalDepth, model, priors);
        }

        /// <summary>
        /// Calculates the Q score and genotype posteriors of a 1/2 locus using a multinomial distribution.
        /// This method is called from Pisces variant caller.
        /// </summary>
        /// <param name="allele1"></param>
        /// <param name="allele2"></param>
        /// <param name="models">IList of models for allele1 and allele 2.  Each model is a 3 element double array.</param>
        /// <returns>RecalibratedVariant that contains the Q score and genotype posteriors.</returns>
        public static MixtureModelResult GetMultiAllelicQScores(CalledAllele allele1, CalledAllele allele2,
            IList<double[]> models)
        {
            int totalCoverage = allele1.TotalCoverage;
            int[] alleleDepths = new int[3];
            alleleDepths[2] = allele2.AlleleSupport;
            alleleDepths[1] = allele1.AlleleSupport;

            // "Reference" here is not really reference--it is just everything else that was not the top two alleles
            alleleDepths[0] = Math.Max(totalCoverage - alleleDepths[1] - alleleDepths[2], 0);

            return MixtureModel.GetMultinomialQScores(alleleDepths, totalCoverage, models);
        }

        public static (int alleleDepth, int totalDepth) DownsampleVariant(int ad, int dp)
        {
            return ((int)((double)ad / dp * MaxEffectiveDepth), MaxEffectiveDepth);
        }

        private static (int alleleDepth, int totalDepth) PreprocessCalledAllele(CalledAllele allele)
        {
            int dp = allele.TotalCoverage;
            int ad;
            if (allele.ReferenceAllele != allele.AlternateAllele)
                ad = allele.AlleleSupport;
            else
                ad = Math.Max(dp - allele.AlleleSupport, 0);

            // Downsample if necessary
            if (dp > MaxEffectiveDepth)
                (ad, dp) = DownsampleVariant(ad, dp);

            if (ad > dp)
                ad = dp;

            return (ad, dp);
        }
    }
}
