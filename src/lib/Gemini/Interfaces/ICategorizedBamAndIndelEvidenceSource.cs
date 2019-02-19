using System.Collections.Generic;
using Gemini.Types;

namespace Gemini.Interfaces
{
    public interface ICategorizedBamAndIndelEvidenceSource
    {
        Dictionary<string, Dictionary<PairClassification, List<string>>> GetCategorizedAlignments();

        Dictionary<string, int[]> GetIndelStringLookup();
        void CollectAndCategorize(IGeminiDataSourceFactory dataSourceFactory, IGeminiDataOutputFactory dataOutputFactory);
    }
}