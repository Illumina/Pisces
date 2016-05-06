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

        private SortedList<int, List<BaseCalledAllele>> CreateCalledSomaticVariants(
            IEnumerable<CandidateAllele> candidates)
        {
            var lookup = new SortedList<int, List<BaseCalledAllele>>();
            var calledVars = candidates.Select(x => new BaseCalledAllele
            {
                Alternate = x.Alternate,
                Reference = x.Reference,
                Chromosome = x.Chromosome,
                Coordinate = x.Coordinate,
                FractionNoCalls = .1f,
                TotalCoverage = 10,
                Filters = new List<FilterType>(),
                AlleleSupport = x.Support,
                Genotype = Genotype.HeterozygousAltRef
            });

            foreach (var variant in calledVars)
            {
                if (lookup.ContainsKey(variant.Coordinate))
                    lookup[variant.Coordinate].Add(variant);
                else
                    lookup.Add(variant.Coordinate, new List<BaseCalledAllele>() {variant});
            }
            return lookup;
        }
    }
}
