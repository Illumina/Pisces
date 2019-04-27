using System;
using System.Collections.Generic;
using System.Text;
using Pisces.Domain.Models;
using Pisces.Domain.Types;

namespace Pisces.Processing.RegionState
{
    public class CollapsedRegionStateManager : RegionStateManager
    {
        /// <summary>
        /// CollapsedRegionStateManager
        /// </summary>
        /// <param name="includeRefAlleles"></param>
        /// <param name="minBasecallQuality"></param>
        /// <param name="intervalSet"></param>
        /// <param name="blockSize"></param>
        /// <param name="trackOpenEnded"></param>
        /// <param name="trackReadSummaries"></param>
        /// <remarks>create CollapsedRegionStateManager (derived) if both ExpectCollapsedReads and ExpectStitchedReads are true 
        /// otherwise use RegionStateManager (based) instead </remarks>
        public CollapsedRegionStateManager(bool includeRefAlleles = false, int minBasecallQuality = 20,
            ChrIntervalSet intervalSet = null, int blockSize = 1000,
            bool trackOpenEnded = false, bool trackReadSummaries = false, int trackedAnchorSize = 5)
            : base(includeRefAlleles, minBasecallQuality, true, intervalSet, blockSize, trackOpenEnded,
                trackReadSummaries, false, trackedAnchorSize)
        {
            ExpectCollapsedReads = true;
        }

        #region overrides

        public override int GetCollapsedReadCount(int position, ReadCollapsedType directionType)
        {
            var region = GetBlock(position, false) as CollapsedRegionState;
            return region == null ? 0 : region.GetCollapsedReadCount(position, directionType);
        }

        protected override void AddCollapsedReadCount(int position, Read alignment, DirectionType directionType)
        {
            if (!alignment.IsCollapsedRead())
                throw new Exception($"The input is collapsed BAM, but {alignment} is not a collapsed read.");
            ReadCollapsedType? type = alignment.GetReadCollapsedType(directionType);
            if (type.HasValue)
            {
                var block = GetBlock(position) as CollapsedRegionState;
                if (block == null)
                    throw new Exception($"Cannot find read collapsed region block @ {position}");
                block.AddCollapsedReadCount(position, type.Value);
            }
        }

        protected override RegionState CreateBlock(int startPosition, int endPosition)
        {
            return new CollapsedRegionState(startPosition, endPosition);
        }

        #endregion
    }
}
