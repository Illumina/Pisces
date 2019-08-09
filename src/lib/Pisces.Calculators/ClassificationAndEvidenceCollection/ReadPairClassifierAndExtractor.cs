using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Common.IO.Utility;
using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;
using Gemini.Types;
using Gemini.Utility;

namespace Gemini.ClassificationAndEvidenceCollection
{
    public class ReadPairClassifierAndExtractor : IReadPairClassifierAndExtractor
    {
        const int NumMismatchesToBeConsideredLikelySnvInStitched = 1;
        private readonly int _numMismatchesToBeConsideredMessy = 3;
        const int NumMismatchesToBeConsideredUnusableIfSplit = 20;
        private readonly int _numSoftclipsToBeConsideredMessy;
        private readonly int _minMapQuality;
        private readonly bool _trustSoftclips;
        private readonly bool _skipStitch;
        private readonly bool _treatAbnormalOrientationAsImproper;
        private readonly int _messyMapq;
        private readonly List<string> _tagsToKeepFromR1;
        private readonly bool _checkMd;

        public Dictionary<int, List<string>> SoftclipsEndingAtPosition = new Dictionary<int, List<string>>();
        public Dictionary<int, List<string>> SoftclipsStartingAtPosition = new Dictionary<int, List<string>>();

        public ReadPairClassifierAndExtractor(bool trustSoftclips, int minMapQuality = 10, bool skipStitch = false, bool treatAbnormalOrientationAsImproper = false, int messyMapq = 30, int numSoftclipsToBeConsideredMessy = 8, int numMismatchesToBeConsideredMessy = 3, List<string> stringTagsToKeepFromR1 = null, bool checkMd = false)
        {
            _trustSoftclips = trustSoftclips;
            _skipStitch = skipStitch;
            _minMapQuality = minMapQuality;
            _treatAbnormalOrientationAsImproper = treatAbnormalOrientationAsImproper;
            _messyMapq = messyMapq;
            _numSoftclipsToBeConsideredMessy = numSoftclipsToBeConsideredMessy;
            _numMismatchesToBeConsideredMessy = numMismatchesToBeConsideredMessy;
            _tagsToKeepFromR1 = stringTagsToKeepFromR1 ?? new List<string>();
            _checkMd = checkMd;
        }

