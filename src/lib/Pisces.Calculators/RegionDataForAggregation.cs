using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Gemini.BinSignalCollection;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.Types;

namespace Gemini
{
    public class RegionDataForAggregation
    {
        public IBinEvidence BinEvidence;
        public ConcurrentDictionary<PairClassification, List<PairResult>> PairResultLookup;
        public EdgeState EdgeState;
        public int EffectiveMaxPosition;
        public int EffectiveMinPosition;
        public List<Tuple<int, string, int, bool>> ConsistentSoftclips;
    }
}