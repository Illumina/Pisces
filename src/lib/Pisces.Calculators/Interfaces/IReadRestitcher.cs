using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Gemini.Logic;

namespace Gemini.Interfaces
{
    public interface IReadRestitcher
    {
        List<BamAlignment> GetRestitchedReads(ReadPair pair, BamAlignment origRead1, BamAlignment origRead2, 
            int? r1Nm, int? r2Nm, bool realignedAroundPairSpecific, INmCalculator nmCalculator, bool recalculateNm, bool realignmentIsSketchy = false);
    }
}