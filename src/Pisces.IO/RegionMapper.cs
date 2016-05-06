using System;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.IO.Interfaces;

namespace Pisces.IO
{
    public class RegionMapper : IRegionMapper
    {
        private ChrReference _chrReference;
        public ChrIntervalSet IntervalSet { get; private set; }
        private int _lastPaddedPosition;
        private int _lastClearedIntervalIndex = -1;

        /// <summary>
        /// Sole job is to pad empty reference calls when using intervals.  Assumes batch has already included reference calls (either empty or not) 
        /// for cleared regions.
        /// </summary>
        /// <param name="chrReference"></param>
        /// <param name="includeReferenceCalls"></param>
        /// <param name="intervals"></param>
        public RegionMapper(ChrReference chrReference, ChrIntervalSet intervals)
        {
            _chrReference = chrReference;
            IntervalSet = intervals;
        }

        public CalledReference GetNextEmptyCall(int startPosition, int? maxUpToPosition)
        {
            var nextActiveInterval = GetNextRegion(startPosition);  // get next region that is not clear of startPosition

            if (nextActiveInterval == null)
                return null;  

            var nextPosition = Math.Max(nextActiveInterval.StartPosition, Math.Max(_lastPaddedPosition + 1, startPosition));

            var endPosition = !maxUpToPosition.HasValue ? IntervalSet.MaxPosition
                : Math.Min(maxUpToPosition.Value, IntervalSet.MaxPosition);

            if (nextPosition > endPosition) return null;

            if (nextActiveInterval.EndPosition <= nextPosition)
                _lastClearedIntervalIndex++;  // if this is the end of the active interval, go ahead and advance the index for next round

            if (!nextActiveInterval.ContainsPosition(nextPosition))
                return null;

            _lastPaddedPosition = nextPosition;
            return GetMissingReference(nextPosition);
        }

        private Region GetNextRegion(int minPosition)
        {
            for (var i = _lastClearedIntervalIndex + 1; i < IntervalSet.Intervals.Count; i ++)
            {
                var region = IntervalSet.Intervals[i];
                if (region.EndPosition >= minPosition)
                    return region;

                _lastClearedIntervalIndex ++;
            }

            return null; // no more intervals
        }

        public CalledReference GetMissingReference(int position)
        {
            var refBase = _chrReference.GetBase(position);
            var allele = new CalledReference()
            {
                Chromosome = _chrReference.Name,
                Reference = refBase,
                Alternate = refBase,
                Coordinate = position,
                Genotype =  Genotype.RefLikeNoCall
            };
            allele.Filters.Add(FilterType.LowDepth);

            return allele;
        }
    }
}