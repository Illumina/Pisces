using System.Collections.Generic;
using Alignment.Domain;
using Gemini.Interfaces;
using Gemini.Models;

namespace Gemini.Realignment
{
    public class NonPairSpecificIndelFinder : IPairSpecificIndelFinder
    {
        public List<PreIndel> GetPairSpecificIndels(ReadPair readpair, List<PreIndel> r1Indels, List<PreIndel> r2Indels, ref int? r1Nm, ref int? r2Nm)
        {
            return null;
        }
    }
}