        public PairResult GetBamAlignmentsAndClassification(ReadPair readPair, IReadPairHandler pairHandler)
        {
            if (readPair.PairStatus == PairStatus.Duplicate)
            {
                // TODO hasIndels and numMismatches and split don't have meaning in an unusable dup, but it's a bit misleading to set them..
                // Also, it's kind of silly to extract those alignments if we're going to set it to unusable anyway.
                var alignments = readPair.GetAlignments().ToList();

                return new PairResult(alignments: alignments, readPair: readPair, classification: PairClassification.Duplicate, hasIndels: false,
                    isSplit: false, numMismatchesInSingleton: 0, softclipLengthForIndelRead: 0)
                {
                    IsReputableIndelContaining = false
                };
            }

            if ((readPair.Read1 != null && readPair.Read1.CigarData.Count == 0) || (readPair.Read2 != null && readPair.Read2.CigarData.Count == 0))
            {
                return new PairResult(readPair.GetAlignments().ToList(), readPair, PairClassification.Unhandleable, false, false);
            }

            var classification = PairClassification.Unknown;
            IEnumerable<BamAlignment> bamAlignmentList = null;

            int? numMismatchesInR1 = null;
            int? numMismatchesInR2 = null;

            var r1HasIndels = OverlappingIndelHelpers.ReadContainsIndels(readPair.Read1);
            var r2HasIndels = OverlappingIndelHelpers.ReadContainsIndels(readPair.Read2);
            var hasIndels = r1HasIndels || r2HasIndels;

            if (IsCompletedPairedPair(readPair))
            {
                if (BothReadsHighQuality(readPair))
                {
                    numMismatchesInR1 = readPair.Read1.GetIntTag("NM");
                    numMismatchesInR2 = readPair.Read2.GetIntTag("NM");

                    var tryStitch = true;

                    if (hasIndels)
                    {
                        if (numMismatchesInR1 == null && numMismatchesInR2 == null)
                        {
                            Logger.WriteWarningToLog(
                                $"Found indel-containing read without NM: '{readPair.Name}', likely indicating that NM is not set on any read. Consider preprocessing the BAM to calculate NM tags for best results.");
                        }

                        return HandleIndelPairIfStitchUnallowed(readPair, numMismatchesInR1 ?? 0,
                            numMismatchesInR2 ?? 0, r1HasIndels, r2HasIndels);
                    }
                    else
                    {
                        // TODO if not realigning anything (or not realigning imperfects), go ahead and stitch immediately
                        // TODO why are we using this bool multiple times
                        // ^ Because there's no point checking for imperfectinos if we don't care about softclips -- we already know there are no indels because this is in the else of if(hasIndels)

                        if (!_trustSoftclips && (ReadContainsImperfections(readPair.Read1, _trustSoftclips) ||
                                                 ReadContainsImperfections(readPair.Read2, _trustSoftclips)))
                        {
                            tryStitch = false;
                            classification =
                                ClassifySoftclipContainingPairGivenSoftclipDistrust(readPair, numMismatchesInR1,
                                    numMismatchesInR2);
                            bamAlignmentList = readPair.GetAlignments();
                        }
                        else
                        {
                            if (numMismatchesInR1 == null)
                            {
                                numMismatchesInR1 = readPair.Read1.GetIntTag("NM");
                            }

                            if (numMismatchesInR2 == null)
                            {
                                numMismatchesInR2 = readPair.Read2.GetIntTag("NM");
                            }

                            if (numMismatchesInR1 >= _numMismatchesToBeConsideredMessy ||
                                numMismatchesInR2 >= _numMismatchesToBeConsideredMessy)
                            {
                                classification = PairClassification.UnstitchMessy;
                                tryStitch = false;

                                if (numMismatchesInR1 <= 1 || numMismatchesInR2 <= 1)
                                {
                                    // One of the reads is clean

                                    tryStitch = false;

                                    if (numMismatchesInR1 <= 1)
                                    {
                                        // R1 is the clean one.
                                        if (readPair.Read2.IsReverseStrand())
                                        {
                                            classification = PairClassification.UnstitchReverseMessy;
                                        }
                                        else
                                        {
                                            classification = PairClassification.UnstitchForwardMessy;
                                        }
                                    }
                                    else
                                    {
                                        if (readPair.Read1.IsReverseStrand())
                                        {
                                            classification = PairClassification.UnstitchReverseMessy;
                                        }
                                        else
                                        {
                                            classification = PairClassification.UnstitchForwardMessy;
                                        }
                                    }
                                }

                                bamAlignmentList = readPair.GetAlignments();
                            }
                            else if (numMismatchesInR1 + numMismatchesInR2 == 0)
                            {
                                classification = PairClassification.UnstitchPerfect;
                                bamAlignmentList = readPair.GetAlignments();
                            }
                            else if (numMismatchesInR1 <= 1 && numMismatchesInR2 <= 1)
                            {
                                classification = PairClassification.UnstitchSingleMismatch;
                                bamAlignmentList = readPair.GetAlignments();
                            }
                            else
                            {
                                classification = PairClassification.UnstitchImperfect;
                                bamAlignmentList = readPair.GetAlignments();
                            }
                        }

                        classification = AdjustClassificationForMultimapper(readPair, classification);
                    }

                    if (classification == PairClassification.UnstitchMessySuspiciousRead)
                    {
                        tryStitch = false;
                    }

                    if (_skipStitch)
                    {
                        tryStitch = false;
                    }

                    if (classification != PairClassification.UnstitchPerfect)
                    {
                        //For now we can't stitch anything else because we can't properly calculate NM!!
                        tryStitch = false;
                    }

                    if (!tryStitch)
                    {
                        if (bamAlignmentList == null)
                        {
                            bamAlignmentList = readPair.GetAlignments().ToList();
                            classification = PairClassification.Unstitchable;
                        }
                    }
                    else
                    {
                        bamAlignmentList = TryStitch(readPair, pairHandler, out classification);
                    }
                }
                else if (OneReadIsHighQuality(readPair))
                {
                    classification = PairClassification.Split;
                    if (hasIndels)
                    {
                        numMismatchesInR1 = numMismatchesInR1 ?? readPair.Read1?.GetIntTag("NM") ?? 0;
                        numMismatchesInR2 = numMismatchesInR2 ?? readPair.Read2?.GetIntTag("NM") ?? 0;

                        return HandlePairContainingIndels(readPair, r1HasIndels, r2HasIndels, numMismatchesInR1.Value,
                            numMismatchesInR2.Value, true, PairClassification.Split, true);
                    }
                }
                else
                {
                    classification = PairClassification.Unusable;
                    bamAlignmentList = readPair.GetAlignments().ToList();
                }

            }
            else
            {
                numMismatchesInR1 = numMismatchesInR1 ?? readPair.Read1?.GetIntTag("NM") ?? 0;
                numMismatchesInR2 = numMismatchesInR2 ?? readPair.Read2?.GetIntTag("NM") ?? 0;
                return ClassifyIncompletePair(readPair, r1HasIndels, r2HasIndels, numMismatchesInR1.Value, numMismatchesInR2.Value);
            }

            // TODO - not sure why I originally had this double-check on whether pairs were split? shouldn't this already be evident from the pair status?
            //var isSplit = bamAlignmentList?.Count() > 0 && bamAlignmentList?.Select(x => x.RefID).Distinct().Count() > 1;
            var isSplit = false; 
            if (isSplit || classification == PairClassification.Split || readPair.PairStatus == PairStatus.SplitChromosomes 
                || readPair.PairStatus == PairStatus.MateNotFound || readPair.PairStatus == PairStatus.MateUnmapped)
            {
                return HandleSplitNonIndelPair(readPair, bamAlignmentList, hasIndels, isSplit);
            }

            var pr = new PairResult(bamAlignmentList.ToList(), readPair, classification, hasIndels, isSplit);

            if (classification == PairClassification.UnstitchMessy || classification == PairClassification.UnstitchMessySuspiciousRead)
            {
                if (_checkMd && HasSuspiciousMd(readPair, numMismatchesInR1, numMismatchesInR2, pr))
                {
                    classification = PairClassification.UnstitchMessySuspiciousMd;
                    pr.Classification = classification;
                }
            }

            return pr;
        }



