using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.Models;
using Gemini.Realignment;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using ReadRealignmentLogic.Utlity;
using Helper = Gemini.Utility.Helper;

namespace Gemini.Logic
{
    public class ReadPairRealignerAndCombiner 
    {
        private readonly string _chromosome;
        private readonly bool _alreadyStitched;
        private readonly bool _pairAware;
        private readonly Dictionary<string, IndelEvidence> _masterLookup;
        private readonly Dictionary<HashableIndel, int[]> _masterOutcomesLookup;
        private readonly bool _skipRestitchIfNothingChanged;
        private readonly bool _allowedToStitch;
        private readonly bool _hasExistingIndels;

        private readonly IEvidenceCollector _evidenceCollector;
        private readonly IReadRestitcher _restitcher;
        private readonly IRealignmentEvaluator _evaluator;
        private readonly IPairSpecificIndelFinder _finder;
        private readonly IndelTargetFinder _indelTargetFinder = new IndelTargetFinder();
        private readonly List<PreIndel> _emptyList = new List<PreIndel>(0);

        public ReadPairRealignerAndCombiner(IEvidenceCollector evidenceCollector, IReadRestitcher restitcher,
            IRealignmentEvaluator evaluator, IPairSpecificIndelFinder finder, string chromosome, bool alreadyStitched, 
            bool pairAware = true, Dictionary<string, IndelEvidence> masterLookup = null, 
            bool hasExistingIndels = false, Dictionary<HashableIndel, int[]> masterOutcomesLookup = null, bool skipRestitchIfNothingChanged = false, bool allowedToStitch = true)
        {
            _evidenceCollector = evidenceCollector;
            _restitcher = restitcher;
            _evaluator = evaluator;
            _finder = finder;
            _chromosome = chromosome;
            _alreadyStitched = alreadyStitched;
            _pairAware = pairAware;
            _masterLookup = masterLookup;
            _hasExistingIndels = hasExistingIndels;
            _masterOutcomesLookup = masterOutcomesLookup;
            _skipRestitchIfNothingChanged = skipRestitchIfNothingChanged;
            _allowedToStitch = allowedToStitch;
        }

