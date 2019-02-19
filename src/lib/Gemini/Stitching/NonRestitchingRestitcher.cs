using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Gemini.Interfaces;

namespace Gemini.Stitching
{
    public class NonRestitchingRestitcher : IReadRestitcher
    {
        public List<BamAlignment> GetRestitchedReads(ReadPair pair, BamAlignment origRead1, BamAlignment origRead2, int? r1Nm, int? r2Nm,
            bool realignedAroundPairSpecific)
        {
            return pair.GetAlignments().ToList();
        }
    }
}