        private bool HasSuspiciousMd(ReadPair pair, int? nm1, int? nm2, PairResult pairResult = null)
        {
            try
            {
                // Assumption that this is only ever called on non-indel containing, paired reads. I know this to be the case but for future dev would be good to add checks in here.
                // ^ Sadly this assumption doesn't hold. I'm not sure whether it's valid MD format or not, but we've now seen reads come in with MDs that reflect indels and those indels have been softclipped away. So now we have to actively check.
                var md1 = pair.Read1.GetStringTag("MD");
                var md2 = pair.Read2.GetStringTag("MD");

                if (string.IsNullOrEmpty(md1) || string.IsNullOrEmpty(md2))
                {
                    return false;
                }

                var mdCounts1 = Helper.GetMdCountsWithSubstitutions(md1, pairResult.ReadPair.Read1.Bases,
                    pairResult.ReadPair.Read1.CigarData[0].Type == 'S'
                        ? (int) pairResult.ReadPair.Read1.CigarData[0].Length
                        : 0, (int)pairResult.ReadPair.Read1.CigarData.GetSuffixClip());
                var mdCounts2 = Helper.GetMdCountsWithSubstitutions(md2,
                    pairResult.ReadPair.Read2.Bases,
                    pairResult.ReadPair.Read2.CigarData[0].Type == 'S'
                        ? (int) pairResult.ReadPair.Read2.CigarData[0].Length
                        : 0, (int)pairResult.ReadPair.Read2.CigarData.GetSuffixClip());

                pairResult.md1 = mdCounts1;
                pairResult.md2 = mdCounts2;

                var mdTotal1 = mdCounts1.A + mdCounts1.T + mdCounts1.C + mdCounts1.G;
                var mdTotal2 = mdCounts2.A + mdCounts2.T + mdCounts2.C + mdCounts2.G;

                var numNs1 = mdTotal1 - nm1;
                var numNs2 = mdTotal2 - nm2;

                if (numNs1 > _numMismatchesToBeConsideredMessy || numNs2 > _numMismatchesToBeConsideredMessy)
                {
                    // We already know this pair is messy and now we find out that it's even messier than we thought: there are a bunch of Ns here.
                    return true;
                }

                if (pair.DontOverlap.HasValue)
                {
                    return false;
                }

                const int totalMdToBeConsideredSuspicious = 8;
                const int runLengthsConsideredSuspicious = 2;
                const int numInRunsToBeConsideredSuspicious = 4;

                var suspiciousBecauseOfTotalMd = mdTotal1 > totalMdToBeConsideredSuspicious ||
                                                 mdTotal2 > totalMdToBeConsideredSuspicious;

                var suspiciousBecauseOfMismatchesInRuns =
                    Math.Max(mdCounts1.NumInRuns, mdCounts2.NumInRuns) > numInRunsToBeConsideredSuspicious || 
                    Math.Max(mdCounts1.RunLength, mdCounts2.RunLength) > runLengthsConsideredSuspicious;

                if (suspiciousBecauseOfTotalMd || suspiciousBecauseOfMismatchesInRuns)
                {
                    // Only one of them has a very high mismatch count
                    if (OneMuchWorse(mdTotal1, mdTotal2, totalMdToBeConsideredSuspicious)) return true;

                    // There is an imbalance in the mismatches that are seen, suggesting that they are not the same mismatches
                    // We have to allow for a little wiggle-room because they're not always perfectly overlapping, but if, say, there are twice as many X>C's on one mate 1 as there are on mate 2, it may indicate a sequencing error. 
                    const int numMismatchesOfTypeToBeConsideredSuspicious = 4;
                    if (OneMuchWorse(mdCounts1.A, mdCounts2.A, numMismatchesOfTypeToBeConsideredSuspicious)) return true;
                    if (OneMuchWorse(mdCounts1.T, mdCounts2.T, numMismatchesOfTypeToBeConsideredSuspicious)) return true;
                    if (OneMuchWorse(mdCounts1.C, mdCounts2.C, numMismatchesOfTypeToBeConsideredSuspicious)) return true;
                    if (OneMuchWorse(mdCounts1.G, mdCounts2.G, numMismatchesOfTypeToBeConsideredSuspicious)) return true;
                    if (OneMuchWorse(mdCounts1.SubA, mdCounts2.SubA, numMismatchesOfTypeToBeConsideredSuspicious)) return true;
                    if (OneMuchWorse(mdCounts1.SubT, mdCounts2.SubT, numMismatchesOfTypeToBeConsideredSuspicious)) return true;
                    if (OneMuchWorse(mdCounts1.SubC, mdCounts2.SubC, numMismatchesOfTypeToBeConsideredSuspicious)) return true;
                    if (OneMuchWorse(mdCounts1.SubG, mdCounts2.SubG, numMismatchesOfTypeToBeConsideredSuspicious)) return true;
                }

                return false;
            }
            catch (ArgumentException e)
            {
                Logger.WriteWarningToLog(
                    $"Error parsing MD for {pair.Name}: {pair.Read1.CigarData} {pair.Read2.CigarData}, will treat as non-suspicious MD. Error details: {e.Message}");
                return false;
            }
        }

