using System.Collections.Generic;
using Gemini.BinSignalCollection;
using Xunit;

namespace Gemini.Tests
{
    public class SparseBoolBinsTests
    {
        [Fact]
        public void IncrementHit()
        {
            var sparseBins = new SparseGroupedBoolBins(100);
            var binsThatShouldBeTrue = new List<int>();

            // Everything should be false to start off
            CheckBins(binsThatShouldBeTrue, sparseBins);

            // Calling this on a bool bin doesn't make the bool bin "truer". Just make sure it stays true with successive updates. Once it's true, it's true.
            var added = sparseBins.AddHit(5);
            Assert.True(added);
            binsThatShouldBeTrue.Add(5);
            CheckBins(binsThatShouldBeTrue, sparseBins);

            sparseBins.IncrementHit(5, 10);
            CheckBins(binsThatShouldBeTrue, sparseBins);

            sparseBins.IncrementHit(4, 5);
            binsThatShouldBeTrue.Add(4);
            CheckBins(binsThatShouldBeTrue, sparseBins);
        }
        [Fact]
        public void AddHit()
        {
            var sparseBoolBins = new SparseGroupedBoolBins(100);
            var binsThatShouldBeTrue = new List<int>();

            // Everything should be false to start off
            CheckBins(binsThatShouldBeTrue, sparseBoolBins);

            // Add a hit
            var added = sparseBoolBins.AddHit(5);
            Assert.True(added);
            binsThatShouldBeTrue.Add(5);
            CheckBins(binsThatShouldBeTrue, sparseBoolBins);

            // Adding same again shouldn't change things
            sparseBoolBins.AddHit(5);
            CheckBins(binsThatShouldBeTrue, sparseBoolBins);

            // Add another hit
            sparseBoolBins.AddHit(7);
            binsThatShouldBeTrue.Add(7);
            CheckBins(binsThatShouldBeTrue, sparseBoolBins);

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

        private static void CheckBins(List<int> binsThatShouldBeTrue, SparseGroupedBoolBins sparseBoolBins)
        {
            for (int i = 0; i < 100; i++)
            {
                if (!binsThatShouldBeTrue.Contains(i))
                {
                    Assert.Equal(false, sparseBoolBins.GetHit(i));
                }
            }

            foreach (var bin in binsThatShouldBeTrue)
            {
                Assert.Equal(true, sparseBoolBins.GetHit(bin));
            }
        }
    }
}