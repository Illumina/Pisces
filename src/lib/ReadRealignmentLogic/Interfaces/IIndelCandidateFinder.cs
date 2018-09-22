using System.Collections.Generic;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;

namespace ReadRealignmentLogic.Interfaces
{
    public interface IIndelCandidateFinder
    {
        List<CandidateIndel> FindIndels(Read read, string refChromosome, string chromosomeName);
    }
}
