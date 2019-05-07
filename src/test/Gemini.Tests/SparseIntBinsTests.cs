using System.Collections.Generic;
using Gemini.BinSignalCollection;
using Xunit;

namespace Gemini.Tests
{
    public class DenseBinsTests
    {
        [Fact]
        public void AddHit()
        {
            var sparseBoolBins = new DenseBins(100);
            var binsThatShouldBeTrue = new Dictionary<int, int>();

            // Everything should be false to start off
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, sparseBoolBins);

            // Add a hit
            var added = sparseBoolBins.AddHit(5);
            Assert.True(added);
            binsThatShouldBeTrue[5] = 1;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, sparseBoolBins);

            // Adding same again shouldn't add any new hits, but should increment this one
            sparseBoolBins.AddHit(5);
            binsThatShouldBeTrue[5]++;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, sparseBoolBins);

            // Add another hit
            sparseBoolBins.AddHit(7);
            binsThatShouldBeTrue[7] = 1;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, sparseBoolBins);

            // Add a hit that's out of range
            var result = sparseBoolBins.AddHit(1000);
            Assert.False(result);
            // Just barely out of range
            result = sparseBoolBins.AddHit(100);
            Assert.False(result);
            // Negative
            result = sparseBoolBins.AddHit(-1);
            Assert.False(result);
        }

        [Fact]
        public void IncrementHit()
        {
            var bins = new DenseBins(100);
            var binsThatShouldBeTrue = new Dictionary<int, int>();

            // Everything should be false to start off
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            var added = bins.AddHit(5);
            Assert.True(added);
            binsThatShouldBeTrue[5] = 1;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            bins.IncrementHit(5, 10);
            binsThatShouldBeTrue[5] += 10;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            bins.IncrementHit(4, 5);
            binsThatShouldBeTrue[4] = 5;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);
        }

        [Fact]
        public void MergeBins()
        {
            var bins = new DenseBins(100);
            var binsThatShouldBeTrue = new Dictionary<int, int>();

            // Everything should be false to start off
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            var added = bins.AddHit(5);
            Assert.True(added);
            bins.IncrementHit(5, 10);
            binsThatShouldBeTrue[5] = 11;
            bins.IncrementHit(4, 5);
            binsThatShouldBeTrue[4] = 5;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            // Merge bins with same start
            var bins2 = new DenseBins(100);
            var binsThatShouldBeTrue2 = new Dictionary<int, int>();
            bins2.IncrementHit(3, 10);
            binsThatShouldBeTrue2[3] = 10;
            bins2.IncrementHit(4, 5);
            binsThatShouldBeTrue2[4] = 5;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue2, bins2);

            bins.Merge(bins2, 0, 0, 100);
            var binsThatShouldBeTrueMerged = new Dictionary<int, int>();
            binsThatShouldBeTrueMerged[3] = 10;
            binsThatShouldBeTrueMerged[4] = 10;
            binsThatShouldBeTrueMerged[5] = 11;
            // The one merged into should be updated
            BinTestHelpers.CheckBins(binsThatShouldBeTrueMerged, bins);
            // The one that was merged in should not change
            BinTestHelpers.CheckBins(binsThatShouldBeTrue2, bins2);

            // 0 in new is 2 in merge
            var bins3 = new DenseBins(100);
            bins3.IncrementHit(93, 10);
            bins3.IncrementHit(94, 5);
            BinTestHelpers.CheckBins(binsThatShouldBeTrue2, bins2);

            bins.Merge(bins3, 80, 80, 100);

            binsThatShouldBeTrueMerged[13] = 10;
            binsThatShouldBeTrueMerged[14] = 5;
            BinTestHelpers.CheckBins(binsThatShouldBeTrueMerged, bins);

            bins.Merge(bins3, 80, 94, 100);

            binsThatShouldBeTrueMerged[14] = 10;
            BinTestHelpers.CheckBins(binsThatShouldBeTrueMerged, bins);

        }
    }

    public class BinTestHelpers
    {
        public static void CheckBins(Dictionary<int, int> nonZeroBinValues, IBins<int> intBins)
        {
            for (int i = 0; i < 100; i++)
            {
                if (!nonZeroBinValues.ContainsKey(i))
                {
                    Assert.Equal(0, intBins.GetHit(i));
                }
            }

            foreach (var bin in nonZeroBinValues)
            {
                Assert.Equal(bin.Value, intBins.GetHit(bin.Key));
            }
        }
    }

    public class SparseIntBinsTests
    {
        [Fact]
        public void IncrementHit()
        {
            var bins = new SparseGroupedIntBins(100);
            var binsThatShouldBeTrue = new Dictionary<int, int>();

            // Everything should be false to start off
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            var added = bins.AddHit(5);
            Assert.True(added);
            binsThatShouldBeTrue[5] = 1;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            bins.IncrementHit(5,10);
            binsThatShouldBeTrue[5]+=10;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            bins.IncrementHit(4, 5);
            binsThatShouldBeTrue[4] = 5;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);
        }

        [Fact]
        public void AddHit()
        {
            var bins = new SparseGroupedIntBins(100);
            var binsThatShouldBeTrue = new Dictionary<int, int>();

            // Everything should be false to start off
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            // Add a hit
            var added = bins.AddHit(5);
            Assert.True(added);
            binsThatShouldBeTrue[5] = 1;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            // Adding same again shouldn't add any new hits, but should increment this one
            bins.AddHit(5);
            binsThatShouldBeTrue[5]++;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            // Add another hit
            bins.AddHit(7);
            binsThatShouldBeTrue[7] = 1;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            // Add a hit that's out of range
            var result = bins.AddHit(1000);
            Assert.False(result);
            // Just barely out of range
            result = bins.AddHit(100);
            Assert.False(result);
            // Negative
            result = bins.AddHit(-1);
            Assert.False(result);

        }

        [Fact]
        public void MergeBins()
        {
            var bins = new SparseGroupedIntBins(100);
            var binsThatShouldBeTrue = new Dictionary<int, int>();

            // Everything should be false to start off
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            var added = bins.AddHit(5);
            Assert.True(added);
            bins.IncrementHit(5, 10);
            binsThatShouldBeTrue[5] = 11;
            bins.IncrementHit(4, 5);
            binsThatShouldBeTrue[4] = 5;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue, bins);

            // Merge bins with same start
            var bins2 = new SparseGroupedIntBins(100);
            var binsThatShouldBeTrue2 = new Dictionary<int, int>();
            bins2.IncrementHit(3, 10);
            binsThatShouldBeTrue2[3] = 10;
            bins2.IncrementHit(4, 5);
            binsThatShouldBeTrue2[4] = 5;
            BinTestHelpers.CheckBins(binsThatShouldBeTrue2, bins2);

            bins.Merge(bins2, 0,0, 100);
            var binsThatShouldBeTrueMerged = new Dictionary<int, int>();
            binsThatShouldBeTrueMerged[3] = 10;
            binsThatShouldBeTrueMerged[4] = 10;
            binsThatShouldBeTrueMerged[5] = 11;
            // The one merged into should be updated
            BinTestHelpers.CheckBins(binsThatShouldBeTrueMerged, bins);
            // The one that was merged in should not change
            BinTestHelpers.CheckBins(binsThatShouldBeTrue2, bins2);

            // 0 in new is 2 in merge
            var bins3 = new SparseGroupedIntBins(100);
            bins3.IncrementHit(93, 10);
            bins3.IncrementHit(94, 5);

            bins.Merge(bins3, 80, 80, 100);

            binsThatShouldBeTrueMerged[13] = 10;
            binsThatShouldBeTrueMerged[14] = 5;
            BinTestHelpers.CheckBins(binsThatShouldBeTrueMerged, bins);

            // Mix with different kind of bins
            var bins4 = new DenseBins(100);
            bins4.IncrementHit(93, 10);
            bins4.IncrementHit(94, 5);

            bins.Merge(bins4, 70, 70, 100);

            binsThatShouldBeTrueMerged[23] = 10;
            binsThatShouldBeTrueMerged[24] = 5;
            BinTestHelpers.CheckBins(binsThatShouldBeTrueMerged, bins);

        }
    }
}