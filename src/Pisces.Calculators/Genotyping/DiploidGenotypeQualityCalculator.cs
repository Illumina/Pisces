using System;
using System.Collections.Generic;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Models.Alleles;

// http://numerics.mathdotnet.com/Probability.html
// http://numerics.mathdotnet.com/License.html
// (MIT/X11)
using MathNet.Numerics;  

namespace Pisces.Calculators
{
    public class DiploidGenotypeQualityCalculator
    {
        /// <summary>
        ///     Assign a q-score for a genotoype call.
        /// </summary>
        public static int Compute(CalledAllele allele, int minQScore, int maxQScore)
        {

            if (allele.TotalCoverage == 0)
                return minQScore;

            Genotype calledGT = allele.Genotype;
            
            //parameters
            float noiseHomRef = 0.05f;
            float noiseHomAlt = 0.075f;
            float noiseHetAlt = 0.10f;
            float expectedHetFreq = 0.40f;  //a reaf 50% typically shows up at <50%, more like 40% or 45%
            float depth = (float) allele.TotalCoverage;
            float support = (float)allele.AlleleSupport;

            //distributions
            var poissonHomRefNoise = new MathNet.Numerics.Distributions.Poisson(noiseHomRef*depth);
            var poissonHomAltNoise = new MathNet.Numerics.Distributions.Poisson(noiseHomAlt*depth);
            var binomialHomAltExpected = new MathNet.Numerics.Distributions.Binomial(expectedHetFreq, allele.TotalCoverage);
            var binomialHomRefNoise = new MathNet.Numerics.Distributions.Binomial(noiseHetAlt, allele.TotalCoverage);
            var binomialHomAltNoise = new MathNet.Numerics.Distributions.Binomial((1-noiseHetAlt), allele.TotalCoverage);
            var nonAlleleCalls = Math.Max(allele.TotalCoverage - allele.AlleleSupport, 0);  //sanitize for funny insertion cases

            double LnPofH0GT = 0;  //H0 is the null hypothesis. The working assumption that the GT given to the allele is correct
            double LnPofH1GT = 0;  //H1 is the alternate hypothesis. The possibiilty that H0 is wrong, and the second-best GT was actaully the right one
  

            //the GT Q model measures how much *more* likely H0 is than H1, given the observations.

            switch (calledGT)
            {

                case Genotype.HomozygousRef:             
                    LnPofH0GT = poissonHomRefNoise.ProbabilityLn(nonAlleleCalls);
                    LnPofH1GT = binomialHomAltExpected.ProbabilityLn(nonAlleleCalls);
                    break;

                case Genotype.HomozygousAlt:
                    LnPofH0GT = poissonHomAltNoise.ProbabilityLn(nonAlleleCalls);
                    LnPofH1GT = binomialHomAltExpected.ProbabilityLn(allele.AlleleSupport);
                    break;

                case Genotype.HeterozygousAlt1Alt2:
                case Genotype.HeterozygousAltRef:
                    if (allele.Frequency >= 0.50)
                    {
                        //test alternate GT as being homAlt
                        LnPofH0GT = binomialHomAltExpected.ProbabilityLn((int)(depth * allele.Frequency));
                        LnPofH1GT = binomialHomAltNoise.ProbabilityLn((int)(depth * allele.Frequency));
                    }
                    else
                    {   //test alternate GT as being homRef
                        LnPofH0GT = binomialHomAltExpected.ProbabilityLn((int)(depth * allele.Frequency));
                        LnPofH1GT = binomialHomRefNoise.ProbabilityLn((int)(depth * allele.Frequency));
                    }
                    break;

                default:
                    return minQScore;

            }

            //note, Ln(X)=Log10 (X) / Log10 (e).
            // ->
            //Log10(A)-Log10(B) = Log10 (e) (ln (A) - ln (B)) = Log10(A/B) 

            /* for debugging..          
            var LogPofCalledGT = Math.Log10(Math.E) * (LnPofCalledGT);
            var LogPofAltGT = Math.Log10(Math.E) * (LnPofAltGT);
            Console.WriteLine(LogPofCalledGT);
            Console.WriteLine(LogPofAltGT);
            */

            var qScore = (int)Math.Floor(10.0 * Math.Log10(Math.E) * (LnPofH0GT - LnPofH1GT));

            return Math.Max( Math.Min(qScore,maxQScore),minQScore);
        }
   }
}
