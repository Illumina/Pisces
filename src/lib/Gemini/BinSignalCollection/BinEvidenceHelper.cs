using System;
using System.Collections.Concurrent;
using Gemini.ClassificationAndEvidenceCollection;

namespace Gemini.BinSignalCollection
{
    public class BinEvidenceHelpers
    {
        public static void AddEvidence(PairResult pairResult, int siteWidth, int regionStart,
            ConcurrentDictionary<int, uint> allHits, ConcurrentDictionary<int, uint> probableTrueSnvRegionsLookup,
            bool isSingleMismatch, int numBins, int refId)
        {
            foreach (var aln in pairResult.Alignments)
            {
                if (aln.RefID != refId)
                {
                    continue;
                }

                var lastBinSpannedByRead = (aln.EndPosition - regionStart) / siteWidth;
                var firstBin = (aln.Position - regionStart) / siteWidth;

                for (int i = firstBin; i <= Math.Min(lastBinSpannedByRead, numBins - 1); i++)
                {
                    allHits[i]++;

                    if (isSingleMismatch)
                    {
                        probableTrueSnvRegionsLookup[i]++;
                    }
                }
            }
        }
    }
}