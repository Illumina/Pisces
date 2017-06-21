using System;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Calculators
{
	public class HaploidGenotypeQualityCalculator
	{
		/// <summary>
		///     Assign a q-score for a genotype call.
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
			float expectedHetFreq = 0.40f;  //a real 50% typically shows up at <50%, more like 40% or 45%
			float depth = (float)allele.TotalCoverage;

			//distributions
			var poissonHomRefNoise = new MathNet.Numerics.Distributions.Poisson(noiseHomRef * depth);
			var poissonHomAltNoise = new MathNet.Numerics.Distributions.Poisson(noiseHomAlt * depth);
			var binomialHomAltExpected = new MathNet.Numerics.Distributions.Binomial(expectedHetFreq, allele.TotalCoverage);
			var nonAlleleCalls = Math.Max(allele.TotalCoverage - allele.AlleleSupport, 0);  //sanitize for funny insertion cases

			double LnPofH0GT = 0;  //H0 is the null hypothesis. The working assumption that the GT given to the allele is correct
			double LnPofH1GT = 0;  //H1 is the alternate hypothesis. The possibility that H0 is wrong, and the second-best GT was actually the right one


			//the GT Q model measures how much *more* likely H0 is than H1, given the observations.

			switch (calledGT)
			{

				case Genotype.HemizygousRef:
					LnPofH0GT = poissonHomRefNoise.ProbabilityLn(nonAlleleCalls);
					LnPofH1GT = binomialHomAltExpected.ProbabilityLn(nonAlleleCalls);
					break;

				case Genotype.HemizygousAlt:
					LnPofH0GT = poissonHomAltNoise.ProbabilityLn(nonAlleleCalls);
					LnPofH1GT = binomialHomAltExpected.ProbabilityLn(allele.AlleleSupport);
					break;

	
				default:
					return minQScore;

			}

			var qScore = (int)Math.Floor(10.0 * Math.Log10(Math.E) * (LnPofH0GT - LnPofH1GT));

			return Math.Max(Math.Min(qScore, maxQScore), minQScore);
		}
	}
}