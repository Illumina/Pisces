using System.Collections.Generic;
using Gemini.Models;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;

namespace Gemini.CandidateIndelSelection
{
    public interface IHashableIndelSource
    {
        List<HashableIndel> GetFinalIndelsForChromosome(string chromosome, List<PreIndel> indelsForChrom, ChrReference chrReference = null);
    }
}