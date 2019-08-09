using System;
using System.Collections.Generic;
using System.Linq;
using Common.IO.Utility;
using Gemini.Models;
using Gemini.Utility;
using Pisces.Domain.Types;

namespace Gemini.CandidateIndelSelection
{
    public class IndelPruner
    {
        private readonly bool _debug;
        private readonly int _binSize;

        public IndelPruner(bool debug, int binSize)
        {
            _debug = debug;
            _binSize = binSize;
        }


        private List<PreIndel> OrderIndelsByScore(IEnumerable<PreIndel> preIndels)
        {
            // Order by score descending, then arbitrarily by allele to make it deterministic
            return preIndels.OrderByDescending(x => x.Score).ThenByDescending(x => x.Length)
                .ThenBy(x => x.AlternateAllele).ThenBy(x => x.ReferenceAllele).ToList();
        }

        public List<PreIndel> GetPrunedPreIndelsForChromosome(List<PreIndel> unfilteredPreIndels)
        {
            const int minIndelLengthForCollapsing = 10;

            var indelsForChromRaw = unfilteredPreIndels;
            var indelsForChrom = new List<PreIndel>();
            var blacklistedIndels = new List<PreIndel>();

            foreach (var indel in OrderIndelsByScore(indelsForChromRaw))
            {
                bool addedAsConcurrent = false;
                // Collapse similar insertions into one

                if (indel.Length >= minIndelLengthForCollapsing && indel.Type == AlleleCategory.Insertion &&
                    !indel.InMulti)
                {
                    // Get other same-length insertions at this position and order by highest score. Low-scoring insertions that have a couple of mismatches from the top insertion can be assumed to be the same insertion and collapsed into it.
                    var concurrentIndels = indelsForChromRaw.Where(x =>
                            x.ReferencePosition == indel.ReferencePosition &&
                            x.AlternateAllele.Length == indel.AlternateAllele.Length && !x.InMulti)
                        .OrderByDescending(x => x.Score).ToList();

                    if (concurrentIndels.Count() > 2 && concurrentIndels.Max(x => x.Score) == indel.Score &&
                        concurrentIndels.Count(x => x.Score == indel.Score) == 1)
                    {
                        if (_debug)
                        {
                            Logger.WriteToLog(
                                $"Multiple concurrent indels!!! {(indel.ReferencePosition + ":" + indel.ReferenceAllele + ">" + indel.AlternateAllele + "(" + indel.Score + ")")} : {concurrentIndels.Count()}");
                        }

                        indelsForChrom.Add(indel);

                        addedAsConcurrent = true;

                        var indelsToBlacklist = concurrentIndels.Where(x =>
                            x.AlternateAllele != indel.AlternateAllele && !x.InMulti).ToList();

                        foreach (var concurrentIndel in indelsToBlacklist)
                        {
                            blacklistedIndels.Add(concurrentIndel);
                        }

                        var sumOfRemovedIndelScores = indelsToBlacklist.Sum(x => x.Score);
                        indel.Score += (sumOfRemovedIndelScores / 2); // TODO why did we divide this by two?

                        if (indelsToBlacklist.Any() && _debug)
                        {
                            Logger.WriteToLog(
                                $"Removed {indelsToBlacklist.Count()} extra variations of {indel} ({indel.Score}). Next highest score: {indelsToBlacklist.Max(x => x.Score)}");
                        }
                    }
                }

                if (_binSize > 0)
                {
                    PruneOverlappingIndels(indelsForChromRaw, indel, blacklistedIndels, _binSize);
                }

                if (!addedAsConcurrent)
                {
                    indelsForChrom.Add(indel);
                }
            }

            foreach (var candidateIndel in blacklistedIndels)
            {
                indelsForChrom.Remove(candidateIndel);
            }

            return indelsForChrom;
        }

        private static void PruneOverlappingIndels(List<PreIndel> indelsForChromRaw, PreIndel indel, List<PreIndel> blacklistedIndels, int binSize)
        {
            // Prune out stuff that would be overlapping or within buffer 
            var nearbyIndels = indelsForChromRaw.Where(x =>
                    !IndelsMatch(indel, x) && (Math.Abs(x.ReferencePosition - indel.ReferencePosition) <=
                                               binSize + (indel.Type == AlleleCategory.Deletion ? indel.Length : 0)
                    ))
                .ToList();

            // TODO consider making the threshold less for larger binsizes?
            var allScores = nearbyIndels.Select(x => x.Score).OrderByDescending(x => x);
            long sumOfScores = allScores.Sum() + indel.Score;
            if ((indel.Score / (float) sumOfScores) > 0.33)
            {
                var indelsToBlacklist = nearbyIndels.Where(x =>
                    !(
                        // Same allele
                        (x.ReferencePosition == indel.ReferencePosition &&
                         x.ReferenceAllele == indel.ReferenceAllele &&
                         x.AlternateAllele == indel.AlternateAllele)
                        ||
                        // Indel contained in multi other, and other is at least ok-ish quality
                        (!indel.InMulti && x.InMulti && Helper.MultiIndelContainsIndel(x, indel) &&
                         x.Score >= (indel.Score * 0.3))
                        ||
                        // Other contained in multi indel, and other is at least ok-ish quality
                        (indel.InMulti && !x.InMulti && Helper.MultiIndelContainsIndel(indel, x) &&
                         x.Score >= (indel.Score * 0.3))
                    )
                    && (
                        // (Much) lower scoring, shorter indel of the same type is likely to just be noise around this
                        // Note: this could be an issue with concurrent somatic and germline variants, ie if these two observed indels do _not_ represent the same biological event
                        x.Score < (indel.Score * 0.5) && x.Length <= indel.Length) && x.Type == indel.Type).ToList();

                foreach (var nearbyIndel in indelsToBlacklist)
                {
                    blacklistedIndels.Add(nearbyIndel);
                }
            }
        }

        private static bool IndelsMatch(PreIndel indel1, PreIndel indel2)
        {
            if (indel1.Chromosome == indel2.Chromosome &&
                indel1.ReferencePosition == indel2.ReferencePosition &&
                indel1.ReferenceAllele == indel2.ReferenceAllele && indel1.AlternateAllele == indel2.AlternateAllele)
            {
                return true;
            }

            return false;
        }
    }
}