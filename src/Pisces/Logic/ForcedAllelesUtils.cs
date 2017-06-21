using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Logic
{
	public class ForcedAllelesUtils
	{
		public static void AddForcedAllelesToCalledAlleles(int pos, List<CalledAllele> calledAllelesInPos, List<Tuple<string, string>> forcedAllelesInPos, string chr)
		{
			int depth = 0;
			int? refDepth = null;
			int altDepth = 0;
			foreach (var calledAllele in calledAllelesInPos)
			{
				var allele = new Tuple<string, string>(calledAllele.ReferenceAllele, calledAllele.AlternateAllele);
				if (forcedAllelesInPos.Contains(allele))
				{
					forcedAllelesInPos.Remove(allele);
					if (calledAllele.IsForcedToReport && calledAllelesInPos.Count > 1)
					{
						calledAllele.Genotype = Genotype.Others;
					}

				}
				depth = Math.Max(depth, calledAllele.TotalCoverage);
				if (calledAllele.Genotype == Genotype.HomozygousRef)
				{
					refDepth = calledAllele.AlleleSupport;
				}
				else
				{
					altDepth += calledAllele.AlleleSupport;
				}

			}
			refDepth = refDepth ?? depth - altDepth;
		    var isRefSite = calledAllelesInPos.Count == 1 && calledAllelesInPos.First().Genotype == Genotype.HomozygousRef;
		    var isNoCallSite =
		        calledAllelesInPos.Any(
		            x =>
		                x.Genotype == Genotype.Alt12LikeNoCall || x.Genotype == Genotype.AltAndNoCall ||
		                x.Genotype == Genotype.AltLikeNoCall || x.Genotype == Genotype.RefLikeNoCall || x.Genotype == Genotype.RefAndNoCall);



            foreach (var forcedAllele in forcedAllelesInPos)
			{				
				calledAllelesInPos.Add(CreateForcedCalledAllele(chr, pos, forcedAllele.Item1, forcedAllele.Item2, depth, refDepth.Value, isRefSite, isNoCallSite));
			}


		}

		private static CalledAllele CreateForcedCalledAllele(string chr, int pos, string refAllele, string altAllele, int depth, int refSupport, bool isRefSite, bool isNoCallSite)
		{


			var forcedCallAllele = new CalledAllele(AlleleCategory.NonReference)
			{
				ReferencePosition = pos,
				Chromosome = chr,
				GenotypeQscore = 0,
				ReferenceAllele = refAllele,
				AlternateAllele = altAllele,
				TotalCoverage = depth,
				AlleleSupport = 0,
				ReferenceSupport = refSupport,
				IsForcedToReport = true

			};

			if (isRefSite)
			{
				forcedCallAllele.Genotype = Genotype.HomozygousRef;
			}
			else if (isNoCallSite)
			{
				forcedCallAllele.Genotype = Genotype.AltLikeNoCall;
			}
			else
			{
				forcedCallAllele.Genotype = Genotype.Others;
			}

			forcedCallAllele.AddFilter(FilterType.ForcedReport);


			return forcedCallAllele;
		}
	}
}