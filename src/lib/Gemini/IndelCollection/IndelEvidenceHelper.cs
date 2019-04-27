using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Gemini.Models;
using Pisces.Domain.Types;

namespace Gemini.IndelCollection
{
    public enum Outcome
    {
        Kept,
        LowObservations,
        LowReputableSupport,
        SuperWeakSmall,
        Filtered,
        Rescued,
        BelowThreshold,
        PoorSingle,
        PoorEdge
    }
    public class IndelEvidence
    {
        public int LeftAnchor;
        public int RightAnchor;
        public int Quality;
        public int Observations;
        public int Stitched;
        public int Forward;
        public int Reverse;
        public int ReputableSupport;
        public int IsRepeat;
        public int IsSplit;
        public int Mess;
        public Outcome Outcome;
        public int Position; // TODO this is the wrong place for this but i need it for now

        public void AddIndelEvidence(IndelEvidence other)
        {
            Stitched += other.Stitched;
            Forward += other.Forward;
            Reverse += other.Reverse;
            Observations += other.Observations;
            Quality += other.Quality;
            Mess += other.Mess;
            LeftAnchor += other.LeftAnchor;
            RightAnchor += other.RightAnchor;
            IsRepeat += other.IsRepeat;
            ReputableSupport += other.ReputableSupport;
            IsSplit += other.IsSplit;
            Position = other.Position;
        }

        public override string ToString()
        {
            return string.Join(",",
                new int[]
                {
                    Observations, LeftAnchor, RightAnchor, Mess, Quality, Forward, Reverse, Stitched, ReputableSupport,
                    IsRepeat, IsSplit
                }) + "," + Outcome.ToString();
        }
    }


    public static class IndelEvidenceHelper
    {
        public static List<PreIndel> FindIndelsAndRecordEvidence(BamAlignment bamAlignment, IndelTargetFinder targetFinder, Dictionary<string, IndelEvidence> lookup, 
            bool isReputable, string chrom, int minMapQuality, bool stitched = false)
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

                    var isMulti = indels.Count() > 1;
                    int readSpanNeededToCoverBoth = 0;
                    if (isMulti)
                    {
                        var firstPosOfVariation = indels[0].ReferencePosition;
                        var lastIndel = indels[indels.Count - 1];
                        var lastPosOfVariation = lastIndel.Type == AlleleCategory.Deletion
                                                     ? lastIndel.ReferencePosition + 1
                                                     : lastIndel.ReferencePosition + lastIndel.Length;
                        readSpanNeededToCoverBoth = lastPosOfVariation - firstPosOfVariation;
                    }

                 

                    // TODO do we want to collect info here for individual indels if they are only seen in multis?
                    // Currently trying to solve this by only collecting for individuals if it seems likely that we're going to see reads that don't span both
                    if (!isMulti || (readSpanNeededToCoverBoth > 25)) // TODO magic number
                    {
                        foreach (var indel in indels)
                        {
                            var indelKey = indel.ToString();

                            // TODO less gnarly

                            var indelMetrics = IndelMetrics(lookup, indelKey);

                            UpdateIndelMetrics(bamAlignment, isReputable, stitched, indelMetrics, indel, totalNm);
                        }
                    }

                    if (isMulti)
                    {
                        var indelKey = string.Join("|", indels.Select(x => x.ToString()));
                        // TODO less gnarly

                        var indelMetrics = IndelMetrics(lookup, indelKey);

                        // TODO - are read-level repeats that informative? Because this is kind of a perf burden
                        // (^ Removed for now for that reason)
                        bool isRepeat = false;
                        //var isRepeat = StitchingLogic.OverlapEvaluator.IsRepeat(bamAlignment.Bases.Substring(0, (int)indels[0].LeftAnchor), 2, out repeatUnit) || StitchingLogic.OverlapEvaluator.IsRepeat(bamAlignment.Bases.Substring(0, (int)indels[1].RightAnchor), 2, out repeatUnit);

                        AddReadLevelIndelMetrics(bamAlignment, isReputable, stitched, indelMetrics, isRepeat);
                        AddMultiIndelMetrics(indelMetrics, indels, totalNm);

                    }
                }

                return indels;
            }
            return null;
        }

        private static void UpdateIndelMetrics(BamAlignment bamAlignment, bool isReputable, bool stitched, IndelEvidence indelMetrics,
            PreIndel indel, int totalNm)
        {
            // TODO - are read-level repeats that informative? Because this is kind of a perf burden
            // (^ Removed for now for that reason)
            bool isRepeat = false;
            //var isRepeat = StitchingLogic.OverlapEvaluator.IsRepeat(bamAlignment.Bases.Substring(0, (int)indel.LeftAnchor), 2, out repeatUnit) || StitchingLogic.OverlapEvaluator.IsRepeat(bamAlignment.Bases.Substring(0, (int)indel.RightAnchor), 2, out repeatUnit);

            AddReadLevelIndelMetrics(bamAlignment, isReputable, stitched, indelMetrics, isRepeat);
            AddIndelMetrics(indelMetrics, indel, totalNm);
        }

        private static IndelEvidence IndelMetrics(Dictionary<string, IndelEvidence> lookup, string indelKey)
        {
            if (!lookup.TryGetValue(indelKey, out var indelMetrics))
            {
                //indelMetrics = new int[11];
                indelMetrics = new IndelEvidence();
                lookup.Add(indelKey, indelMetrics);
            }

            return indelMetrics;
        }

        private static void AddMultiIndelMetrics(IndelEvidence indelMetrics, List<PreIndel> indels, int totalNm)
        {
            indelMetrics.LeftAnchor += indels[0].LeftAnchor;
            indelMetrics.RightAnchor += indels[1].RightAnchor;
            indelMetrics.Mess += Math.Max(0, totalNm - indels.Sum(x => x.Length));
            indelMetrics.Quality += indels.Min(x => x.AverageQualityRounded);
        }

        private static void AddIndelMetrics(IndelEvidence indelMetrics, PreIndel indel, int totalNm)
        {
            indelMetrics.Position = indel.ReferencePosition;
            indelMetrics.LeftAnchor += indel.LeftAnchor;
            indelMetrics.RightAnchor += indel.RightAnchor;
            indelMetrics.Mess += Math.Max(0, totalNm - indel.Length);
            indelMetrics.Quality += indel.AverageQualityRounded;
        }

        private static void AddReadLevelIndelMetrics(BamAlignment bamAlignment, bool isReputable, bool stitched, IndelEvidence indelMetrics,
            bool isRepeat)
        {
            indelMetrics.Observations++;
            if (stitched)
            {
                indelMetrics.Stitched++;
            }
            else
            {
                if (bamAlignment.IsReverseStrand())
                {
                    indelMetrics.Reverse++;
                }
                else
                {
                    indelMetrics.Forward++;
                }
            }

            if (isReputable)
            {
                indelMetrics.ReputableSupport++;
            }

            if (isRepeat)
            {
                indelMetrics.IsRepeat++;
            }

            if (!bamAlignment.IsMateMapped() || bamAlignment.MateRefID != bamAlignment.RefID)
            {
                indelMetrics.IsSplit++;
            }
        }
    }
}