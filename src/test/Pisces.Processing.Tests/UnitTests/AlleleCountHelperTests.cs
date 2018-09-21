using Pisces.Domain.Types;
using Pisces.Processing.RegionState;
using Xunit;

namespace Pisces.Processing.Tests.UnitTests
{
    public class AlleleCountHelperTests
    {
        [Fact]
        public void GetAlleleCount()
        {
            var alleleMatrix = new int[10,6,3,11];

            alleleMatrix[1, (int)AlleleType.A, (int)DirectionType.Forward, 0] = 50;
            alleleMatrix[1, (int)AlleleType.A, (int)DirectionType.Forward, 4] = 2;
            alleleMatrix[1, (int) AlleleType.A, (int) DirectionType.Forward, 5] = 5;
            alleleMatrix[1, (int)AlleleType.A, (int)DirectionType.Forward, 6] = 3;
            alleleMatrix[1, (int)AlleleType.A, (int)DirectionType.Forward, 10] = 300;

            // From start of read:
            // Must be at least 5 in from beginning - take stuff that's close to the end (? now I'm questioning if we should take these)
            Assert.Equal(308, AlleleCountHelper.GetAnchorAdjustedAlleleCount(5, false, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, null));
            // If min anchor is bigger than what we granularly track, default to well-anchored
            Assert.Equal(308, AlleleCountHelper.GetAnchorAdjustedAlleleCount(10, false, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, null));
            // Must be at least 2 in from beginning - take stuff that's close to the end (? now I'm questioning if we should take these)
            Assert.Equal(310, AlleleCountHelper.GetAnchorAdjustedAlleleCount(2, false, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, null));
            // Must be at least 2 in from beginning and symmetrical - must be at least 2 in from either end
            Assert.Equal(10, AlleleCountHelper.GetAnchorAdjustedAlleleCount(2, false, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, null, true));
            // Take anything
            Assert.Equal(360, AlleleCountHelper.GetAnchorAdjustedAlleleCount(0, false, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, null));

            // Use maxAnchor to get just the residual stuff (we use this when adding weighted unanchored reads)
            Assert.Equal(52, AlleleCountHelper.GetAnchorAdjustedAlleleCount(0, false, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, 4));
            // Use maxAnchor to get just the residual stuff (we use this when adding weighted unanchored reads)
            Assert.Equal(50, AlleleCountHelper.GetAnchorAdjustedAlleleCount(0, false, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, 3));
            // Use maxAnchor to get just the residual stuff (we use this when adding weighted unanchored reads) - even if maxAnchor is greater than well-anchored, cap it so it is not double-counted
            Assert.Equal(52, AlleleCountHelper.GetAnchorAdjustedAlleleCount(0, false, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, 6));


            // From end of read:
            // Must be at least 5 in from end - take stuff that's close to the beginning (? now I'm questioning if we should take these)
            Assert.Equal(57, AlleleCountHelper.GetAnchorAdjustedAlleleCount(5, true, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, null));
            // If min anchor is bigger than what we granularly track, default to well-anchored
            Assert.Equal(57, AlleleCountHelper.GetAnchorAdjustedAlleleCount(10, true, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, null));
            // Must be at least 2 in from end - take stuff that's close to the beginning (? now I'm questioning if we should take these)
            Assert.Equal(60, AlleleCountHelper.GetAnchorAdjustedAlleleCount(2, true, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, null));
            // Take anything
            Assert.Equal(360, AlleleCountHelper.GetAnchorAdjustedAlleleCount(0, true, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, null));

            // Use maxAnchor to get just the residual stuff (we use this when adding weighted unanchored reads)
            Assert.Equal(303, AlleleCountHelper.GetAnchorAdjustedAlleleCount(0, true, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, 4));
            // Use maxAnchor to get just the residual stuff (we use this when adding weighted unanchored reads)
            Assert.Equal(300, AlleleCountHelper.GetAnchorAdjustedAlleleCount(0, true, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, 3));
            // Use maxAnchor to get just the residual stuff (we use this when adding weighted unanchored reads) - even if maxAnchor is greater than well-anchored, cap it so it is not double-counted
            Assert.Equal(303, AlleleCountHelper.GetAnchorAdjustedAlleleCount(0, true, 5, 11, alleleMatrix, 1, (int)AlleleType.A,
                (int)DirectionType.Forward, 11, 6));


        }
    }
}