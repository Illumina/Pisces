using System;
using System.Collections.Generic;
using System.Text;
using Pisces.Domain;
using Pisces.Domain.Types;

namespace Pisces.Processing.RegionState
{
    public class CollapsedRegionState : RegionState
    {
        // [pos,count/categories]
        protected int[,] _collapsedCount;

        public CollapsedRegionState(int startPosition, int endPosition)
            : base(startPosition, endPosition)
        {
            var regionSize = EndPosition - StartPosition + 1;
            _collapsedCount = new int[regionSize, Constants.NumReadCollapsedTypes];
        }

        public int GetCollapsedReadCount(int position, ReadCollapsedType directionType)
        {
            if (!IsPositionInRegion(position))
                throw new ArgumentException(string.Format("Position {0} is not in region '{1}'.", position, Name));
            return _collapsedCount[position - StartPosition, (int)directionType];
        }

        public void AddCollapsedReadCount(int position, ReadCollapsedType collapsedType)
        {
            if (IsPositionInRegion(position))
            {
                _collapsedCount[position - StartPosition, (int)collapsedType]++;
                if (collapsedType == ReadCollapsedType.SimplexReverseStitched || 
                    collapsedType == ReadCollapsedType.SimplexForwardStitched)
                {
                    _collapsedCount[position - StartPosition, (int)ReadCollapsedType.SimplexStitched]++; 
                }
                else if (collapsedType == ReadCollapsedType.SimplexForwardNonStitched ||
                    collapsedType == ReadCollapsedType.SimplexReverseNonStitched)
                {
                    _collapsedCount[position - StartPosition, (int)ReadCollapsedType.SimplexNonStitched]++;
                }
            }
        }

        public override void Reset(int startPosition, int endPosition)
        {
            base.Reset(startPosition, endPosition);
            _collapsedCount = new int[EndPosition - StartPosition + 1, Constants.NumReadCollapsedTypes];
        }
    }
}
