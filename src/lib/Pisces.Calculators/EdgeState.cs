using System.Collections.Generic;
using Gemini.BinSignalCollection;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.Types;
using ReadRealignmentLogic.Models;

namespace Gemini
{
    public class EdgeState
    {
        public Dictionary<PairClassification, List<PairResult>> EdgeAlignments =
            new Dictionary<PairClassification, List<PairResult>>();
        public List<HashableIndel> EdgeIndels = new List<HashableIndel>();
        public int EffectiveMinPosition;
        public string Name;
        public IBinEvidence BinEvidence;
    }
}