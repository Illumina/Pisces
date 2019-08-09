using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Gemini.Interfaces;
using Gemini.Models;

namespace Gemini.Realignment
{
    public class PairSpecificIndelFinder : IPairSpecificIndelFinder
    {
        public List<PreIndel> GetPairSpecificIndels(ReadPair readpair, List<PreIndel> r1Indels, List<PreIndel> r2Indels, ref int? r1Nm, ref int? r2Nm)
        {
            return r1Indels.Concat(r2Indels).ToList();
        }
    }
}