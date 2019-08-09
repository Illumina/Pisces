using System;
using System.Collections.Generic;
using System.Linq;
using Common.IO.Utility;
using Gemini.IndelCollection;
using Gemini.Models;
using Gemini.Utility;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using ReadRealignmentLogic.Models;

namespace Gemini.CandidateIndelSelection
{
    public class BasicIndelFilterer
    {
        private readonly int _foundThreshold;
        private readonly int _anchorThreshold;
        private readonly bool _debug;
        private readonly int _strictAnchorThreshold;
        private readonly int _strictFoundThreshold;
        private readonly int _maxMess;


        public BasicIndelFilterer(int foundThreshold, int anchorThreshold, bool debug, int strictAnchorThreshold = 0, int strictFoundThreshold = 0, int maxMess = 20)
        {
            _foundThreshold = foundThreshold;
            _anchorThreshold = anchorThreshold;
            _debug = debug;
            _strictAnchorThreshold = strictAnchorThreshold;
            _strictFoundThreshold = strictFoundThreshold;
            _maxMess = maxMess;
        }


        private class IndelStatusCounter
        {
            public int Kept;
            public int Rescued;
            public int BelowThreshold;
            public int PoorSingle;
            public int PoorEdge;
        }

        private static HashableIndel GetHashableIndel(PreIndel preIndel, int score = 0)
        {
            var indelIdentifier = new HashableIndel
            {
                Chromosome = preIndel.Chromosome,
                ReferencePosition = preIndel.ReferencePosition,
                ReferenceAllele = preIndel.ReferenceAllele,
                AlternateAllele = preIndel.AlternateAllele,
                Type = preIndel.ReferenceAllele.Length > preIndel.AlternateAllele.Length
                    ? AlleleCategory.Deletion
                    : AlleleCategory.Insertion,
                Length = Math.Abs(preIndel.ReferenceAllele.Length - preIndel.AlternateAllele.Length),
                Score = score,
                InMulti = preIndel.InMulti,
                OtherIndel = preIndel.OtherIndel
            };
            return Helper.CopyHashable(indelIdentifier);
        }

        public Dictionary<string, List<PreIndel>> GetRealignablePreIndels(Dictionary<string, IndelEvidence> indelStringLookup, bool allowRescue, int regionEdgeThreshold = int.MaxValue)
        {
            var statusCounter = new IndelStatusCounter();
            var edgeThreshold = Math.Max(_foundThreshold + 1, _foundThreshold * 1.5);
            var indelsToAdd = new List<PreIndel>();
            var multiIndelsToRecalculate = new Dictionary<HashableIndel, List<IndelEvidence>>();
            var indelsToRemove = new List<string>();

            var numImmediatelySkipped = 0;
            var numProcessed = 0;

            // TODO different way of doing this, bc we don't use the indel after that
            var indelsLookup = new Dictionary<string, List<PreIndel>>();
            foreach (var key in indelStringLookup.Keys)
            {
                var indelMetrics = indelStringLookup[key];
                var keepForNextRegion = indelMetrics.Position >= regionEdgeThreshold;

                if (indelMetrics.Observations == 0 && !keepForNextRegion)
                {
                    indelMetrics.Outcome = Outcome.LowObservations;
                    continue;
                }
                numProcessed++;

                if (indelMetrics.Observations < _strictFoundThreshold && !keepForNextRegion)
                {
                    indelMetrics.Outcome = Outcome.LowObservations;
                    numImmediatelySkipped++;
                    continue;
                }

                // No reputable evidence!
                if (indelMetrics.ReputableSupport < 1 && !keepForNextRegion)
                {
                    var reqSupport = Math.Max(_foundThreshold, 2);
                    var considerRescuing =
                        indelMetrics.Forward > reqSupport && indelMetrics.Reverse > reqSupport;
                    if (!considerRescuing)
                    {
                        indelMetrics.Outcome = Outcome.LowReputableSupport;
                        numImmediatelySkipped++;
                        continue;
                    }
                }

                var entryIndelKeys = ExtractIndelsFromKeyString(key);
                if (entryIndelKeys == null)
                {
                    continue;
                }
                if (entryIndelKeys.Count > 1)
                {
                    foreach (var entryIndel in entryIndelKeys)
                    {
                        var multiKey = GetHashableIndel(entryIndel);

                        if (!multiIndelsToRecalculate.TryGetValue(multiKey, out var existingIndelMetrics))
                        {
                            existingIndelMetrics = new List<IndelEvidence>();
                            multiIndelsToRecalculate[multiKey] = existingIndelMetrics;
                        }
                        existingIndelMetrics.Add(indelMetrics);
                    }
                }
                else
                {

                    var entryIndels = ExtractIndelsFromEntry(indelMetrics, key, statusCounter, edgeThreshold,
                        allowRescue, entryIndelKeys);
                    if (entryIndels != null)
                    {
                        indelsToAdd.AddRange(entryIndels);
                    }
                    else
                    {
                        indelsToRemove.Add(key);
                    }
                }
            }

            foreach (var indelToRecalculate in multiIndelsToRecalculate)
            {
                RecalculateIndelAndAddIfNeeded(allowRescue, indelToRecalculate, statusCounter, edgeThreshold, indelsToAdd);
            }

            foreach (var indel in indelsToAdd)
            {
                if (!indelsLookup.TryGetValue(indel.Chromosome, out var indelsForChrom))
                {
                    indelsForChrom = new List<PreIndel>();
                    indelsLookup.Add(indel.Chromosome, indelsForChrom);
                }
                indelsForChrom.Add(indel);
            }

            foreach (var badIndel in indelsToRemove)
            {
                indelStringLookup.Remove(badIndel);
            }

            statusCounter.BelowThreshold += numImmediatelySkipped;
            return indelsLookup;
        }

