using System.Collections.Generic;
using Alignment.Domain;
using Gemini.Models;

namespace Gemini.Interfaces
{
    public interface IPairSpecificIndelFinder
    {
        List<PreIndel> GetPairSpecificIndels(ReadPair readpair,
            ref int? r1Nm, ref int? r2Nm);
    }
}