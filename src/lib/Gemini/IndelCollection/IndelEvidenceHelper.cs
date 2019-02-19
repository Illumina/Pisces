using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;

namespace Gemini.IndelCollection
{
    public static class IndelEvidenceHelper
    {
        public static void FindIndelsAndRecordEvidence(BamAlignment bamAlignment, IndelTargetFinder targetFinder, Dictionary<string, int[]> lookup, bool isReputable, string chrom, int minMapQuality, bool stitched = false)
        {
            // TODO define whether we want to collect indels from supplementaries. I think we probably do...
            // TODO do we want to collect indels from duplicates? 
            // Was thinking this might be faster than checking all the ops on all the reads, we'll see - it also makes an important assumption that no reads are full I or full D
            if (bamAlignment.MapQuality > minMapQuality && bamAlignment.CigarData.Count > 1 &&
                bamAlignment.IsPrimaryAlignment())
            {
                var indels = targetFinder.FindIndels(bamAlignment, chrom);

                if (indels.Any())
                {
                    // TODO this doesn't support nm from stitched, which is not in a tag. Need to pass it in!!
                    var nm = bamAlignment.GetIntTag("NM");
                    var totalNm = nm ?? 0;

                    foreach (var indel in indels)
                    {
                        var indelKey = indel.ToString();
                        // TODO less gnarly

                        if (!lookup.ContainsKey(indelKey))
                        {
                            lookup.Add(indelKey, new int[9]);
                        }


                        lookup[indelKey][0]++;
                        lookup[indelKey][1] += (int)indel.LeftAnchor;
                        lookup[indelKey][2] += (int)indel.RightAnchor;
                        lookup[indelKey][3] += Math.Max(0, totalNm - indel.Length);
                        lookup[indelKey][4] += indel.AverageQualityRounded;
                        lookup[indelKey][stitched ? 7 : (bamAlignment.IsReverseStrand() ? 6 : 5)]++;

                        if (isReputable)
                        {
                            lookup[indelKey][8]++;
                        }
                    }

                    if (indels.Count() > 1)
                    {
                        var indelKey = string.Join("|", indels.Select(x => x.ToString()));
                        // TODO less gnarly

                        if (!lookup.ContainsKey(indelKey))
                        {
                            lookup.Add(indelKey, new int[9]);
                        }

                        lookup[indelKey][0]++;
                        lookup[indelKey][1] += (int)indels[0].LeftAnchor;
                        lookup[indelKey][2] += (int)indels[1].RightAnchor;
                        lookup[indelKey][3] += Math.Max(0, totalNm - indels.Sum(x => x.Length));
                        lookup[indelKey][4] += indels.Min(x => x.AverageQualityRounded);
                        lookup[indelKey][stitched ? 7 : (bamAlignment.IsReverseStrand() ? 6 : 5)]++;
                        if (isReputable)
                        {
                            lookup[indelKey][8]++;
                        }
                    }
                }
            }
        }
    }
}