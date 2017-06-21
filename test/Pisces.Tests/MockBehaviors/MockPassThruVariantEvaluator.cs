using System.Collections.Generic;
using System.Linq;
using Moq;
using Pisces.Interfaces;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Processing.Models;

namespace Pisces.Tests.MockBehaviors
{
    public class AlleleCallerPassThrough : Mock<IAlleleCaller>
    {
        public AlleleCallerPassThrough()
        {
            Setup(c => c.Call(It.IsAny<CandidateBatch>(), It.IsAny<IAlleleSource>()))
                .Returns((IEnumerable<CandidateAllele> c, IAlleleSource s) => CreateCalledSomaticVariants(c));
        }

        private SortedList<int, List<CalledAllele>> CreateCalledSomaticVariants(
            IEnumerable<CandidateAllele> candidates)
        {
            var lookup = new SortedList<int, List<CalledAllele>>();
            var calledVars = candidates.Select(x => new CalledAllele
            {
                AlternateAllele = x.AlternateAllele,
                ReferenceAllele = x.ReferenceAllele,
                Chromosome = x.Chromosome,
                ReferencePosition = x.ReferencePosition,
                NumNoCalls = 1,
                //FractionNoCalls = .1f,
                TotalCoverage = 10,
                Filters = new List<FilterType>(),
                AlleleSupport = x.Support,
                Genotype = Genotype.HeterozygousAltRef
            });

            foreach (var variant in calledVars)
            {
                if (lookup.ContainsKey(variant.ReferencePosition))
                    lookup[variant.ReferencePosition].Add(variant);
                else
                    lookup.Add(variant.ReferencePosition, new List<CalledAllele>() {variant});
            }
            return lookup;
        }
    }
}
