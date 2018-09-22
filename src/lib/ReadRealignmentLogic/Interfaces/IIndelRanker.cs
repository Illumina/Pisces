using System.Collections.Generic;
using ReadRealignmentLogic.Models;

namespace ReadRealignmentLogic.Interfaces
{
    public interface IIndelRanker
    {
        void Rank(List<CandidateIndel> candidateIndels);
    }
}
