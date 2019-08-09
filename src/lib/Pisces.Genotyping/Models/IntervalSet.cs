using System;
using System.Collections.Generic;
using System.Linq;

namespace Pisces.Domain.Models
{
    public class ChrIntervalSet
    {
        public List<Region> Intervals { get; private set; }
        public string ChrName { get; private set; }
        private int _lastIndexCleared = -1;

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

            for(var i = _lastIndexCleared + 1; i < Intervals.Count; i ++)
            {
                var interval = Intervals[i];
                if (interval.StartPosition > clipToRegion.EndPosition)
                    break;

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
                    var leftRegion = new Region(remainingRegion.StartPosition, excludeRegion.StartPosition - 1, false);
                    var rightRegion = new Region(excludeRegion.EndPosition + 1, remainingRegion.EndPosition, false);

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
                    remainingRegion = excludeRegion.ContainsPosition(remainingRegion.StartPosition)
                        ? new Region(excludeRegion.EndPosition + 1, remainingRegion.EndPosition) // clip on left side
                        : new Region(remainingRegion.StartPosition, excludeRegion.StartPosition - 1);
                }
            }

            if (remainingRegion != null)
                regions.Add(remainingRegion);

            return regions;
        }

        public bool ContainsPosition(int position)
        {
            for(var i = _lastIndexCleared + 1; i < Intervals.Count; i ++)
            {
                var interval = Intervals[i];

                if (interval.StartPosition > position)
                    break;

                if (interval.ContainsPosition(position))
                    return true;
            }

            return false;
        }

        public bool ExpandInterval(int lookUpPosition, int newStart)
        {
           

            for (var i = _lastIndexCleared + 1; i < Intervals.Count; i++)
            {
                var interval = Intervals[i];

                if (interval.StartPosition > lookUpPosition)
                    return false;

                if (interval.ContainsPosition(lookUpPosition))
                {
                    interval.StartPosition = newStart;
                    return true;
                }
            }

            return false;
        }

        public void SetCleared(int position)
        {
            for (var i = _lastIndexCleared + 1; i < Intervals.Count; i ++)
            {
                var interval = Intervals[i];

                if (interval.EndPosition <= position)
                    _lastIndexCleared = i;

                if (interval.StartPosition > position)
                    break;
            }
        }

        public List<Region> GetIntervals(int endPosition)
        {
            var intervals = new List<Region>();

            for (var i = _lastIndexCleared + 1; i < Intervals.Count; i++)
            {
                var interval = Intervals[i];

                if (interval.StartPosition > endPosition)
                    break;

                intervals.Add(interval);
            }

            return intervals;
        }
    }
}
