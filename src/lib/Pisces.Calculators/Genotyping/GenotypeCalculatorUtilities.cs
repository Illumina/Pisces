using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Calculators
{
	public class GenotypeCalculatorUtilities
	{
		public static List<CalledAllele> GetAllelesToPruneBasedOnGTCall(Genotype singleGTForLoci,
	List<CalledAllele> orderedVariants, List<CalledAllele> allelesToPrune)
		{
			int allowedNumVarAlleles = 0;

			switch (singleGTForLoci)
			{
				case Genotype.AltAndNoCall:
				case Genotype.AltLikeNoCall:
				case Genotype.HomozygousAlt:
				case Genotype.HeterozygousAltRef:
				case Genotype.HemizygousAlt:
					{
						allowedNumVarAlleles = 1;
						break;
					}
				case Genotype.Alt12LikeNoCall:
				case Genotype.HeterozygousAlt1Alt2:
					{
						allowedNumVarAlleles = 2;
						break;
					}
				default:
					{
						allowedNumVarAlleles = 0;
						break;
					}
			}

			for (int i = 0; i < orderedVariants.Count; i++)
			{
				if (i >= allowedNumVarAlleles)
					allelesToPrune.Add(orderedVariants[i]);

			}
			return allelesToPrune;
		}

		public static bool CheckForDepthIssue(IEnumerable<CalledAllele> alleles, int minDepthToEmit)
		{
			foreach (var allele in alleles)
			{
				if (allele.TotalCoverage < minDepthToEmit)
				{
					return true;
				}
			}
			return false;
		}

		public static List<CalledAllele> FilterAndOrderAllelesByFrequency(IEnumerable<CalledAllele> alleles, List<CalledAllele> allelesToPrune,
	double minFreqThreshold)
		{
			var variantAlleles = new List<CalledAllele>();
            var comparer = new AlleleCompareByLociAndAllele();

			foreach (var allele in alleles)
			{
				if (allele.Type != AlleleCategory.Reference)
				{
					if (allele.Frequency >= minFreqThreshold)
					{
						variantAlleles.Add(allele);
					}
					else
						allelesToPrune.Add(allele);
				}
			}

            variantAlleles = variantAlleles.OrderByDescending(p => p.Frequency).ThenBy(a => a, comparer).ToList();
            //variantAlleles = variantAlleles.OrderByDescending(p => p.Frequency).ToList();
            return variantAlleles;
		}


		public static double GetReferenceFrequency(IEnumerable<CalledAllele> alleles, double minorVF)
		{
			double altFrequencyCount = 0;
            double refFrequencyCountBySNP = 0;
            double indelFrequencyCount = 0;

            if (alleles.Count() == 0)
				return 0;

			if (alleles.Count() == 1)
				return alleles.First().RefFrequency;

			foreach (var allele in alleles)
			{

				if (allele.Type == AlleleCategory.Reference)
				{
					return allele.Frequency;
				}
				if (allele is CalledAllele)
				{
					altFrequencyCount += allele.Frequency;

                    //Note, we cannot just do this, below. Incase we have a 
                    // variant (a) 50 % SNP w/50% ref , but also a
                    //variant (b)  50 % indel, 50% not indel situation.

                    if (allele.Type == AlleleCategory.Snv)
                    {
                        refFrequencyCountBySNP = ((CalledAllele)allele).RefFrequency;
                    }
                    else //its an MNV or indel
                    {
                        indelFrequencyCount += ((CalledAllele)allele).Frequency;
                    }
				}
			}

            //We get here, we might all the calls are indels or MNVS, ie a 1/2 GT
            //in which case (since MNV and indel, the reference counts are just equal to ~not MNV or indel)
            // so the % ref boils down to our best estimate.

            //we know its got to be less that the MinorVF, and also less than the sum of the VF calls put together.

            //this is a bit of a hack for very rare cases. if we fixed the fact that the ref counts on indels
            //are not the true ref count, but average over a length of bases, counting the non-indel reads rather than the ref reads.
           
            double refFreqEstimate = Math.Max(refFrequencyCountBySNP - indelFrequencyCount, 0.0);
            return refFreqEstimate;
        }



	}
}