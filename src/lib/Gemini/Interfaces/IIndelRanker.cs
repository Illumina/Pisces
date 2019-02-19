using System.Collections.Generic;
using Gemini.Models;

namespace Gemini.Interfaces
{
    public interface IIndelRanker
    {
        void Rank(List<PreIndel> candidateIndels);
    }
}