        public List<BamAlignment> ExtractReads(PairResult pairResult, INmCalculator nmCalculator, bool doRealign = true, int readsToSilence = 0)
        {
            //Console.WriteLine(pairResult.ReadPair.Name);
            var realignmentStateR1 = new RealignmentState();
            var realignmentStateR2 = new RealignmentState();

            var pair = pairResult.ReadPair;
            if (pair.PairStatus == PairStatus.LeaveUntouched)
            {
                return pair.GetAlignments().ToList();
            }
            var reads = new List<BamAlignment>();

            // TODO clean this up, should really not have two layers (pair result and readpair) here
            if (pairResult.ReadPair.Stitched)
            {
                const bool recalculateNm = true; // Always recalculate NM for stitched reads
                // TODO more explicitly assume/check that there is actually only one here
                foreach (var alignment in pairResult.Alignments)
                {
                    if (doRealign)
                    {
                        var finalReadR1 = RealignSingleAlignment(alignment, out var realignedR1, realignmentStateR1);
                        pair.RealignedR1 = realignedR1;

                        SilenceReads(finalReadR1, null, readsToSilence, realignedR1, false);

                        pair.StitchedNm = UpdateAndAddAlignment(finalReadR1, false, null, reads, nmCalculator, recalculateNm);

                    }
                    else
                    {
                        SilenceReads(alignment, null, readsToSilence, false, false);

                        pair.StitchedNm = UpdateAndAddAlignment(alignment, false, null, reads, nmCalculator, recalculateNm);
                    }
                }

                return reads;

            }
            if (pair.IsComplete(false) && (pair.PairStatus == PairStatus.Paired || pair.PairStatus == PairStatus.OffTarget))
            {
                var realignedR1 = false;

                var realignedR2 = false;
                var forcedSoftclipR1 = false;
                var forcedSoftclipR2 = false;
                var sketchy = false;
                var origRead1 = pair.Read1;
                var origRead2 = pair.Read2;
                int? r1Nm = pair.Read1.GetIntTag("NM");
                int? r2Nm = pair.Read2.GetIntTag("NM");
                List<PreIndel> pairIndels = null;


                if (pair.PairStatus == PairStatus.Paired)
                {
                    if (doRealign)
                    {
                        List<PreIndel> r1Indels = _hasExistingIndels && pairResult.R1Nm <= 2
                            ? pairResult.OriginalIndelsR1 ?? _indelTargetFinder.FindIndels(origRead1, _chromosome)
                            : _emptyList;
                        List<PreIndel> r2Indels = _hasExistingIndels && pairResult.R2Nm <= 2
                            ? pairResult.OriginalIndelsR2 ?? _indelTargetFinder.FindIndels(origRead2, _chromosome)
                            : _emptyList;

                        if (_pairAware && _hasExistingIndels && (r1Indels.Any() || r2Indels.Any()))
                        {
                            pairIndels = _finder.GetPairSpecificIndels(pair, r1Indels, r2Indels, ref r1Nm, ref r2Nm);
                        }
                      
                        var confirmedAccepted = new List<HashableIndel>();

                        bool confirmedR1 = false;
                        bool confirmedR2 = false;

                        var r1AlreadyGotToTry = false;
                        // TODO should we pay attention to whether one already has an indel and one doesn't, and realign/confirm the one that does, or that has the better one, first?
                        if (r1Indels.Count >= r2Indels.Count)
                        {
                            r1AlreadyGotToTry = true;
                            var try1Read1 = _evaluator.GetFinalAlignment(origRead1,
                                out realignedR1, out forcedSoftclipR1, out confirmedR1, out sketchy,
                                selectedIndels: pairIndels,
                                existingIndels: r1Indels, confirmedAccepteds: confirmedAccepted, mateIndels: r2Indels,
                                state: realignmentStateR1);

                            pair.Read1 = try1Read1;
                            //if (pair.Name == "HWI-D00119:50:H7AP8ADXX:2:1205:8560:72790")
                            //{
                            //    Console.WriteLine(
                            //        $"Done try1read1: {pair.Read1.CigarData}, {confirmedR1}, {realignedR1}");
                            //    Console.WriteLine(
                            //        $"{string.Join(",", confirmedAccepted?.Select(x => x.StringRepresentation))}");
                            //}
                        }
                        if ((realignedR1 || confirmedR1) && confirmedAccepted.Any())
                        {
                            pair.Read2 = _evaluator.GetFinalAlignment(origRead2,
                                out realignedR2, out forcedSoftclipR2, out confirmedR2, out sketchy, selectedIndels: pairIndels,
                                existingIndels: r2Indels, confirmedAccepteds: confirmedAccepted, state: realignmentStateR2);
                            //if (pair.Name == "HWI-D00119:50:H7AP8ADXX:2:1205:8560:72790")
                            //{
                            //    Console.WriteLine($"Done read2 after read 1: {pair.Read2.CigarData}");
                            //    Console.WriteLine($"{string.Join(",", confirmedAccepted?.Select(x => x.StringRepresentation))}");
                            //}
                        }
                        else
                        {
                            confirmedAccepted.Clear();
                            var try1Read2 = _evaluator.GetFinalAlignment(origRead2,
                                out realignedR2, out forcedSoftclipR2, out confirmedR2, out sketchy, selectedIndels: pairIndels,
                                existingIndels: r2Indels, confirmedAccepteds: confirmedAccepted, mateIndels: r1Indels, state: realignmentStateR2);
                            pair.Read2 = try1Read2;

                            //if (pair.Name == "HWI-D00119:50:H7AP8ADXX:2:1205:8560:72790")
                            //{
                            //    Console.WriteLine($"Done try1read2: {pair.Read2.CigarData}, {confirmedR2}, {realignedR2}");
                            //    Console.WriteLine($"{string.Join(",", confirmedAccepted?.Select(x => x.StringRepresentation))}");
                            //}
                            if ((realignedR2 || confirmedR2) && confirmedAccepted.Any())
                            {
                                pair.Read1 = _evaluator.GetFinalAlignment(origRead1,
                                    out realignedR1, out forcedSoftclipR1, out confirmedR1, out sketchy, selectedIndels: pairIndels,
                                    existingIndels: r1Indels, confirmedAccepteds: confirmedAccepted, state: realignmentStateR1);

                                //if (pair.Name == "HWI-D00119:50:H7AP8ADXX:2:1205:8560:72790")
                                //{
                                //    Console.WriteLine($"Done read1 after read2: {pair.Read1.CigarData}");
                                //    Console.WriteLine($"{string.Join(",", confirmedAccepted?.Select(x => x.StringRepresentation))}");
                                //}

                            }
                            else
                            {
                                if (!r1AlreadyGotToTry)
                                {
                                    var try1Read1 = _evaluator.GetFinalAlignment(origRead1,
                                        out realignedR1, out forcedSoftclipR1, out confirmedR1, out sketchy,
                                        selectedIndels: pairIndels,
                                        existingIndels: r1Indels, confirmedAccepteds: confirmedAccepted, mateIndels: r2Indels,
                                        state: realignmentStateR1);

                                    pair.Read1 = try1Read1;
                                    //if (pair.Name == "HWI-D00119:50:H7AP8ADXX:2:1205:8560:72790")
                                    //{
                                    //    Console.WriteLine(
                                    //        $"Done try1read1: {pair.Read1.CigarData}, {confirmedR1}, {realignedR1}");
                                    //    Console.WriteLine(
                                    //        $"{string.Join(",", confirmedAccepted?.Select(x => x.StringRepresentation))}");
                                    //}
                                }
                                if ((realignedR1 || confirmedR1) && confirmedAccepted.Any())
                                {
                                    pair.Read2 = _evaluator.GetFinalAlignment(origRead2,
                                        out realignedR2, out forcedSoftclipR2, out confirmedR2, out sketchy, selectedIndels: pairIndels,
                                        existingIndels: r2Indels, confirmedAccepteds: confirmedAccepted, state: realignmentStateR2);
                                    //if (pair.Name == "HWI-D00119:50:H7AP8ADXX:2:1205:8560:72790")
                                    //{
                                    //    Console.WriteLine($"Done read2 after read 1: {pair.Read2.CigarData}");
                                    //    Console.WriteLine($"{string.Join(",", confirmedAccepted?.Select(x => x.StringRepresentation))}");
                                    //}
                                }
                            
                       }

                        }

                        pairResult.R1Confirmed = confirmedR1;
                        pairResult.R2Confirmed = confirmedR2;
                    }
                    else
                    {
                        realignmentStateR1.Message = "Not do realign";
                        realignmentStateR2.Message = "Not do realign";
                    }

                    if (realignedR1 || realignedR2)
                    {
                        pair.Realigned = true;
                    }


                }


                pair.RealignedR1 = realignedR1;
                pair.RealignedR2 = realignedR2;

                pair.Message = realignmentStateR1.Message + " " + realignmentStateR2.Message;

                // Silence realigned pair if needed
                SilenceReads(pair.Read1, pair.Read2, readsToSilence, realignedR1, realignedR2);
                // Silence original reads in case needed
                SilenceReads(origRead1, origRead2, readsToSilence, false, false);

                if (_allowedToStitch && !(forcedSoftclipR1 || forcedSoftclipR2) && (!_skipRestitchIfNothingChanged || (realignedR1 || realignedR2)))
                {
                    reads = _restitcher.GetRestitchedReads(pair, origRead1, origRead2, r1Nm, r2Nm, pairIndels != null, nmCalculator, 
                        realignedR1 || realignedR2 || (r1Nm + r2Nm > 0),sketchy);

                    var isStitched = reads.Count == 1;
                    pair.Stitched = isStitched;
                    pairResult.TriedStitching = true;

                    if (!isStitched)
                    {
                        reads.Clear();
                        // TODO looks like we are passing through EC twice - here and in the below foreach
                        HandleUnstitchedReads(pair, reads, realignedR1, realignedR2, nmCalculator);
                    }
                    foreach (var bamAlignment in reads)
                    {
                        _evidenceCollector.CollectEvidence(bamAlignment, true, isStitched, _chromosome);
                    }
                }
                else
                {
                    HandleUnstitchedReads(pair, reads, realignedR1, realignedR2, nmCalculator);
                }


            }
            else
            {
                if (doRealign)
                {
                    var finalReadR1 = RealignSingleAlignment(pair.Read1, out var realignedR1, realignmentStateR1);
                    var finalReadR2 = RealignSingleAlignment(pair.Read2, out var realignedR2, realignmentStateR2);
                    pair.RealignedR1 = realignedR1;
                    pair.RealignedR2 = realignedR2;
                    if (realignedR1 || realignedR2)
                    {
                        pair.Realigned = true;
                    }
                    SilenceReads(finalReadR1, finalReadR2, readsToSilence, realignedR1, realignedR2);
                    pair.Nm1 = UpdateAndAddAlignment(finalReadR1, realignedR2, finalReadR2, reads, nmCalculator, realignedR1);
                    pair.Nm2 = UpdateAndAddAlignment(finalReadR2, realignedR1, finalReadR1, reads, nmCalculator, realignedR2);

                }
                else
                {

                    SilenceReads(pair.Read1, pair.Read2, readsToSilence, false, false);

                    pair.Nm1 = UpdateAndAddAlignment(pair.Read1, false, pair.Read2, reads, nmCalculator, false);
                    pair.Nm2 = UpdateAndAddAlignment(pair.Read2, false, pair.Read1, reads, nmCalculator, false);

                    realignmentStateR1.Message = "Not do realign";
                    realignmentStateR2.Message = "Not do realign";

                }

            }

            //Console.WriteLine(realignmentStateR1.Message);
            pair.Message = realignmentStateR1.Message + " " + realignmentStateR2.Message;


            return reads;
        }

