using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Gemini.Types;

namespace Gemini.Interfaces
{
    public interface IReadPairClassifierAndExtractor
    {
        List<BamAlignment> GetBamAlignmentsAndClassification(ReadPair readPair, IReadPairHandler pairHandler, out PairClassification classification, out bool hasIndels, out int numMismatchesInSingleton, out bool isSplit);
    }
}