        private void RecalculateIndelAndAddIfNeeded(bool allowRescue, KeyValuePair<HashableIndel, List<IndelEvidence>> indelToRecalculate,
            IndelStatusCounter statusCounter, double edgeThreshold, List<PreIndel> indelsToAdd)
        {
            var hashable = indelToRecalculate.Key;
            var indel = new PreIndel(new CandidateAllele(hashable.Chromosome, hashable.ReferencePosition,
                hashable.ReferenceAllele, hashable.AlternateAllele, hashable.Type));
            indel.InMulti = hashable.InMulti;
            indel.OtherIndel = hashable.OtherIndel;

            var metrics = new IndelEvidence();
            foreach (var metricsList in indelToRecalculate.Value)
            {
                metrics.AddIndelEvidence(metricsList);
            }

            var entryIndels = ExtractIndelsFromEntry(metrics, indel.ToString() + "|" + indel.OtherIndel,
                statusCounter, edgeThreshold, allowRescue, new List<PreIndel>() {indel});
            if (entryIndels != null)
            {
                indelsToAdd.AddRange(entryIndels);
            }
        }


        private bool IsStrong(float avgQuals, float reputableSupportFraction, float avgAnchorLeft, float avgMess, float avgAnchorRight, float reverseSupport, int observationCount, float fwdSupport, string i, float stitchedSupport)
        {
            if (observationCount < _strictFoundThreshold)
            {
                return false;
            }

            if (Math.Min(avgAnchorLeft, avgAnchorRight) < _strictAnchorThreshold)
            {
                return false;
            }

            // TODO these are magic right now, fix this
            var isStrong = avgQuals > 32 &&
                       ((reputableSupportFraction > 0.75 && Math.Min(avgAnchorLeft, avgAnchorRight) > 30 &&
                         avgMess <= 0.4) ||
                        (avgMess <= Math.Max(1.5, Math.Min(avgAnchorLeft, avgAnchorRight) / 20) &&
                         reputableSupportFraction > 0.6 && Math.Abs((fwdSupport - reverseSupport) + stitchedSupport) < 0.25))
                       &&
                       ((observationCount > 2 && avgAnchorLeft > 20 && avgAnchorRight > 20) ||
                        avgAnchorLeft > 30 && avgAnchorRight > 30);

            if (i.Contains("|") && !isStrong)
            {
                isStrong = avgQuals > 34 && avgMess <= 1 && i.Contains("|") && avgAnchorLeft > 10 &&
                           avgAnchorRight > 10;
            }

            return isStrong;
        }