        private static bool OneMuchWorse(int count, int count2, int threshold)
        {
            if (count > count2)
            {
                if (count > threshold && count > count2 * 2)
                {
                    return true;
                }
            }
            else
            {
                if (count2 > threshold && count2 > count * 2)
                {
                    return true;
                }
            }
            
            return false;
        }

        private PairClassification AdjustClassificationForMultimapper(ReadPair readPair,
            PairClassification classification)
        {
            if (classification == PairClassification.UnstitchMessy || classification == PairClassification.UnstitchMessyIndel)
            {
                var hasIndels = classification == PairClassification.UnstitchMessyIndel;
                if (readPair.Read1 == null)
                {
                    throw new Exception("Null read 1");
                }
                if (readPair.Read2 == null)
                {
                    throw new Exception("Null read 1");
                }
                if (readPair.Read1.MapQuality < _messyMapq || readPair.Read2.MapQuality < _messyMapq)
                {
                    classification = hasIndels ? PairClassification.UnstitchMessyIndelSuspiciousRead : PairClassification.UnstitchMessySuspiciousRead;
                }
            }

            return classification;
        }

        private PairClassification ClassifySoftclipContainingPairGivenSoftclipDistrust(ReadPair readPair,
            int? numMismatchesInR1, int? numMismatchesInR2)
        {
            var classification = PairClassification.UnstitchImperfect;
            if (numMismatchesInR1 == null)
            {
                numMismatchesInR1 = readPair.Read1.GetIntTag("NM");
            }

            if (numMismatchesInR2 == null)
            {
                numMismatchesInR2 = readPair.Read2.GetIntTag("NM");
            }

            var r1Softclip = GatherR1SoftclipInfo(readPair.Read1);

            var r2Softclip = GatherR2SoftclipInfo(readPair.Read2);
            // Consider it messy if it has softclips and mismatches, or has softclip that is long
            var r1IsMessy = r1Softclip >= _numSoftclipsToBeConsideredMessy || numMismatchesInR1 > 1 && r1Softclip > 0 || numMismatchesInR1 >= _numMismatchesToBeConsideredMessy;
            var r2IsMessy = r2Softclip >= _numSoftclipsToBeConsideredMessy || numMismatchesInR2 > 1 && r2Softclip > 0 || numMismatchesInR2 >= _numMismatchesToBeConsideredMessy;
            var r1IsVeryClean = !r1IsMessy && r1Softclip == 0 && numMismatchesInR1 <= 2;
            var r2IsVeryClean = !r2IsMessy && r2Softclip == 0 && numMismatchesInR2 <= 2;

            if (r1IsMessy || r2IsMessy)
            {
                classification = PairClassification.UnstitchMessy;

                if (r2IsMessy && r1IsVeryClean)
                {
                    // Read 2 is the messy one
                    if (readPair.Read2.IsReverseStrand())
                    {
                        classification = PairClassification.UnstitchReverseMessy;
                    }
                    else
                    {
                        classification = PairClassification.UnstitchForwardMessy;
                    }
                }
                else if (r1IsMessy && r2IsVeryClean)
                {
                    // Read 1 is the messy one
                    if (readPair.Read1.IsReverseStrand())
                    {
                        classification = PairClassification.UnstitchReverseMessy;
                    }
                    else
                    {
                        classification = PairClassification.UnstitchForwardMessy;
                    }
                }
            }

            classification = AdjustClassificationForMultimapper(readPair, classification);
            return classification;
        }

