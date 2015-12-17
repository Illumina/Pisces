using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using Moq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Tests.MockBehaviors
{
    public class AlleleCallerPassThrough : Mock<IAlleleCaller>
    {
        public AlleleCallerPassThrough()
        {
            Setup(c => c.Call(It.IsAny<CandidateBatch>(), It.IsAny<IStateManager>()))
                .Returns((IEnumerable<CandidateAllele> c, IStateManager s) => CreateCalledSomaticVariants(c));
        }

        private IEnumerable<BaseCalledAllele> CreateCalledSomaticVariants(
            IEnumerable<CandidateAllele> candidates)
        {
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
                Genotype = Genotype.HeterozygousAlt
            });
            return calledVars;
        }
    }
}
