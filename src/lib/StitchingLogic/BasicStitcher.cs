using System;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Common.IO.Utility;
using Pisces.Domain.Utility;
using Alignment.Domain.Sequencing;
using System.Collections.Generic;

namespace StitchingLogic
{
    public struct StitchingResult
    {
        public readonly bool Stitched;
        public readonly int NumDisagreements;
        public readonly int NumAgreements;
        public readonly int NumNDisagreements;

        public StitchingResult(bool stitched, int numAgreements, int numDisagreements, int numNDisagreements)
        {
            Stitched = stitched;
            NumAgreements = numAgreements;
            NumDisagreements = numDisagreements;
            NumNDisagreements = numNDisagreements;
        }
    }

    public class BasicStitcher : IAlignmentStitcher
    {
        private const string UnstitchablePairUnableToNifyReturnedIndividually = "Unstitchable pair unable to Nify, returned individually";
        private const string UnstitchablePairReturnedIndividually = "Unstitchable pair returned individually";
        private const string UnstitchablePairNIfied = "Unstitchable pair N-ified";
        private const string ReadsSuccesfullyMerge = "Reads succesfully merge";
        private const string OverlappingBasesAreRepeatCannotReliablyStitch = "Overlapping bases are repeat, cannot reliably stitch";
        private const string ReadsOverlapButWeCanTFigureOutTheCigar = "Reads overlap but we can't figure out the cigar";
        private readonly bool _useSoftclippedBases;
        private readonly bool _debug;
        private ReadStatusCounter _statusCounter;
        private uint _minMapQuality;
        private readonly bool _dontStitchHomopolymerBridge;
        private readonly bool _nifyUnstitchablePairs;
        private readonly bool _ignoreProbeSoftclips;
        private readonly CigarReconciler _cigarReconciler;
        private readonly ReadMerger _readMerger;
        private readonly int _thresholdNumDisagreeingBases;
        private readonly bool _countNsTowardNumDisagreeingBases;

        public BasicStitcher(int minBaseCallQuality, bool nifyDisagreements = true, bool useSoftclippedBases = true, bool debug = false,
            bool nifyUnstitchablePairs = false, bool allowRescuedInsertionBaseDisagreement = false, bool ignoreProbeSoftclips = true, int maxReadLength = 1024,
            bool ignoreReadsAboveMaxLength = false, uint minMapQuality = 1, bool dontStitchHomopolymerBridge = true, int thresholdNumDisagreeingBases = int.MaxValue, bool countNsTowardNumDisagreeingBases = false)
        {
            _nifyUnstitchablePairs = nifyUnstitchablePairs;
            _ignoreProbeSoftclips = ignoreProbeSoftclips;
            _useSoftclippedBases = useSoftclippedBases;
            _debug = debug;
            _minMapQuality = minMapQuality;
            _dontStitchHomopolymerBridge = dontStitchHomopolymerBridge;
            _statusCounter = new ReadStatusCounter();
            _cigarReconciler = new CigarReconciler(_statusCounter, _useSoftclippedBases, _debug, _ignoreProbeSoftclips, maxReadLength, minBaseCallQuality, ignoreReadsAboveMaxStitchedLength: ignoreReadsAboveMaxLength, nifyDisagreements: nifyDisagreements);
            _readMerger = new ReadMerger(minBaseCallQuality, allowRescuedInsertionBaseDisagreement, _useSoftclippedBases,
                nifyDisagreements, _statusCounter, _debug, _ignoreProbeSoftclips, _countNsTowardNumDisagreeingBases);
            _thresholdNumDisagreeingBases = thresholdNumDisagreeingBases;
            _countNsTowardNumDisagreeingBases = countNsTowardNumDisagreeingBases;
        }