        private long GatherR2SoftclipInfo(BamAlignment read)
        {
            var r2Softclip = read.CigarData.HasSoftclips
                ? (read.CountBasesWithOperationType('S'))
                : 0;

            if (r2Softclip > 15)
            {
                var prefixClip = read.CigarData.GetPrefixClip();
                if (prefixClip > 15)
                {
                    var softclipEnd = read.Position;
                    if (!SoftclipsEndingAtPosition.ContainsKey(softclipEnd))
                    {
                        SoftclipsEndingAtPosition.Add(softclipEnd, new List<string>());
                    }

                    SoftclipsEndingAtPosition[softclipEnd].Add(read.Bases.Substring(0, (int) prefixClip));
                }

                var suffixClip = (int) read.CigarData.GetSuffixClip();
                if (suffixClip > 15)
                {
                    var softclipStart = read.EndPosition;
                    if (!SoftclipsStartingAtPosition.ContainsKey(softclipStart))
                    {
                        SoftclipsStartingAtPosition.Add(softclipStart, new List<string>());
                    }

                    SoftclipsStartingAtPosition[softclipStart]
                        .Add(read.Bases.Substring(read.Bases.Length - suffixClip - 1, (int) suffixClip));
                }
            }

            return r2Softclip;
        }

        private long GatherR1SoftclipInfo(BamAlignment read)
        {
            var r1Softclip = read.CigarData.HasSoftclips
                ? (read.CountBasesWithOperationType('S'))
                : 0;

            if (r1Softclip > 15)
            {
                var prefixClip = read.CigarData.GetPrefixClip();
                if (prefixClip > 15)
                {
                    var softclipEnd = read.Position;
                    if (!SoftclipsEndingAtPosition.ContainsKey(softclipEnd))
                    {
                        SoftclipsEndingAtPosition.Add(softclipEnd, new List<string>());
                    }

                    SoftclipsEndingAtPosition[softclipEnd].Add(read.Bases.Substring(0, (int) prefixClip));
                }

                var suffixClip = (int) read.CigarData.GetSuffixClip();
                if (suffixClip > 15)
                {
                    var softclipStart = read.EndPosition;
                    if (!SoftclipsStartingAtPosition.ContainsKey(softclipStart))
                    {
                        SoftclipsStartingAtPosition.Add(softclipStart, new List<string>());
                    }

                    SoftclipsStartingAtPosition[softclipStart]
                        .Add(read.Bases.Substring(read.Bases.Length - suffixClip, (int) suffixClip));
                }
            }

            return r1Softclip;
        }

