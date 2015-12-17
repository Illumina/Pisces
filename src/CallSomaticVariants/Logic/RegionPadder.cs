using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic.RegionState;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Logic
{
    public class RegionPadder : IRegionPadder
    {
        private ChrReference _chrReference;
        public ChrIntervalSet IntervalSet { get; private set; }
        private int _lastClearedPosition = 0;

        /// <summary>
        /// Sole job is to pad empty reference calls when using intervals.  Assumes batch has already included reference calls (either empty or not) 
        /// for cleared regions.
        /// </summary>
        /// <param name="chrReference"></param>
        /// <param name="includeReferenceCalls"></param>
        /// <param name="intervals"></param>
        public RegionPadder(ChrReference chrReference, ChrIntervalSet intervals)
        {
            _chrReference = chrReference;
            IntervalSet = intervals;
        }

        public void Pad(ICandidateBatch batch, bool mapAll = false)
        {
            if (IntervalSet == null || (batch.ClearedRegions == null && !mapAll))  // nothing to do
                return;

            var startPosition = Math.Max(_lastClearedPosition + 1, IntervalSet.MinPosition);

            var endPosition = mapAll ? IntervalSet.MaxPosition
                : Math.Min(batch.ClearedRegions.Max(c => c.EndPosition), IntervalSet.MaxPosition);

            if (startPosition > endPosition)
                return;

            var nonClearedIntervals = IntervalSet.GetClipped(new Region(startPosition, endPosition), batch.ClearedRegions);

            AddMissingReferences(batch, nonClearedIntervals);

            _lastClearedPosition = endPosition;
        }

        public void AddMissingReferences(ICandidateBatch batch, List<Region> intervalsToAdd)
        {
            for (var i = 0; i < intervalsToAdd.Count; i ++)
            {
                var interval = intervalsToAdd[i];
                for (var position = interval.StartPosition; position <= interval.EndPosition; position++)
                {
                    var refBase = _chrReference.GetBase(position);
                    batch.Add(new CandidateAllele(_chrReference.Name, position, refBase, refBase,
                        AlleleCategory.Reference));
                }
            }
        }
    }
}