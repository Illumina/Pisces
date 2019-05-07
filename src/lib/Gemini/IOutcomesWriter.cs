using System.Collections.Concurrent;
using ReadRealignmentLogic.Models;

namespace Gemini
{
    public interface IOutcomesWriter
    {
        void WriteIndelOutcomesFile(ConcurrentDictionary<HashableIndel, int[]> masterOutcomesLookup);

        void CategorizeProgressTrackerAndWriteCategoryOutcomesFile(ConcurrentDictionary<string, int> progressTracker);

        void WriteIndelsFile(ConcurrentDictionary<HashableIndel, int> masterFinalIndels);
    }
}