        private List<PreIndel> ExtractIndelsFromKeyString(string keyString)
        {
            var splittedIndels = keyString.Split('|');
            if (splittedIndels.Length > 2)
            {
                return null;
            }
            else
            {
                var listOfIndels = new List<PreIndel>();
                if (splittedIndels.Length == 1)
                {
                    listOfIndels.Add(GetIndelKey(splittedIndels[0]));
                }
                else
                {
                    var indel1 = GetIndelKey(splittedIndels[0]);
                    var indel2 = GetIndelKey(splittedIndels[1]);

                    indel1.InMulti = true;
                    indel1.OtherIndel = Helper.CandidateToString(indel2);
                    indel2.InMulti = true;
                    indel2.OtherIndel = Helper.CandidateToString(indel1);

                    listOfIndels.Add(indel1);
                    listOfIndels.Add(indel2);

                }

                return listOfIndels;
            }

        }

        private List<PreIndel> ExtractIndelsFromEntry(IndelEvidence indelMetrics, string keyString,
            IndelStatusCounter statusCounter,
            double edgeThreshold, bool allowRescue, List<PreIndel> indels)
        {
            var indelsToAdd = new List<PreIndel>();
            var observationCount = indelMetrics.Observations;
            var anchorLeft = indelMetrics.LeftAnchor;
            var anchorRight = indelMetrics.RightAnchor;
            var mess = indelMetrics.Mess;
            var quals = indelMetrics.Quality;
            var fwdSupport = indelMetrics.Forward / (float) observationCount;
            var reverseSupport = indelMetrics.Reverse / (float) observationCount;
            var stitchedSupport = indelMetrics.Stitched / (float)observationCount;
            var reputableSupportFraction = indelMetrics.ReputableSupport / (float) observationCount;
            var numFromUnanchoredRepeat = indelMetrics.IsRepeat;
            var numFromMateUnmapped = indelMetrics.IsSplit;

            var avgAnchorLeft = anchorLeft / (float) observationCount;
            var avgAnchorRight = anchorRight / (float) observationCount;

            var avgQuals = quals / (float) observationCount;
            var avgMess = mess / (float) observationCount;

            // TODO clean this up, no more magic
            bool isStrong = false;

            if (allowRescue)
            {
                isStrong = IsStrong(avgQuals, reputableSupportFraction, avgAnchorLeft, avgMess, avgAnchorRight,
                    reverseSupport, observationCount, fwdSupport, keyString, stitchedSupport);
            }
            
            if (indels.Count > 2)
            {
                Logger.WriteToLog(
                    $"Can't support more than 2 indels in one read: ignoring multi-indel {keyString} (seen {observationCount} times)");
            }
            else if (indels.Count > 1)
            {
                var indel1 = GetIndelFromEntry(indels[0], anchorLeft, anchorRight, observationCount, mess, fwdSupport,
                    reverseSupport, reputableSupportFraction, avgQuals, stitchedSupport, numFromMateUnmapped, numFromUnanchoredRepeat);
                var indel2 = GetIndelFromEntry(indels[1], anchorLeft, anchorRight, observationCount, mess, fwdSupport,
                    reverseSupport, reputableSupportFraction, avgQuals, stitchedSupport, numFromMateUnmapped, numFromUnanchoredRepeat);

                indel1.InMulti = true;
                indel2.InMulti = true;

                indel1.OtherIndel = Helper.CandidateToString(indel2);
                indel2.OtherIndel = Helper.CandidateToString(indel1);

                indelsToAdd.Add(indel1);
                indelsToAdd.Add(indel2);
            }
            else
            {
                var indel = GetIndelFromEntry(indels[0], anchorLeft, anchorRight, observationCount, mess, fwdSupport,
                    reverseSupport, reputableSupportFraction, avgQuals, stitchedSupport, numFromMateUnmapped, numFromUnanchoredRepeat);

                indelsToAdd.Add(indel);
            }

            if (indels.Count == 1 && indelsToAdd[0].Length == 1 && (observationCount < _foundThreshold * 0.8 || observationCount <= 2))
            {
                indelMetrics.Outcome = Outcome.SuperWeakSmall;
                return null;
            }
            if (ShouldRemoveVariant(observationCount, avgAnchorLeft, avgAnchorRight, isStrong, statusCounter,
                avgQuals,
                avgMess, anchorLeft, anchorRight, edgeThreshold, indelMetrics))
            {
                return null;
            }

            statusCounter.Kept++;

            return indelsToAdd;
        }

