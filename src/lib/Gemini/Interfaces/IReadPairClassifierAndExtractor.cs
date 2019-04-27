using Alignment.Domain;
using Alignment.IO;
using Gemini.ClassificationAndEvidenceCollection;

namespace Gemini.Interfaces
{
    public interface IReadPairClassifierAndExtractor
    {
        PairResult GetBamAlignmentsAndClassification(ReadPair readPair, IReadPairHandler pairHandler);
    }
}