        private PairResult ClassifyIncompletePair(ReadPair readPair, bool r1HasIndels, bool r2HasIndels, int r1Nm, int r2Nm)
        {
            var hasIndels = r1HasIndels || r2HasIndels;
            var classification = PairClassification.Unstitchable;
            if (readPair.PairStatus == PairStatus.LongFragment)
            {
                classification = PairClassification.LongFragment;
            }

            var isImproper = readPair.IsImproper || (_treatAbnormalOrientationAsImproper && !readPair.NormalPairOrientation);
            if (isImproper)
            {
                classification = PairClassification.Improper;
            }

            if (readPair.Read1 != null)
            {
                GatherR1SoftclipInfo(readPair.Read1);
            }

            if (readPair.Read2 != null)
            {
                GatherR2SoftclipInfo(readPair.Read2);
            }


            if (readPair.NumPrimaryReads == 1)
            {
                //{
                //    var prefixClip = readPair.Read1.CigarData.GetPrefixClip();
                //    if (prefixClip > 15)
                //    {
                //        var softclipEnd = readPair.Read1.Position;
                //        if (!SoftclipsEndingAtPosition.ContainsKey(softclipEnd))
                //        {
                //            SoftclipsEndingAtPosition.Add(softclipEnd, new List<string>());
                //        }

                //        SoftclipsEndingAtPosition[softclipEnd].Add(readPair.Read1.Bases.Substring(0, (int)prefixClip));
                //    }
                //}
                classification = isImproper ? PairClassification.Improper : PairClassification.UnstitchableAsSingleton;
                if (hasIndels)
                {
                    classification = isImproper ? PairClassification.IndelImproper : PairClassification.IndelSingleton;
                }
            }
            else if (hasIndels)
            {
                classification = PairClassification.UnstitchIndel;
                if (readPair.IsImproper 
                    //|| readPair.MaxPosition - readPair.MinPosition > 1000
                    )
                {
                    classification = PairClassification.IndelImproper;
                }
            }

            if (hasIndels)
            {
                return HandlePairContainingIndels(readPair, r1HasIndels, r2HasIndels, r1Nm, r2Nm, hasIndels, classification, false);
            }
            else
            {
                return new PairResult(alignments: readPair.GetAlignments(), readPair: readPair,
                    classification: classification, hasIndels: false,
                    isSplit: false, numMismatchesInSingleton: Math.Max(r1Nm, r2Nm), softclipLengthForIndelRead: 0)
                {
                    R1Nm = r1Nm,
                    R2Nm = r2Nm
                };

            }
        }

        private PairResult HandlePairContainingIndels(ReadPair readPair, bool r1HasIndels, bool r2HasIndels, int r1Nm,
            int r2Nm,
            bool hasIndels, PairClassification classification, bool isSplit, IEnumerable<BamAlignment> bamAlignmentList = null)
        {
            var effectiveSc = 0;
            var effectiveNm = 0;
            var r1TotMismatchEvents = 0;
            var r2TotMismatchEvents = 0;
            if (r1HasIndels)
            {
                var r1Sc = _trustSoftclips ? 0 :
                    readPair.Read1.CigarData.HasSoftclips ? readPair.Read1.CountBasesWithOperationType('S') : 0;
                var r1NumIndels = (int) readPair.Read1.CountBasesWithOperationType('I') +
                                  (int) readPair.Read1.CountBasesWithOperationType('D');
                var r1NumIndelEvents =
                    readPair.Read1.NumOperationsOfType('D') + readPair.Read1.NumOperationsOfType('I');
                r1Nm = Math.Max(0, r1Nm - r1NumIndels);
                r1TotMismatchEvents = r1Nm + r1NumIndelEvents;

                if (r2HasIndels)
                {
                    var r2Sc = _trustSoftclips ? 0 :
                        readPair.Read2.CigarData.HasSoftclips ? readPair.Read2.CountBasesWithOperationType('S') : 0;

                    var r2NumIndels = (int) readPair.Read2.CountBasesWithOperationType('I') +
                                      (int) readPair.Read2.CountBasesWithOperationType('D');

                    var r2NumIndelEvents =
                        readPair.Read2.NumOperationsOfType('D') + readPair.Read2.NumOperationsOfType('I');

                    r2Nm = Math.Max(0, r2Nm - r2NumIndels);

                    r2TotMismatchEvents = r2Nm + r2NumIndelEvents;
                    effectiveNm = Math.Min(r1Nm, r2Nm);
                    effectiveSc = (int) Math.Min(r1Sc, r2Sc);
                }
                else
                {
                    r2TotMismatchEvents = r2Nm;
                    effectiveNm = r1Nm;
                    effectiveSc = (int) r1Sc;
                }
            }
            else if (r2HasIndels)
            {
                var r2Sc = _trustSoftclips ? 0 :
                    readPair.Read2.CigarData.HasSoftclips ? readPair.Read2.CountBasesWithOperationType('S') : 0;
                var r2NumIndelBases = (int) readPair.Read2.CountBasesWithOperationType('I') +
                                  (int) readPair.Read2.CountBasesWithOperationType('D');

                var r2NumIndelEvents =
                    readPair.Read2.NumOperationsOfType('D') + readPair.Read2.NumOperationsOfType('I');
                r2Nm = Math.Max(0, r2Nm - r2NumIndelBases);
                effectiveNm = r2Nm;
                effectiveSc = (int) r2Sc;
                r2TotMismatchEvents = r2Nm + r2NumIndelEvents;


                r1TotMismatchEvents = r1Nm;
            }

            if ((Math.Max(r1TotMismatchEvents, r2TotMismatchEvents)) > _numMismatchesToBeConsideredMessy && 
                (classification == PairClassification.UnstitchIndel || classification == PairClassification.Disagree))
            {
                classification = PairClassification.UnstitchMessyIndel;
                {
                    if (r1TotMismatchEvents <= 2)
                    {
                        // Only r2 is messy
                        classification = readPair.Read2.IsReverseStrand()
                            ? PairClassification.UnstitchReverseMessyIndel
                            : PairClassification.UnstitchForwardMessyIndel;
                    }
                    else if (r2TotMismatchEvents <= 2)
                    {
                        // Only r1 is messy
                        classification = readPair.Read1.IsReverseStrand()
                            ? PairClassification.UnstitchReverseMessyIndel
                            : PairClassification.UnstitchForwardMessyIndel;

                    }
                }

                classification = AdjustClassificationForMultimapper(readPair, classification);
            }

            var isReputable = effectiveNm < 3 && (_trustSoftclips || effectiveSc < 10);

            return new PairResult(alignments: bamAlignmentList ?? readPair.GetAlignments(), readPair: readPair,
                classification: classification, hasIndels: hasIndels,
                isSplit: isSplit, numMismatchesInSingleton: effectiveNm, softclipLengthForIndelRead: effectiveSc)
            {
                R1Nm = r1Nm,
                R2Nm = r2Nm,
                IsReputableIndelContaining = isReputable
            };
        }

