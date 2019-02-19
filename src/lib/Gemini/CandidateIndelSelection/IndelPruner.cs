using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public List<PreIndel> GetPrunedPreIndelsForChromosome(List<PreIndel> unfilteredPreIndels)
        {
            // Initialize collections
            var indelsForChromRaw = unfilteredPreIndels;

            // Prune the indels
            var indelsForChrom = new List<PreIndel>();
            var blacklistedIndels = new List<PreIndel>();
            var collapseSimilarInsertions = true;
            if (true)
            {
                foreach (var indel in indelsForChromRaw)
                {

                    bool addedAsConcurrent = false;
                    if (collapseSimilarInsertions)
                    {
                        if (indel.Length >= 10 && indel.Type == AlleleCategory.Insertion && !indel.InMulti)
                        {
                            var concurrentIndels = indelsForChromRaw.Where(x =>
                                    x.ReferencePosition == indel.ReferencePosition &&
                                    x.AlternateAllele.Length == indel.AlternateAllele.Length && !x.InMulti)
                                .OrderByDescending(x => x.Score).ToList();

                            if (concurrentIndels.Count() > 2 && concurrentIndels.Max(x => x.Score) == indel.Score &&
                                concurrentIndels.Count(x => x.Score == indel.Score) == 1)
                            {
                                Logger.WriteToLog(
                                    $"Multiple concurrent indels!!! {(indel.ReferencePosition + ":" + indel.ReferenceAllele + ">" + indel.AlternateAllele + "(" + indel.Score + ")")} : {concurrentIndels.Count()}");

                                // TODO (Cleanup) remove consensus logic, we're not doing it anymore - instead just taking the best.
                                // TODO (Improve) maybe be stricter on how much better the best has to be to do this?
                                // TODO (Improve) maybe check that a particular insertion is only <= n bp off from the confident one before removing it?
                                var consensusIndel = new StringBuilder();

                                for (int i = 0; i < indel.AlternateAllele.Length; i++)
                                {
                                    var bases = new List<char>();
                                    foreach (var concurrentIndel in concurrentIndels)
                                    {
                                        bases.Add(concurrentIndel.AlternateAllele[i]);
                                    }

                                    var distincted = bases.Distinct();
                                    if (distincted.Count() > 1)
                                    {
                                        consensusIndel.Append('N');
                                    }
                                    else
                                    {
                                        consensusIndel.Append(distincted.First());
                                    }
                                }

                                if (consensusIndel.ToString().Length != indel.AlternateAllele.Length)
                                {
                                    throw new Exception("This shouldn't happen");
                                }

                                if (consensusIndel.ToString() == indel.AlternateAllele)
                                {
                                    throw new Exception("Why are these the same");
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
                                indel.Score += (sumOfRemovedIndelScores / 2);

                                if (indelsToBlacklist.Any() && _debug)
                                {
                                    Logger.WriteToLog(
                                        $"Removed {indelsToBlacklist.Count()} extra variations of {indel} ({indel.Score}). Next highest score: {indelsToBlacklist.Max(x => x.Score)}");

                                }
                            }
                        }
                    }

                    if (_binSize > 0)
                    {

                        var nearbyIndels = indelsForChromRaw.Where(x =>
                                !IndelsMatch(indel, x) && (Math.Abs(x.ReferencePosition - indel.ReferencePosition) <=
                                                           _binSize
                                    //|| Math.Abs(x.ReferencePosition - indel.ReferencePosition) <= x.Length && x.Type == indel.Type && x.Length == indel.Length)
                                ))
                            .ToList();

                        // TODO consider making the threshold less for larger binsizes?
                        var sumOfScores = nearbyIndels.Sum(x => x.Score) + indel.Score;
                        if ((indel.Score / (float)sumOfScores) > 0.5)
                        {
                            var indelsToBlacklist = nearbyIndels.Where(x =>
                                !(
                                    // Same allele
                                    (x.ReferencePosition == indel.ReferencePosition &&
                                  x.ReferenceAllele == indel.ReferenceAllele &&
                                  x.AlternateAllele == indel.AlternateAllele) 
                                ||
                                    // Indel contained in multi other, and other is at least ok-ish quality
                                (!indel.InMulti && x.InMulti && Helper.MultiIndelContainsIndel(x, indel) && x.Score >= (indel.Score * 0.3)) 
                                    ||
                                    // Other contained in multi indel, and other is at least ok-ish quality
                                    (indel.InMulti && !x.InMulti && Helper.MultiIndelContainsIndel(indel,x) && x.Score >= (indel.Score *0.3)) 
                                    )
                                    
                                    &&  x.Score < (indel.Score * 0.5)).ToList();

                            foreach (var nearbyIndel in indelsToBlacklist)
                            {
                                blacklistedIndels.Add(nearbyIndel);
                            }

                            if (indelsToBlacklist.Any() && _debug)
                            {
                                Logger.WriteToLog(
                                    $"Removed {indelsToBlacklist.Count()} nearby variants to {indel} ({indel.Score}). Next highest score: {indelsToBlacklist.Max(x => x.Score)}");

                            }
                        }
                    }

                    if (!addedAsConcurrent)
                    {
                        indelsForChrom.Add(indel);
                    }

                }

                foreach (var candidateIndel in blacklistedIndels)
                {
                    //Logger.WriteToLog("Removing candidate indel because of blacklist");
                    indelsForChrom.Remove(candidateIndel);
                }
            }

            return indelsForChrom;
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