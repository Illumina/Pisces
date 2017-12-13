using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Interfaces;

namespace Pisces.Logic.VariantCalling
{
    public class DiploidLocusProcessor : ILocusProcessor
    {


        public void Process(List<CalledAllele> calledAllelesInPosition)
        {

            var forcedAlleles = new List<CalledAllele>();
            var nonForcedAlleles = new List<CalledAllele>();
            foreach (var calledAllele in calledAllelesInPosition)
            {
                if (calledAllele.Filters.Contains(FilterType.ForcedReport))
                {
                    forcedAlleles.Add(calledAllele);
                }
                else
                {
                    nonForcedAlleles.Add(calledAllele);
                }
            }

            if (!forcedAlleles.Any()) return;


            var isRef = nonForcedAlleles.Any(v => v.IsRefType);
            var isNoCall = !nonForcedAlleles.Any() || nonForcedAlleles.Any(v => v.IsNocall);

            var genotype = isNoCall
                ? Genotype.AltLikeNoCall
                : (isRef ? Genotype.HomozygousRef : Genotype.Others);

            foreach (var forcedAllele in forcedAlleles)
            {
                forcedAllele.Genotype = genotype;
            }


            var minGenotypeQscore = !nonForcedAlleles.Any() ? 0 : nonForcedAlleles.Min(v => v.GenotypeQscore);
            foreach (var calledAllele in calledAllelesInPosition)
            {
                calledAllele.GenotypeQscore = minGenotypeQscore;
            }
        }

        


      
    }
}