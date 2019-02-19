using System;
using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Gemini.Interfaces;
using Gemini.Models;
using Gemini.Realignment;

namespace Gemini.Logic
{
    public class ReadPairRealignerAndCombiner : IReadPairHandler
    {
        private readonly string _chromosome;
        private readonly bool _alreadyStitched;
        private readonly bool _pairAware;
        private readonly Dictionary<string, int[]> _masterLookup;
        private readonly bool _debug;

        private readonly IEvidenceCollector _evidenceCollector;
        private readonly IReadRestitcher _restitcher;
        private readonly IRealignmentEvaluator _evaluator;
        private readonly IPairSpecificIndelFinder _finder;

        public ReadPairRealignerAndCombiner(IEvidenceCollector evidenceCollector, IReadRestitcher restitcher,
            IRealignmentEvaluator evaluator, IPairSpecificIndelFinder finder, string chromosome, bool alreadyStitched, bool pairAware = true, Dictionary<string, int[]> masterLookup = null, bool debug = false)
        {
            _evidenceCollector = evidenceCollector;
            _restitcher = restitcher;
            _evaluator = evaluator;
            _finder = finder;
            _chromosome = chromosome;
            _alreadyStitched = alreadyStitched;
            _pairAware = pairAware;
            _masterLookup = masterLookup;
            _debug = debug;
        }

        public List<BamAlignment> ExtractReads(ReadPair pair)
        {
            var reads = new List<BamAlignment>();

            if (pair.IsComplete(false) && pair.PairStatus == PairStatus.Paired)
            {
                var realignedR1 = false;
                var realignedR2 = false;
                var forcedSoftclipR1 = false;
                var forcedSoftclipR2 = false;
                int? r1Nm = null;
                int? r2Nm = null;
                var origRead1 = pair.Read1;
                var origRead2 = pair.Read2;

                List<PreIndel> pairIndels = null;

                if (_pairAware)
                {
                    pairIndels = _finder.GetPairSpecificIndels(pair, ref r1Nm, ref r2Nm);
                }

                //if (pairIndels != null && pairIndels.Any())
                //{
                //    _statusCounter.AddStatusCount("Attempting to realign around pair-specific indels.");
                //}

                // TODO configure stuff in evaluator
                pair.Read1 = _evaluator.GetFinalAlignment(origRead1,
                    out realignedR1, out forcedSoftclipR1, pairIndels);
                pair.Read2 = _evaluator.GetFinalAlignment(origRead2,
                    out realignedR2, out forcedSoftclipR2, pairIndels);

                if (!(forcedSoftclipR1 || forcedSoftclipR2))
                {
                    reads = _restitcher.GetRestitchedReads(pair, origRead1, origRead2, r1Nm, r2Nm, pairIndels != null);

                    var isStitched = reads.Count == 1;

                    if (!isStitched)
                    {
                        reads.Clear();
                        reads.Add(pair.Read1);
                        reads.Add(pair.Read2);

                        if (realignedR1)
                        {
                            pair.Read2.MatePosition = pair.Read1.Position;
                            _evidenceCollector.CollectEvidence(pair.Read1, true, false, _chromosome);
                        }
                        if (realignedR2)
                        {
                            pair.Read1.MatePosition = pair.Read2.Position;
                            _evidenceCollector.CollectEvidence(pair.Read2, true, false, _chromosome);
                        }
                    }
                    foreach (var bamAlignment in reads)
                    {
                        _evidenceCollector.CollectEvidence(bamAlignment, true, isStitched, _chromosome);
                    }

                }
                else
                {
                    reads.Add(pair.Read1);
                    reads.Add(pair.Read2);

                    if (realignedR1)
                    {
                        pair.Read2.MatePosition = pair.Read1.Position;
                        _evidenceCollector.CollectEvidence(pair.Read1, true, false, _chromosome);
                    }
                    if (realignedR2)
                    {
                        pair.Read1.MatePosition = pair.Read2.Position;
                        _evidenceCollector.CollectEvidence(pair.Read2, true, false, _chromosome);
                    }

                }
            }
            else
            {
                var finalReadR1 = RealignSingleAlignment(pair.Read1, out var realignedR1);
                var finalReadR2 = RealignSingleAlignment(pair.Read2, out var realignedR2);

                UpdateAndAddAlignment(finalReadR1, realignedR2, finalReadR2, reads);
                UpdateAndAddAlignment(finalReadR2, realignedR1, finalReadR1, reads);
            }


            return reads;
        }

        public void Finish()
        {
            var evidence = _evidenceCollector.GetEvidence();

            if (evidence != null && _masterLookup != null)
            {
                foreach (var keyValuePair in evidence)
                {
                    if (_debug)
                    {
                        Console.WriteLine(keyValuePair.Key + ":" + string.Join(",", keyValuePair.Value));
                    }
                    if (!_masterLookup.ContainsKey(keyValuePair.Key))
                    {
                        _masterLookup.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                    else
                    {
                        // TODO clean up
                        for (int i = 0; i < 9; i++)
                        {
                            _masterLookup[keyValuePair.Key][i] += keyValuePair.Value[i];
                        }
                    }
                }
            }
        }

        private static void UpdateAndAddAlignment(BamAlignment finalRead, bool realignedOther, BamAlignment finalReadOther,
            List<BamAlignment> reads)
        {
            if (finalRead != null)
            {
                if (realignedOther)
                {
                    finalRead.MatePosition = finalReadOther.Position;
                }

                reads.Add(finalRead);
            }
        }

        private BamAlignment RealignSingleAlignment(BamAlignment bamAlignment, out bool realigned)
        {
            if (bamAlignment == null)
            {
                realigned = false;
                return null;
            }
            realigned = false;
            var forcedSoftclip = false;
            var realignedRead = _evaluator.GetFinalAlignment(bamAlignment, out realigned, out forcedSoftclip);

            if ((realigned || forcedSoftclip) && _alreadyStitched)
            {
                // Update the XD tag if stitched read changed

                if (realignedRead.TagData == null)
                {
                    realignedRead.TagData = new byte[0];
                }
                // TODO make this real - update the direction string
                // This only matters in the case where we realign a read that already was stitched and add or remove a deletion, thereby affecting the reference span.                
                var newXd = StitchedRealignmentHelpers.GetUpdatedXdForRealignedStitchedRead(bamAlignment, realignedRead);
                if (newXd != null)
                {
                    TagUtils.ReplaceOrAddStringTag(ref realignedRead.TagData, "XD", newXd);
                }

            }

            if (realigned && !forcedSoftclip)
            {
                _evidenceCollector.CollectEvidence(realignedRead, true, false, _chromosome);
            }

            return realignedRead;
        }

    }
}
