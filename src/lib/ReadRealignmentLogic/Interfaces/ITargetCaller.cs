using System.Collections.Generic;
using Pisces.Domain.Interfaces;
using ReadRealignmentLogic.Models;

namespace ReadRealignmentLogic.Interfaces
{
    public interface ITargetCaller
    {
        List<CandidateIndel> Call(List<CandidateIndel> candidateIndels, IAlleleSource alleleSource);
    }
}
