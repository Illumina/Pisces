using System.Collections.Concurrent;
using Gemini.BinSignalCollection;
using Xunit;

namespace Gemini.Tests
{
    public class BinEvidenceHelpersTests
    {
        [Fact]
        public void AddEvidence()
        {
            var pairResult = TestHelpers.GetPairResult(10);
            var pairResultFarAway = TestHelpers.GetPairResult(750);
            ConcurrentDictionary<int, uint> probableTrueSnvRegionsLookup = new ConcurrentDictionary<int, uint>();
            ConcurrentDictionary<int, uint> allHits = new ConcurrentDictionary<int, uint>();

            // TODO: yuck. this stuff should be moved over to binevidence or at least somehow consolidated
            for (var i = 0; i < 1000; i++)
            {
                allHits[i] = 0;
                probableTrueSnvRegionsLookup[i] = 0;
            }
            BinEvidenceHelpers.AddEvidence(pairResult, 500, 0, allHits, probableTrueSnvRegionsLookup, true, 1000, 1);
            Assert.Equal(2f, allHits[0]);
            Assert.Equal(2f, probableTrueSnvRegionsLookup[0]);
            
            // Same bin, not mismatch
            BinEvidenceHelpers.AddEvidence(pairResult, 500, 0, allHits, probableTrueSnvRegionsLookup, false, 1000, 1);
            Assert.Equal(4f, allHits[0]);
            Assert.Equal(2f, probableTrueSnvRegionsLookup[0]);

            BinEvidenceHelpers.AddEvidence(pairResultFarAway, 500, 0, allHits, probableTrueSnvRegionsLookup, false, 1000, 1);
            Assert.Equal(4f, allHits[0]);
            Assert.Equal(2f, probableTrueSnvRegionsLookup[0]);
            Assert.Equal(2f, allHits[1]);
            Assert.Equal(0f, probableTrueSnvRegionsLookup[1]);

        }
    }
}