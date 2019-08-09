using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pisces.Domain.Types
{
    public enum ReadCollapsedType
    {
        /// <summary> duplex collapsed and stitched read pair </summary>
        DuplexStitched = 0,
        /// <summary> duplex collapsed and non-stitched read pair </summary>
        DuplexNonStitched = 1,
        SimplexStitched = 2,
        SimplexNonStitched = 3,
        /// <summary> simplex collapsed with F1R2 orientation and stitched read pair </summary>
        SimplexForwardStitched = 4,
        /// <summary> simplex collapsed with F1R2 orientation and non-stitched read pair </summary>
        SimplexForwardNonStitched = 5,
        /// <summary> simplex collapsed with R1F2 orientation and stitched read pair </summary>
        SimplexReverseStitched = 6,
        /// <summary> simplex collapsed with R1F2 orientation and non-stitched read pair </summary>
        SimplexReverseNonStitched = 7
    }
}