        public StitchingResult TryStitch(AlignmentSet set)
        {
            try
            {
                if (set.PartnerRead1 == null || set.PartnerRead2 == null)
                    throw new ArgumentException("Set has missing read.");

                if (IsStitchable(set))
                {
                    // Assumption is that exactly one read is first mate
                    var r1IsFirstMate = !set.PartnerRead2.IsFirstMate;

                    // Assumption is that exactly one read is forward and one read is reverse, and each component read is only one direction
                    // GetStitchedCigar returns null if cigars can't possibly agree
                    var stitchingInfo = _cigarReconciler.GetStitchedCigar(set.PartnerRead1.CigarData,
                        set.PartnerRead1.Position,
                        set.PartnerRead2.CigarData, set.PartnerRead2.Position,
                        set.PartnerRead1.SequencedBaseDirectionMap.First() == DirectionType.Reverse, set.IsOutie, set.PartnerRead1.Sequence, set.PartnerRead2.Sequence, set.PartnerRead1.Qualities, set.PartnerRead2.Qualities, r1IsFirstMate);

                    if (stitchingInfo!= null && stitchingInfo.NumDisagreeingBases + (_countNsTowardNumDisagreeingBases ? stitchingInfo.NumNDisagreements : 0) > _thresholdNumDisagreeingBases)
                    {
                        stitchingInfo = null;
                    }

                    if (stitchingInfo != null)
                    {
                        var stitchedCigar = stitchingInfo.StitchedCigar;

                        if (stitchedCigar == null)
                        // there's an overlap but we can't figure out the cigar - TODO revisit
                        {
                            AddDebugStatusCount(ReadsOverlapButWeCanTFigureOutTheCigar);
                            //_statusCounter.AddDebugStatusCount(ReadsOverlapButWeCanTFigureOutTheCigar);
                            return new StitchingResult(false, stitchingInfo.NumDisagreeingBases, stitchingInfo.NumDisagreeingBases, stitchingInfo.NumNDisagreements);
                        }

                        // Returns null if unable to generate consensus
                        var mergedRead = stitchingInfo.IsSimple ? _readMerger.GenerateConsensusRead(set.PartnerRead1, set.PartnerRead2, stitchingInfo, set.IsOutie)  : 
                            _readMerger.GenerateConsensusRead(set.PartnerRead1, set.PartnerRead2,
                            stitchingInfo, set.IsOutie);

                        if (mergedRead != null)
                        {

                            mergedRead.BamAlignment.RefID = set.PartnerRead1.BamAlignment.RefID;
                            mergedRead.IsDuplex = set.PartnerRead1.IsDuplex || set.PartnerRead2.IsDuplex;
                            mergedRead.CigarDirections = stitchingInfo.StitchedDirections;
                            mergedRead.BamAlignment.MapQuality = Math.Max(set.PartnerRead1.MapQuality,
                                set.PartnerRead2.MapQuality);

                            if (_dontStitchHomopolymerBridge)
                            {
                                var bridgeAnchored = stitchingInfo.IsSimple ? 
                                    OverlapEvaluator.BridgeAnchored(stitchingInfo.OverlapBases) : OverlapEvaluator.BridgeAnchored(mergedRead);
                                if (!bridgeAnchored)
                                {
                                    AddDebugStatusCount(OverlappingBasesAreRepeatCannotReliablyStitch);
                                    //_statusCounter.AddDebugStatusCount(OverlappingBasesAreRepeatCannotReliablyStitch);
                                    return new StitchingResult(false, stitchingInfo.NumAgreements, stitchingInfo.NumDisagreeingBases, stitchingInfo.NumNDisagreements);
                                }
                            }

                            set.ReadsForProcessing.Add(mergedRead);

                            AddDebugStatusCount(ReadsSuccesfullyMerge);
                            //_statusCounter.AddDebugStatusCount(ReadsSuccesfullyMerge);

                            return new StitchingResult(true, stitchingInfo.NumAgreements, stitchingInfo.NumDisagreeingBases, stitchingInfo.NumNDisagreements);
                        }

                    }
                }

                // If we didn't return true already, stitching failed.
                if (_debug)
                {
                    Logger.WriteToLog("Stitching failed on read " + set.PartnerRead1.Name);
                }

                if (_nifyUnstitchablePairs && IsStitchable(set))
                {
                    // TODO consider removing this functionality.
                    // Give a merged, Nified read if the pairs are stitchable (i.e. overlap) but conflicting
                    try
                    {
                        var mergedRead = _readMerger.GenerateNifiedMergedRead(set, _useSoftclippedBases);
                        mergedRead.BamAlignment.RefID = set.PartnerRead1.BamAlignment.RefID;
                        mergedRead.IsDuplex = set.PartnerRead1.IsDuplex || set.PartnerRead2.IsDuplex;
                        mergedRead.BamAlignment.MapQuality = Math.Max(set.PartnerRead1.MapQuality,
                            set.PartnerRead2.MapQuality);
                        set.ReadsForProcessing.Add(mergedRead);

                        AddDebugStatusCount(UnstitchablePairNIfied);
                        //_statusCounter.AddDebugStatusCount(UnstitchablePairNIfied);
                        return new StitchingResult(true, 0, 0, 0);
                    }
                    catch (Exception e)
                    {
                        Logger.WriteExceptionToLog(e);
                        //_statusCounter.AddDebugStatusCount(UnstitchablePairUnableToNifyReturnedIndividually);
                        AddDebugStatusCount(UnstitchablePairUnableToNifyReturnedIndividually);
                        set.ReadsForProcessing.Add(set.PartnerRead1);
                        set.ReadsForProcessing.Add(set.PartnerRead2);
                    }
                }
                else
                {
                    AddDebugStatusCount(UnstitchablePairReturnedIndividually);
                    //_statusCounter.AddDebugStatusCount(UnstitchablePairReturnedIndividually);
                    set.ReadsForProcessing.Add(set.PartnerRead1);
                    set.ReadsForProcessing.Add(set.PartnerRead2);
                }
                return new StitchingResult(false, 0,0,0);
            }
            catch (Exception e)
            {
                throw new Exception("Stitching failed for read '" + set.PartnerRead1.Name + "': " + e.Message + "..." + e.StackTrace, e.InnerException);
            }

        }

        private void AddDebugStatusCount(string status)
        {
            if (_debug)
            {
                _statusCounter.AddDebugStatusCount(status);
            }

        }
   
        public ReadStatusCounter GetStatusCounter()
        {
            return _statusCounter;
        }

        public void SetStatusCounter(ReadStatusCounter counter)
        {
            _statusCounter = counter;
        }

        private bool IsStitchable(AlignmentSet set)
        {
            return (set.PartnerRead1.Chromosome == set.PartnerRead2.Chromosome) &&
                   (_useSoftclippedBases ? set.PartnerRead1.ClipAdjustedEndPosition >= set.PartnerRead2.ClipAdjustedPosition : set.PartnerRead1.EndPosition >= set.PartnerRead2.Position);
        }
    }
}