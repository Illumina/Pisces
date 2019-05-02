using System.Collections.Concurrent;
using System.Collections.Generic;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Models;
using Gemini.Types;

namespace Gemini
{
    public class IndelEvidenceCollector
    {
        private static bool ClassificationContainsQualityIndels(PairClassification classification)
        {
            // TODO should this have the other indel read types?
            return classification == PairClassification.Disagree ||
                   classification == PairClassification.IndelSingleton ||
                   classification == PairClassification.IndelUnstitchable ||
                   classification == PairClassification.UnstitchIndel;
        }

        public static PairResult[] CollectIndelEvidence(IndelTargetFinder targetFinder, string chrom,
            ConcurrentDictionary<string, IndelEvidence> indelLookup, PairResult[] pairs)
        {
            var localResult = new Dictionary<string, IndelEvidence>();

            foreach (var p in pairs)
            {
                var MinMapQualityForEvidence = 10;
                var pClassification = p.Classification;
                if (p.HasIndels)
                {
                    // Reputable indel reads will not have lots of mismatches or softclips

                    p.OriginalIndelsR1 = new List<PreIndel>();
                    p.OriginalIndelsR2 = new List<PreIndel>();

                    var stitched = pClassification == PairClassification.ImperfectStitched ||
                                   pClassification == PairClassification.MessyStitched ||
                                   pClassification == PairClassification.PerfectStitched ||
                                   pClassification == PairClassification.SingleMismatchStitched;

                    // Assumes we're not ever dealing with stitched reads here
                    var aln = p.ReadPair.Read1;

                    if (aln != null && aln.IsMapped() && aln.RefID >= 0)
                    {
                        // TODO would anything ever be coming through here that is _not_ ClassificationContainsQualityIndels?
                        if (ClassificationContainsQualityIndels(pClassification))
                        {
                            p.OriginalIndelsR1 = IndelEvidenceHelper.FindIndelsAndRecordEvidence(aln, targetFinder, localResult,
                                p.IsReputableIndelContaining, chrom, MinMapQualityForEvidence, stitched);
                        }
                    }

                    aln = p.ReadPair.Read2;
                    if (aln != null && aln.IsMapped() && aln.RefID >= 0)
                    {
                        // TODO would anything ever be coming through here that is _not_ ClassificationContainsQualityIndels?
                        if (ClassificationContainsQualityIndels(pClassification))
                        {
                            p.OriginalIndelsR2 = IndelEvidenceHelper.FindIndelsAndRecordEvidence(aln, targetFinder, localResult,
                                p.IsReputableIndelContaining, chrom, MinMapQualityForEvidence, stitched);
                        }
                    }
                }
            }

            foreach (var kvp in localResult)
            {
                indelLookup.AddOrUpdate(kvp.Key, kvp.Value, (k, v) =>
                {
                    v.AddIndelEvidence(kvp.Value);
                    return v;
                });
            }

            return pairs;
        }
    }
}