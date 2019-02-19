using System;
using System.Collections.Generic;
using System.Linq;
using Common.IO.Utility;
using Gemini.Models;
using Gemini.Types;
using Gemini.Utility;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

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
        private const int NumEvidenceDataPoints = 9;


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

        public Dictionary<string, List<PreIndel>> MergeIndelEvidence(List<Dictionary<string, int[]>> indelStringLookups)
        {
            var mergedLookup = new Dictionary<string, int[]>();
            var keys = indelStringLookups.SelectMany(x=>x.Keys).Distinct();

            foreach (var key in keys)
            {
                var evidence = new int[NumEvidenceDataPoints];

                foreach (var indelStringLookup in indelStringLookups)
                {
                    if (indelStringLookup.ContainsKey(key))
                    {
                        var originalEvidence = indelStringLookup[key];
                        for (var i = 0; i < NumEvidenceDataPoints; i++)
                        {
                            evidence[i] += originalEvidence[i];
                        }
                    }
                }

                mergedLookup[key] = evidence;
            }

            var indelsLookup = GetRealignablePreIndels(mergedLookup, false);
            return indelsLookup;
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
            return indelIdentifier;
        }

        public Dictionary<string, List<PreIndel>> GetRealignablePreIndels(Dictionary<string, int[]> indelStringLookup, bool allowRescue)
        {
            var statusCounter = new IndelStatusCounter();
            var edgeThreshold = Math.Max(_foundThreshold + 1, _foundThreshold * 1.5);
            var indelsToAdd = new List<PreIndel>();
            var multiIndelsToRecalculate = new Dictionary<HashableIndel, List<int[]>>();

            // TODO different way of doing this, bc we don't use the indel after that
            var indelsLookup = new Dictionary<string, List<PreIndel>>();
            foreach (var key in indelStringLookup.Keys)
            {
                var indelMetrics = indelStringLookup[key];

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
                        if (!multiIndelsToRecalculate.ContainsKey(multiKey))
                        {
                            multiIndelsToRecalculate.Add(multiKey, new List<int[]>());
                        }

                        multiIndelsToRecalculate[multiKey].Add(indelMetrics);
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
                }
            }

            foreach (var indelToRecalculate in multiIndelsToRecalculate)
            {
                var hashable = indelToRecalculate.Key;
                var indel = new PreIndel(new CandidateAllele(hashable.Chromosome, hashable.ReferencePosition,
                    hashable.ReferenceAllele, hashable.AlternateAllele, hashable.Type));
                indel.InMulti = hashable.InMulti;
                indel.OtherIndel = hashable.OtherIndel;

                var metrics = new int[9];
                foreach (var metricsList in indelToRecalculate.Value)
                {
                    for (var i = 0; i < 9; i++)
                    {
                        metrics[i] += metricsList[i];
                    }
                }

                var entryIndels = ExtractIndelsFromEntry(metrics, indel.ToString() + "|" + indel.OtherIndel,
                    statusCounter, edgeThreshold, allowRescue, new List<PreIndel>(){indel});
                if (entryIndels != null)
                {
                    indelsToAdd.AddRange(entryIndels);
                }
            }

            foreach (var indel in indelsToAdd)
            {
                if (!indelsLookup.ContainsKey(indel.Chromosome))
                {
                    indelsLookup.Add(indel.Chromosome, new List<PreIndel>());
                }

                indelsLookup[indel.Chromosome].Add(indel);
            }

            Logger.WriteToLog(
                $"Completed filtering indels (rescuing {(allowRescue ? "is" : "not")} enabled): Discarded {statusCounter.BelowThreshold} below thresholds ({statusCounter.Rescued} rescued), {statusCounter.PoorSingle} low quality with single observation, {statusCounter.PoorEdge} very low quality with <= {edgeThreshold} observations. Kept {statusCounter.Kept} observed events.");
            return indelsLookup;
        }


        private bool IsStrong(float avgQuals, float reputableSupportFraction, float avgAnchorLeft, float avgMess, float avgAnchorRight, float reverseSupport, int observationCount, float fwdSupport, string i, float stitchedSupport)
        {
            if (Math.Min(avgAnchorLeft, avgAnchorRight) < _strictAnchorThreshold)
            {
                return false;
            }
            if (observationCount < _strictFoundThreshold)
            {
                return false;
            }

            // TODO maybe also look at average min anchor for each read? if it was only at the beginnings or the ends, that could be interesting

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

        private bool IsStrongNew(float avgQuals, float reputableSupportFraction, float avgAnchorLeft, float avgMess, float avgAnchorRight, float reverseSupport, int observationCount, float fwdSupport, string i)
        {
            if (Math.Min(avgAnchorLeft, avgAnchorRight) < _strictAnchorThreshold)
            {
                return false;
            }
            if (observationCount < _strictFoundThreshold)
            {
                return false;
            }

            var isStrong = (avgQuals > 32 && reputableSupportFraction > 0.75 && avgMess <= Math.Max(1.5, Math.Min(avgAnchorLeft, avgAnchorRight) / 20) &&
                        (
                            (Math.Min(avgAnchorLeft, avgAnchorRight) > Math.Max(30, _anchorThreshold * 3)) && avgMess <= 0.4)) // One or more and super clean, very anchored
                       ||
                       ((observationCount > _foundThreshold * 0.5 && Math.Min(avgAnchorLeft, avgAnchorRight) > Math.Max(20, _anchorThreshold * 2)) // Rescue low freq but not super low, with good anchor
                        ||
                        (observationCount >= _foundThreshold * 1.5 && Math.Min(avgAnchorLeft, avgAnchorRight) > _anchorThreshold * 0.5)); // rescue high freq low anchor

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

        private List<PreIndel> ExtractIndelsFromEntry(int[] indelMetrics, string keyString,
            IndelStatusCounter statusCounter,
            double edgeThreshold, bool allowRescue, List<PreIndel> indels)
        {
            var indelsToAdd = new List<PreIndel>();
            var observationCount = indelMetrics[0];
            var anchorLeft = indelMetrics[1];
            var anchorRight = indelMetrics[2];
            var mess = indelMetrics[3];
            var quals = indelMetrics[4];
            var fwdSupport = indelMetrics[5] / (float) observationCount;
            var reverseSupport = indelMetrics[6] / (float) observationCount;
            var stitchedSupport = indelMetrics[7] / (float)observationCount;
            var reputableSupportFraction = indelMetrics[8] / (float) observationCount;

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
                    reverseSupport, reputableSupportFraction, avgQuals, stitchedSupport);
                var indel2 = GetIndelFromEntry(indels[1], anchorLeft, anchorRight, observationCount, mess, fwdSupport,
                    reverseSupport, reputableSupportFraction, avgQuals, stitchedSupport);

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
                    reverseSupport, reputableSupportFraction, avgQuals, stitchedSupport);

                indelsToAdd.Add(indel);
            }

            if (indels.Count == 1 && indelsToAdd[0].Length == 1 && (observationCount < _foundThreshold * 0.8 || observationCount < 2))
            {
                isStrong = false;
            }
            if (ShouldRemoveVariant(observationCount, avgAnchorLeft, avgAnchorRight, isStrong, statusCounter, keyString,
                avgQuals,
                avgMess, fwdSupport, reverseSupport, reputableSupportFraction, anchorLeft, anchorRight, edgeThreshold, stitchedSupport))
            {
                return null;
            }

            if (_debug)
            {   
                Logger.WriteToLog("KEPT:\t" + VariantDetailsString(keyString, observationCount, avgQuals, avgMess, avgAnchorLeft, avgAnchorRight, fwdSupport, reverseSupport, stitchedSupport, reputableSupportFraction));
            }

            statusCounter.Kept++;

            // TODO determine whether further consideration needs to be done for different variant types



            return indelsToAdd;
        }

        private static string VariantDetailsString(string keyString, int observationCount, float avgQuals, float avgMess,
            float avgAnchorLeft, float avgAnchorRight, float fwdSupport, float reverseSupport, float stitchedSupport,
            float reputableSupportFraction)
        {
            var resultString = keyString + ", o:" + observationCount + ", q:" + avgQuals + ", m:" + avgMess +
                               ", a:" +
                               Math.Min(avgAnchorLeft, avgAnchorRight) + ", f:" + fwdSupport + ", r:" +
                               reverseSupport + ", s:" + stitchedSupport +
                               ", reput:" + reputableSupportFraction;

            return resultString;
        }

        private bool ShouldRemoveVariant(int observationCount, float avgAnchorLeft, float avgAnchorRight, bool isStrong,
            IndelStatusCounter statusCounter, string keyString, float avgQuals, float avgMess, float fwdSupport, float reverseSupport,
            float reputableSupportFraction, int anchorLeft, int anchorRight, double edgeThreshold, float stitchedSupport)
        {
            if (observationCount < _foundThreshold || avgAnchorLeft < _anchorThreshold || avgAnchorRight < _anchorThreshold || avgMess > _maxMess)
            {
                if (isStrong)
                {
                    statusCounter.Rescued++;
                    if(_debug){
                        Logger.WriteToLog("RESCUED:\t" + VariantDetailsString(keyString, observationCount, avgQuals, avgMess, avgAnchorLeft, avgAnchorRight, fwdSupport, reverseSupport, stitchedSupport, reputableSupportFraction));

                    }
                }
                else
                {
                    if(_debug){
                        Logger.WriteToLog("BELOW THRESH:\t" + VariantDetailsString(keyString, observationCount, avgQuals, avgMess, avgAnchorLeft, avgAnchorRight, fwdSupport, reverseSupport, stitchedSupport, reputableSupportFraction));

                    }
                    statusCounter.BelowThreshold++;
                    return true;
                }
            }


            if (observationCount == 1 && (Math.Min(anchorLeft, anchorRight) < 5 || avgMess > 1 || avgQuals < 30))
            {
                if (_debug){
                    Logger.WriteToLog("POOR SINGLE:\t" + VariantDetailsString(keyString, observationCount, avgQuals, avgMess, avgAnchorLeft, avgAnchorRight, fwdSupport, reverseSupport, stitchedSupport, reputableSupportFraction));

                }
                statusCounter.PoorSingle++;
                // Even if we want to allow single-observation variants to be realigned against, maybe let's avoid the really junky ones
                return true;
            }

            if ((observationCount <= edgeThreshold) && (avgMess > 2 || avgQuals < 25))
            {
                if (_debug)
                {
                    Logger.WriteToLog("POOR EDGE:\t" + VariantDetailsString(keyString, observationCount, avgQuals,
                                          avgMess, avgAnchorLeft, avgAnchorRight, fwdSupport, reverseSupport,
                                          stitchedSupport, reputableSupportFraction));
                }

                statusCounter.PoorEdge++;
                return true;
            }

            return false;
        }

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
            float avgQuals, float stitchedSupport)
        {

            var averageAnchor = (anchorLeft + anchorRight) /
                                observationCount;
            var averageMess = (mess / observationCount) -
                              Math.Abs(indel.ReferenceAllele.Length - indel.AlternateAllele.Length);

            var balance = Math.Max(1,
                (fwdSupport >= reverseSupport ? reverseSupport / fwdSupport : fwdSupport / reverseSupport) + stitchedSupport);

            // Still want to care about absolute anchor lengths (maybe reads are variable lengths), but also definitely need to care about anchor balance (3 reads at 90/10 would have same avg anchor as 3 reads at 50/50)
            var anchorBalance = Math.Max(1, anchorLeft >= anchorRight ? (anchorLeft / (float)anchorRight) : (anchorRight/(float)anchorLeft));


            indel.Score = Math.Max(0, (int)(observationCount * (Math.Max(1, averageAnchor / 2 - averageMess)) * balance * anchorBalance * (1 + reputableSupport) * avgQuals / 30));
            return indel;
        }
    }
}
