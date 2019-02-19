using System.Collections.Generic;
using Gemini.Types;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;

namespace Gemini.Interfaces
{
    public interface IReadRealigner
    {
        RealignmentResult Realign(Read read, List<HashableIndel> allTargets,
            Dictionary<HashableIndel, GenomeSnippet> indelContexts,
            IIndelRanker ranker, bool pairSpecific, int maxIndelSize = 50);

    }
}