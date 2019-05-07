using System.Collections.Generic;
using Gemini.IndelCollection;
using Gemini.Types;

namespace Gemini
{
    public class EvidenceAndClassificationResults
    {
        public Dictionary<string, IndelEvidence> IndelEvidence;
        public Dictionary<string, Dictionary<PairClassification, List<string>>> CategorizedBams;
    }
}