        private PairResult HandleIndelPairIfStitchUnallowed(ReadPair readPair, int numMismatchesInR1, int numMismatchesInR2, bool r1HasIndels, bool r2HasIndels)
        {
            IEnumerable<BamAlignment> bamAlignmentList = null;
            PairClassification classification;

            var read1 = readPair.Read1;
            var read2 = readPair.Read2;

            if (read1.EndPosition >= read2.Position)
            {
                bamAlignmentList = OverlappingIndelHelpers.IndelsDisagreeWithStrongMate(
                    read1, read2, out bool disagree, 3, false);

                // TODO allow to stitch if they don't disagree, as they may not necessarily get the chance later (either user is not using realigner, or there are no indels strong enough to realign against)
                // Alternatively, if there are no indels to realign against, still stitch stuff if we can! (handle this in the realigner)
                // For the cases where we want to skip realignment, either tell it to stitch here (configurable), or have it still go through realigner but not realign?
                if (disagree)
                {
                    classification = PairClassification.Disagree;
                }
                else
                {
                    classification = PairClassification.UnstitchIndel;
                }
            }
            else
            {
                classification = PairClassification.UnstitchIndel;
            }

            return HandlePairContainingIndels(readPair, r1HasIndels, r2HasIndels, numMismatchesInR1, numMismatchesInR2,
                r1HasIndels || r2HasIndels, classification, false, bamAlignmentList);
        }

        private PairResult HandleSplitNonIndelPair(ReadPair readPair, IEnumerable<BamAlignment> bamAlignmentList,
            bool hasIndels, bool isSplit)
        {
            int numMismatchesInSingleton = 0;

            PairClassification classification;
            if (bamAlignmentList == null || !bamAlignmentList.Any())
            {
                bamAlignmentList = readPair.GetAlignments().ToList();
            }

            if (!bamAlignmentList.Any())
            {
                classification = PairClassification.Unusable;
            }
            else
            {
                classification = PairClassification.Split;
                if (readPair.PairStatus == PairStatus.SplitQuality)
                {
                    classification = PairClassification.Unstitchable;
                }
                if (bamAlignmentList.Count() == 1 && bamAlignmentList.First().MapQuality < _minMapQuality)
                {
                    classification = PairClassification.UnusableSplit;
                }
                else
                {
                    var nms = bamAlignmentList.Select(b => b.GetIntTag("NM") ?? 0);
                    numMismatchesInSingleton = nms.Max();
                    if (numMismatchesInSingleton > NumMismatchesToBeConsideredUnusableIfSplit)
                    {
                        // TODO perhaps make these unmapped? Or just adjust the mapq? Or eventually softclip? It's just inconvenient to do the softclipping here because we don't have the reference sequence. Perhaps we could just softclip the last N bases of the read though?
                        // Making them unusable and skipping them ended up hurting recall, as one might imagine. Not by a ton, and you do see precision go up by a similar amount, but need to think about this.
                        // TODO look into what TPs were lost between commit a8b and ce3.
                        classification = PairClassification.UnusableSplit;
                    }
                    else if (hasIndels || numMismatchesInSingleton > _numMismatchesToBeConsideredMessy)
                    {
                        classification = PairClassification.MessySplit;
                    }
                }
            }

            return new PairResult(alignments: bamAlignmentList.ToList(), readPair: readPair,
                classification: classification, hasIndels: hasIndels,
                isSplit: isSplit, numMismatchesInSingleton: numMismatchesInSingleton, softclipLengthForIndelRead: 0);
        }