        private void SilenceReads(BamAlignment read1, BamAlignment read2, int readsToSilence, bool realignedR1, bool realignedR2)
        {
            if (!realignedR1 && (readsToSilence == 1 || readsToSilence == 3))
            {
                for (var i = 0; i < read1.Qualities.Length; i++)
                {
                    read1.Qualities[i] = 0;
                }
            }
            if (!realignedR2 && (readsToSilence == 2 || readsToSilence == 3))
            {
                for (var i = 0; i < read2.Qualities.Length; i++)
                {
                    read2.Qualities[i] = 0;
                }
            }

        }

        private void HandleUnstitchedReads(ReadPair pair, List<BamAlignment> reads, bool realignedR1, bool realignedR2, INmCalculator nmCalculator)
        {
            reads.Add(pair.Read1);
            reads.Add(pair.Read2);

            if (realignedR1)
            {
                pair.Read2.MatePosition = pair.Read1.Position;
                _evidenceCollector.CollectEvidence(pair.Read1, true, false, _chromosome);
                var nm = nmCalculator.GetNm(pair.Read1);
                pair.Nm1 = nm;

                AddNmTag(pair.Read1, nm);
            }

            if (realignedR2)
            {
                pair.Read1.MatePosition = pair.Read2.Position;
                _evidenceCollector.CollectEvidence(pair.Read2, true, false, _chromosome);
                var nm = nmCalculator.GetNm(pair.Read1);
                pair.Nm2 = nm; 
                AddNmTag(pair.Read2, nm);
            }
        }


