using System;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Common.IO.Utility;

namespace StitchingLogic
{
    public class BasicStitcher : IAlignmentStitcher
    {
        private readonly bool _useSoftclippedBases;
        private readonly bool _debug;
        private ReadStatusCounter _statusCounter;
        private readonly bool _nifyUnstitchablePairs;
        private readonly bool _ignoreProbeSoftclips;
        private readonly CigarReconciler _cigarReconciler;
        private readonly ReadMerger _readMerger;


        public BasicStitcher(int minBaseCallQuality, bool nifyDisagreements = true, bool useSoftclippedBases = true, bool debug = false, 
            bool nifyUnstitchablePairs = false, bool allowRescuedInsertionBaseDisagreement = false, bool ignoreProbeSoftclips = true, int maxReadLength = 1024, 
            bool ignoreReadsAboveMaxLength = false) 
        {
            _nifyUnstitchablePairs = nifyUnstitchablePairs;
            _ignoreProbeSoftclips = ignoreProbeSoftclips;
            _useSoftclippedBases = useSoftclippedBases;
            _debug = debug;
            _statusCounter = new ReadStatusCounter();
            _cigarReconciler = new CigarReconciler(_statusCounter, _useSoftclippedBases, _debug, _ignoreProbeSoftclips, maxReadLength, minBaseCallQuality, ignoreReadsAboveMaxStitchedLength: ignoreReadsAboveMaxLength, nifyDisagreements: nifyDisagreements);
            _readMerger = new ReadMerger(minBaseCallQuality, allowRescuedInsertionBaseDisagreement, _useSoftclippedBases, 
                nifyDisagreements, _statusCounter, _debug, _ignoreProbeSoftclips);
        }
  
        public bool TryStitch(AlignmentSet set)
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

                    if (stitchingInfo != null)
                    {
                        var stitchedCigar = stitchingInfo.StitchedCigar;

                        if (stitchedCigar == null)
                            // there's an overlap but we can't figure out the cigar - TODO revisit
                        {
                            _statusCounter.AddDebugStatusCount("Reads overlap but we can't figure out the cigar");
                            return false;
                        }

                        // Returns null if unable to generate consensus
                        var mergedRead = _readMerger.GenerateConsensusRead(set.PartnerRead1, set.PartnerRead2,
                            stitchingInfo, set.IsOutie);

                        if (mergedRead != null)
                        {
                            mergedRead.BamAlignment.RefID = set.PartnerRead1.BamAlignment.RefID;
                            mergedRead.IsDuplex = set.PartnerRead1.IsDuplex || set.PartnerRead2.IsDuplex;
                            mergedRead.CigarDirections = stitchingInfo.StitchedDirections;
                            mergedRead.BamAlignment.MapQuality = Math.Max(set.PartnerRead1.MapQuality,
                                set.PartnerRead2.MapQuality);

                            set.ReadsForProcessing.Add(mergedRead);

                            _statusCounter.AddDebugStatusCount("Reads succesfully merge");
                            return true;
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
                    // Give a merged, Nified read if the pairs are stitchable (i.e. overlap) but conflicting
                    var mergedRead = _readMerger.GenerateNifiedMergedRead(set, _useSoftclippedBases);
                    mergedRead.BamAlignment.RefID = set.PartnerRead1.BamAlignment.RefID;
                    mergedRead.IsDuplex = set.PartnerRead1.IsDuplex || set.PartnerRead2.IsDuplex;
                    mergedRead.BamAlignment.MapQuality = Math.Max(set.PartnerRead1.MapQuality,
                        set.PartnerRead2.MapQuality);
                    set.ReadsForProcessing.Add(mergedRead);

                    _statusCounter.AddDebugStatusCount("Unstitchable pair N-ified");
                    return true;
                }
                else
                {
                    _statusCounter.AddDebugStatusCount("Unstitchable pair returned individually");
                    set.ReadsForProcessing.Add(set.PartnerRead1);
                    set.ReadsForProcessing.Add(set.PartnerRead2);
                }
                return false;
            }
            catch (Exception e)
            {
                throw new Exception("Stitching failed for read '" + set.PartnerRead1.Name + "': " + e.Message);
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