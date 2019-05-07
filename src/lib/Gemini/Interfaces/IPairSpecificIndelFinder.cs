using System.Collections.Generic;
using Alignment.Domain;
using Gemini.Models;

namespace Gemini.Interfaces
{
    public class DummyRegionFilterer : IRegionFilterer
    {
        public bool AnyIndelsNearby(int startPosition)
        {
            return true;
        }
    }
    public interface IRegionFilterer
    {
        bool AnyIndelsNearby(int startPosition);
    }

    public interface IPairSpecificIndelFinder
    {
        List<PreIndel> GetPairSpecificIndels(ReadPair readpair, List<PreIndel> r1Indels, List<PreIndel> r2Indels,
            ref int? r1Nm, ref int? r2Nm);
    }
}