        public void Finish()
        {
            var evidence = _evidenceCollector.GetEvidence();

            if (evidence != null && _masterLookup != null)
            {
                foreach (var keyValuePair in evidence)
                {
                    if (!_masterLookup.ContainsKey(keyValuePair.Key))
                    {
                        _masterLookup.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                    else
                    {
                        _masterLookup[keyValuePair.Key].AddIndelEvidence(keyValuePair.Value);
                    }
                }
            }

            if (_masterOutcomesLookup != null)
            {
                var outcomes = _evaluator.GetIndelOutcomes();
                foreach (var outcome in outcomes)
                {
                    if (!_masterOutcomesLookup.TryGetValue(outcome.Key, out var outcomesForIndel))
                    {
                        outcomesForIndel = new int[8];
                        _masterOutcomesLookup.Add(outcome.Key, outcomesForIndel);
                    }

                    for (int i = 0; i < outcome.Value.Length; i++)
                    {
                        outcomesForIndel[i] += outcome.Value[i];
                    }

                }
            }
        }

        private static int UpdateAndAddAlignment(BamAlignment finalRead, bool realignedOther,
            BamAlignment finalReadOther,
            List<BamAlignment> reads, INmCalculator nmCalculator, bool recalculateNm)
        {
            int nm = 0;
            if (finalRead != null)
            {
                if (realignedOther)
                {
                    finalRead.MatePosition = finalReadOther.Position;
                }

                if (recalculateNm)
                {
                    nm = nmCalculator.GetNm(finalRead);
                    AddNmTag(finalRead, nm);
                }

                reads.Add(finalRead);
            }

            return nm;
        }

        private BamAlignment RealignSingleAlignment(BamAlignment bamAlignment, out bool realigned, RealignmentState state = null)
        {
            if (bamAlignment == null)
            {
                realigned = false;
                return null;
            }
            realigned = false;
            var forcedSoftclip = false;
            bool confirmed;
            bool sketchy;
            List<PreIndel> origIndels = _indelTargetFinder.FindIndels(bamAlignment, _chromosome);
            var realignedRead = _evaluator.GetFinalAlignment(bamAlignment, out realigned, out forcedSoftclip, out confirmed,  out sketchy, existingIndels: origIndels, state: state);

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
                    realignedRead.ReplaceOrAddStringTag("XD", newXd);
                }

            }

