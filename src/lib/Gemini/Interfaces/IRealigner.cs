using System.Collections.Generic;

namespace Gemini.Interfaces
{
    public interface IRealigner
    {
        void Execute(string inBam, string outBam, bool tryRestitch, bool alreadyStitched, bool pairAwareRealign);

        Dictionary<string, int[]> GetIndels();
    }
}