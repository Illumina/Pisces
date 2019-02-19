using System.Collections.Generic;
using Alignment.IO;
using Gemini.Realignment;
using StitchingLogic;

namespace Gemini.Interfaces
{
    public interface IBamRealignmentFactory
    {
        IReadPairHandler GetRealignPairHandler(bool tryRestitch, bool alreadyStitched, bool pairAwareRealign,
            Dictionary<int, string> refIdMapping, ReadStatusCounter statusCounter, bool isSnowball,
            IChromosomeIndelSource indelSource, string chromosome, Dictionary<string, int[]> masterLookup);
    }
}