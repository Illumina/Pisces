using System.Collections.Generic;
using Gemini.Types;

namespace Gemini.Interfaces
{
    public interface ICategorizedBamRealigner
    {
        void RealignAroundCandidateIndels(Dictionary<string, int[]> indelStringLookup, Dictionary<string, Dictionary<PairClassification, List<string>>> categorizedAlignments);
    }
}