        private bool OneReadIsHighQuality(ReadPair readPair)
        {
            return readPair.Read1.MapQuality >= _minMapQuality || readPair.Read2.MapQuality >= _minMapQuality;
        }

        private bool BothReadsHighQuality(ReadPair readPair)
        {
            return readPair.Read1.MapQuality >= _minMapQuality && readPair.Read2.MapQuality >= _minMapQuality;
        }

        private static bool IsCompletedPairedPair(ReadPair readPair)
        {
            return readPair.IsComplete(false) && readPair.PairStatus == PairStatus.Paired && !readPair.IsImproper;
        }

        private IEnumerable<BamAlignment> TryStitch(ReadPair readPair, IReadPairHandler pairHandler, out PairClassification classification)
        {
            // TODO if we end up allowing NM calculation in here, this will become true. 
            const bool allowStitchingOnImperfectReads = false;
            IEnumerable<BamAlignment> bamAlignmentList = pairHandler.ExtractReads(readPair);
            var bamAlignmentList2 = bamAlignmentList.ToList();

            if (bamAlignmentList2.Count == 1)
            {
                readPair.Stitched = true;
                classification = PairClassification.PerfectStitched;

                if (allowStitchingOnImperfectReads)
                {
                    var stitchedResult = bamAlignmentList2[0];
                    int? nm = 0;
                    //TODO handle this if it is a hit on performance. Making it simple for now because the previous logic where we were lazy evaluating was a bit skewed
                    var containsImperfections = ReadContainsImperfections(stitchedResult, _trustSoftclips);
                    //nm = stitchedResult.GetIntTag("NM"); // TODO reinstate this if stitched read has proper NM

                    var numMismatchesInR1 = readPair.Read1.GetIntTag("NM");
                    var numMismatchesInR2 = readPair.Read2.GetIntTag("NM");
                    if (containsImperfections ||
                        (nm > 0 || numMismatchesInR1 > 0 || numMismatchesInR2 > 0))
                    {
                        classification = PairClassification.ImperfectStitched;

                        if (numMismatchesInR1 <= NumMismatchesToBeConsideredLikelySnvInStitched &&
                            numMismatchesInR2 <= NumMismatchesToBeConsideredLikelySnvInStitched &&
                            !containsImperfections)
                        {
                            classification = PairClassification.SingleMismatchStitched;
                        }
                        else if (nm >= _numMismatchesToBeConsideredMessy ||
                                 numMismatchesInR1 >= _numMismatchesToBeConsideredMessy ||
                                 numMismatchesInR2 >= _numMismatchesToBeConsideredMessy)
                        {
                            classification = PairClassification.MessyStitched;
                        }
                    }
                }

                foreach (var alignment in bamAlignmentList)
                {
                    foreach (var tag in _tagsToKeepFromR1)
                    {
                        var r1Tag = readPair.Read1.GetStringTag(tag);
                        if (r1Tag != null)
                        {
                            alignment.ReplaceOrAddStringTag(tag, r1Tag);
                        }
                    }

                }

            }
            else
            {
                classification = PairClassification.FailStitch;
            }

            return bamAlignmentList;
        }

        private bool ReadContainsImperfections(BamAlignment alignment, bool trustSoftClips)
        {
            if (alignment == null)
            {
                return false;
            }

            if (alignment.CigarData.HasIndels || (!trustSoftClips && alignment.CigarData.HasSoftclips))
            {
                return true;
            }

            return false;

        }
    }


    public struct MdCounts
    {
        public int SubA { get; }
        public int SubT { get; }
        public int SubC { get; }
        public int SubG { get; }
        public int SubN { get; }

        public int A { get; }
        public int T { get; }
        public int C { get; }
        public int G { get; }
        public int RunLength { get; }
        public int NumInRuns { get; }
        public bool IsSet { get; }

        public MdCounts(int A, int T, int C, int G, int runLength, int numInRuns, int subA = 0, int subT = 0, int subC = 0, int subG = 0, int subN = 0)
        {
            this.A = A;
            this.T = T;
            this.C = C;
            this.G = G;
            RunLength = runLength;
            NumInRuns = numInRuns;

            SubA = subA;
            SubC = subC;
            SubT = subT;
            SubG = subG;
            SubN = subN;
            IsSet = true;
        }
    }
}