        private bool ShouldRemoveVariant(int observationCount, float avgAnchorLeft, float avgAnchorRight, bool isStrong,
            IndelStatusCounter statusCounter, float avgQuals, float avgMess, int anchorLeft, int anchorRight, double edgeThreshold, IndelEvidence evidence)
        {
            if (observationCount < _foundThreshold || avgAnchorLeft < _anchorThreshold || avgAnchorRight < _anchorThreshold || avgMess > _maxMess)
            {
                if (isStrong)
                {
                    evidence.Outcome = Outcome.Rescued;
                    statusCounter.Rescued++;
                }
                else
                {
                    evidence.Outcome = Outcome.BelowThreshold;
                    statusCounter.BelowThreshold++;
                    return true;
                }
            }


            if (observationCount == 1 && (Math.Min(anchorLeft, anchorRight) < 5 || avgMess > 1 || avgQuals < 30))
            {
                evidence.Outcome = Outcome.PoorSingle;
                statusCounter.PoorSingle++;
                // Even if we want to allow single-observation variants to be realigned against, maybe let's avoid the really junky ones
                return true;
            }

            if ((observationCount <= edgeThreshold) && (avgMess > 2 || avgQuals < 25))
            {
                evidence.Outcome = Outcome.PoorEdge;
                statusCounter.PoorEdge++;
                return true;
            }

            return false;
        }

        // TODO EXTRACT TO SHARED
        private static PreIndel GetIndelKey(string splittedIndel)
        {
            var splitString = splittedIndel.Split(' ').SelectMany(x => x.Split(':', '>')).ToList();
            var chrom = splitString[0];
            var pos = int.Parse(splitString[1]);
            var refAllele = splitString[2];
            var altAllele = splitString[3];

            var indel = new PreIndel(new CandidateAllele(chrom, pos, refAllele, altAllele,
                refAllele.Length > altAllele.Length ? AlleleCategory.Deletion : AlleleCategory.Insertion)
            {
            });
            return indel;
        }

        private static PreIndel GetIndelFromEntry(PreIndel indel, int anchorLeft, int anchorRight,
            int observationCount, int mess, float fwdSupport, float reverseSupport, float reputableSupport,
            float avgQuals, float stitchedSupport, int numFromMateUnmapped, int numFromUnanchoredRepeat)
        {
            var averageAnchor = (anchorLeft + anchorRight) /
                                observationCount;
            var averageMess = (mess / (float)observationCount);

            var balance = Math.Max(1,
                (fwdSupport >= reverseSupport ? reverseSupport / fwdSupport : fwdSupport / reverseSupport) + stitchedSupport);

            balance = fwdSupport >= reverseSupport ? fwdSupport / (Math.Max(1,reverseSupport)) : reverseSupport / Math.Max(1,fwdSupport);

            // Still want to care about absolute anchor lengths (maybe reads are variable lengths), but also definitely need to care about anchor balance (3 reads at 90/10 would have same avg anchor as 3 reads at 50/50)
            var anchorBalance = Math.Max(1, (anchorLeft >= anchorRight ? (anchorLeft / (float)anchorRight) : (anchorRight/(float)anchorLeft)));
            anchorBalance = anchorLeft >= anchorRight
                ? anchorLeft / (float) (Math.Max(1, anchorRight)) : anchorRight / (float) (Math.Max(1, anchorLeft));

            var averageCleanAnchor = (averageAnchor - averageMess) / (float)averageAnchor;

            indel.Observations = observationCount;
            indel.Score = (int)(Math.Max(0, (int)(observationCount * (1/balance) * (1/anchorBalance) * (1 + reputableSupport + (stitchedSupport/balance)) * (avgQuals / 30) * averageCleanAnchor * 10)) * (1 + (indel.Length / 5) ) * ((observationCount - numFromMateUnmapped - numFromUnanchoredRepeat) / (float)observationCount));

            return indel;
        }
    }
}