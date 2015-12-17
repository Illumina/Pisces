using System;
using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Logic.RegionState;

namespace CallSomaticVariants.Models
{
    public class ChrIntervalSet
    {
        public List<Region> Intervals { get; private set; }
        public string ChrName { get; private set; }

        public ChrIntervalSet(List<Region> intervals, string chrName)
        {
            ChrName = chrName;
            Intervals = intervals;

            if (string.IsNullOrEmpty(chrName))
                throw new ArgumentException("Chr name cannot be empty");

            if (intervals == null)
                throw new ArgumentException("Intervals cannot be null");

            foreach(var interval in intervals)
                if (!interval.IsValid())
                    throw new ArgumentException(string.Format("Interval {2}:{0}-{1} is not valid", interval.StartPosition, interval.EndPosition, chrName));

            if (Intervals.Any())
            {
                MaxPosition = Intervals.Max(i => i.EndPosition);
                MinPosition = Intervals.Min(i => i.StartPosition);
            }
        }

        public int MaxPosition { get; private set; }
        public int MinPosition { get; private set; }

        public void SortAndCollapse()
        {
            // collapse any overlaps
            var doCollapse = true;
            var scrubbedIntervals = new List<Region>();

            while (doCollapse)
            {
                scrubbedIntervals.Clear();
                doCollapse = false;

                foreach (var interval in Intervals)
                {
                    var existingOverlap = scrubbedIntervals.FirstOrDefault(i => i.Overlaps(interval));

                    if (existingOverlap != null)
                    {
                        var union = existingOverlap.Merge(interval);
                        if (!scrubbedIntervals.Contains(union))
                        {
                            scrubbedIntervals.Remove(existingOverlap);
                            scrubbedIntervals.Add(union);
                            doCollapse = true; // go for another round of merging
                        }
                    }
                    else
                    {
                        scrubbedIntervals.Add(interval);
                    }
                }

                Intervals.Clear();
                Intervals.AddRange(scrubbedIntervals);
            }

            Intervals.Sort((i1, i2) => i1.StartPosition.CompareTo(i2.StartPosition));
        }

        public List<Region> GetClipped(Region clipToRegion, List<Region> excludeRegions = null )
        {
            var clippedIntervals = new List<Region>();

            if (!clipToRegion.IsValid())
                throw new ArgumentException(string.Format("Region {0} is not valid.", clipToRegion));

            foreach (var interval in Intervals)
            {
                if (!clipToRegion.Overlaps(interval))
                    continue;

                var clippedInterval = new Region(Math.Max(clipToRegion.StartPosition, interval.StartPosition),
                    Math.Min(clipToRegion.EndPosition, interval.EndPosition));

                if (excludeRegions == null)
                {
                    clippedIntervals.Add(clippedInterval);
                }
                else {
                    clippedIntervals.AddRange(GetMinus(clippedInterval, excludeRegions)); 
                }
            }

            return clippedIntervals;
        }

        /// <summary>
        /// Get intervals for a region, minus an overlaps with the exclude regions.
        /// </summary>
        /// <param name="keepRegion">Region to keep</param>
        /// <param name="excludeRegions">List of regions to exclude from the final result.  Assumes regions are sorted.</param>
        /// <returns></returns>
        public static List<Region> GetMinus(Region keepRegion, List<Region> excludeRegions)
        {
            var regions = new List<Region>();

            if (keepRegion == null || !keepRegion.IsValid())
                throw new ArgumentException(string.Format("Region {0} is not valid.", keepRegion));

            if (excludeRegions == null)
            {
                regions.Add(keepRegion);
                return regions;
            }

            var remainingRegion = new Region(keepRegion.StartPosition, keepRegion.EndPosition);

            foreach (var excludeRegion in excludeRegions)
            {
                if (excludeRegion == null || !excludeRegion.IsValid())
                    throw new ArgumentException(string.Format("Region {0} is not valid.", excludeRegion));

                if (!remainingRegion.Overlaps(excludeRegion))
                    continue;  // no overlap, keep checking other excluded regions

                if (excludeRegion.FullyContains(remainingRegion))
                {
                    remainingRegion = null;  // done, fully excluded
                    break;
                }

                if (remainingRegion.FullyContains(excludeRegion))
                {
                    // need to break up remaining region!
                    var leftRegion = new Region(remainingRegion.StartPosition, excludeRegion.StartPosition - 1);
                    var rightRegion = new Region(excludeRegion.EndPosition + 1, remainingRegion.EndPosition);

                    if (leftRegion.IsValid())
                        regions.Add(leftRegion);

                    if (!rightRegion.IsValid())
                    {
                        remainingRegion = null; // done, nothing left on right side
                        break;
                    }
                    
                    remainingRegion = rightRegion; // pass right region on for more processing
                }
                else
                {
                    if (excludeRegion.ContainsPosition(remainingRegion.StartPosition)) // clip on left side
                        remainingRegion = new Region(excludeRegion.EndPosition + 1, remainingRegion.EndPosition);
                    else 
                        remainingRegion = new Region(remainingRegion.StartPosition, excludeRegion.StartPosition - 1);
                }
            }

            if (remainingRegion != null)
                regions.Add(remainingRegion);

            return regions;
        }
    }
}
