using System.Collections.Generic;

namespace Gemini.Interfaces
{
    public interface ISamtoolsWrapper
    {
        void SamtoolsSort(string originalBam, string sortedBam, int samtoolsThreadCount,
            int samtoolsMemoryMbPerThread, bool byName = false);

        void SamtoolsCat(string mergedBam, IEnumerable<string> inputBams);
        void SamtoolsIndex(string bamToIndex);
    }
}