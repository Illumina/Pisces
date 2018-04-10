using System.Collections.Generic;
using RealignIndels.Models;

namespace RealignIndels.Interfaces
{
    public interface IIndelRanker
    {
        void Rank(List<CandidateIndel> candidateIndels);
    }
}