            if (realigned && !forcedSoftclip)
            {
                _evidenceCollector.CollectEvidence(realignedRead, true, false, _chromosome);
            }

            return realignedRead;
        }

        private static void AddNmTag(BamAlignment alignment, int nm)
        {
            if (nm > 0)
            {
                alignment.ReplaceOrAddIntTag("NM", nm, true);
            }
        }

    }

    public interface INmCalculator
    {
        int GetNm(BamAlignment alignment);
    }

    public class NmCalculator : INmCalculator
    {
        private IGenomeSnippetSource _genomeSnippetSource;

        public NmCalculator(IGenomeSnippetSource genomeSnippetSource)
        {
            _genomeSnippetSource = genomeSnippetSource;
        }

        public int GetNm(BamAlignment alignment)
        {
            var positionMap = new PositionMap(alignment.Bases.Length);
            Read.UpdatePositionMap(alignment.Position + 1, alignment.CigarData, positionMap);

            var snippet = _genomeSnippetSource.GetGenomeSnippet(alignment.Position);

            var numMismatches =
                Helper.GetNumMismatches(alignment.Bases, positionMap, snippet.Sequence, snippet.StartPosition);

            if (numMismatches == null)
            {
                throw new Exception($"Num mismatches is null: {alignment.Name} {alignment.Position} {alignment.CigarData} {string.Join(",",positionMap.Map)}");
            }

            var numIndelBases = alignment.CigarData.NumIndelBases();

            return numMismatches.Value + numIndelBases;

        }
    }
}
