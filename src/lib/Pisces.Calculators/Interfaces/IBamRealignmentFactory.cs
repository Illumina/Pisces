using System.Collections.Generic;
using Gemini.IndelCollection;
using Gemini.Logic;
using Gemini.Realignment;
using ReadRealignmentLogic.Models;
using StitchingLogic;

namespace Gemini.Interfaces
{
    public interface IBamRealignmentFactory
    {
        ReadPairRealignerAndCombiner GetRealignPairHandler(bool tryRestitch, bool alreadyStitched,
            bool pairAwareRealign,
            Dictionary<int, string> refIdMapping, ReadStatusCounter statusCounter, bool isSnowball,
            IChromosomeIndelSource indelSource, string chromosome, Dictionary<string, IndelEvidence> masterLookup,
            bool hasIndels, Dictionary<HashableIndel, int[]> outcomesLookup, bool skipRestitchIfNothingChanged);
    }
}