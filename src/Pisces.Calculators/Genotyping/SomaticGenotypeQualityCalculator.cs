using System;
using System.Collections.Generic;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Calculators
{
    public class SomaticGenotypeQualityCalculator
    {
        public static int Compute(CalledAllele allele, float minVF, int minGTQScore, int maxGTQScore)
        {
            double rawQ = allele.VariantQscore;

            if (allele.TotalCoverage == 0)
                return 0;

            if ((allele.Genotype == Genotype.HomozygousRef) || (allele.Genotype == Genotype.HomozygousAlt))
            {
                //a homozygous somatic call GT is a fairly strong statement. It implies
                //A) we found the allele for sure (the VariantQscore)
                var p1 = MathOperations.QtoP(allele.VariantQscore);

                //and
                //B) the chance that we missed any alternate calls is very small. 
                // this would be the chance false negative given VF=min freq, and coverage is as given.
                
                var nonAlleleObservations = (1f - allele.Frequency) * allele.TotalCoverage;
                var expectedNonAllelObservations = Math.Floor( minVF * allele.TotalCoverage); //what we would see, if it was present

                //This takes care of the cases:
                //A) we dont have enough depth to ever observe any non-ref variant. If, if depth is 10, we would never see a 5% variant anyway.
                //B) if we see 6% not reference > 5% min safe var call freqeuncy, we are pretty worried about calling this as a 0/0 GT
                if (nonAlleleObservations >= expectedNonAllelObservations)
                    return minGTQScore;

                //var p2 = poissonDist.CumulativeDistribution(nonRefObservations); <- this method does badly for values lower than the mean
                var p2 = Poisson.Cdf(nonAlleleObservations, expectedNonAllelObservations);
                rawQ = MathOperations.PtoQ(p1 + p2);

            }

            var qScore = Math.Min(maxGTQScore, rawQ);
            qScore = Math.Max(qScore, minGTQScore);
            return (int)Math.Round(qScore);
        }
    }
}