using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.Logic;
using Alignment.Logic.Tests;
using BamStitchingLogic;
using Gemini.IO;
using Moq;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{
    public class PairFilterReadPairSourceTests
    {
        [Fact]
        public void GetNextEntryUntilNull()
        {
            var stagedPairs = new Dictionary<BamAlignment, ReadPair>();
            var unpaired = TestHelpers.CreateBamAlignment("AAA", 3, 1, 30, false, name: "Unpaired", isFirstMate: true);

            var readA1 = TestHelpers.CreateBamAlignment("AAA", 3, 1, 30, false, name: "Read1", isFirstMate: true);
            var pair1 = TestHelpers.GetPair("3M", "3M", name: "Read1");

            var pairDuplicate = TestHelpers.GetPair("3M", "3M", name: "Dup");
            pairDuplicate.Read1.SetIsDuplicate(true);
            var readDuplicate = TestHelpers.CreateBamAlignment("AAA", 3, 1, 30, false, name: "Dup", isFirstMate: true);
            readDuplicate.SetIsDuplicate(true);
            stagedPairs.Add(readA1, pair1);
            stagedPairs.Add(readDuplicate, pairDuplicate);
            
            var pairs = GetReadPairs(stagedPairs.Keys.ToList(), stagedPairs, new List<BamAlignment>(){unpaired}, false);
            Assert.Equal(3, pairs.Count);
            Assert.Equal(PairStatus.Paired, pairs[0].PairStatus);
            Assert.Equal(PairStatus.Duplicate, pairs[1].PairStatus);
            Assert.Equal(PairStatus.Unknown, pairs[2].PairStatus);

            // Should filter out duplicate if configured to do so
            pairs = GetReadPairs(stagedPairs.Keys.ToList(), stagedPairs, new List<BamAlignment>() { unpaired }, true);
            Assert.Equal(2, pairs.Count);
            Assert.Equal(PairStatus.Paired, pairs[0].PairStatus);
            Assert.Equal(PairStatus.Unknown, pairs[1].PairStatus);

            var readEarlierChrom = TestHelpers.CreateBamAlignment("AAA", 3, 1, 30, false, name: "Earlier", isFirstMate: true);
            readEarlierChrom.RefID = 0;
            var pairEarlierChrom = TestHelpers.GetPair("3M", "3M", name: "Earlier");
            stagedPairs.Add(readEarlierChrom, pairEarlierChrom);
            pairs = GetReadPairs(stagedPairs.Keys.ToList(), stagedPairs, new List<BamAlignment>() { unpaired }, true);
            Assert.Equal(3, pairs.Count);

            // Should filter out reads based on refId if configured to do so
            pairs = GetReadPairs(stagedPairs.Keys.ToList(), stagedPairs, new List<BamAlignment>() { unpaired }, true, 1);
            Assert.Equal(2, pairs.Count);

            var readLaterChrom = TestHelpers.CreateBamAlignment("AAA", 3, 1, 30, false, name: "Later", isFirstMate: true);
            readLaterChrom.RefID = 10;
            var pairLaterChrom = TestHelpers.GetPair("3M", "3M", name: "Later");
            stagedPairs.Add(readLaterChrom, pairLaterChrom);
            pairs = GetReadPairs(stagedPairs.Keys.ToList(), stagedPairs, new List<BamAlignment>() { unpaired }, true, 1);
            Assert.Equal(2, pairs.Count);

            // Should stop once hits higher refId if configured to filter on refId (unrealistic, but testing anyway)
            var readA1again = TestHelpers.CreateBamAlignment("AAA", 3, 1, 30, false, name: "BackToChr1", isFirstMate: true);
            var pair1again = TestHelpers.GetPair("3M", "3M", name: "BackToChr1");
            stagedPairs.Add(readA1again, pair1again);
            pairs = GetReadPairs(stagedPairs.Keys.ToList(), stagedPairs, new List<BamAlignment>() { unpaired }, true, 1);
            Assert.Equal(2, pairs.Count);

        }

        private static List<ReadPair> GetReadPairs(List<BamAlignment> bamAlignments, Dictionary<BamAlignment, ReadPair> stagedReadPairs, List<BamAlignment> flushableUnpaired, bool skipDups, int? refId=null)
        {
            var refIdLookup = new Dictionary<string, int>() { { "chr1", 1 } };
            var bamReader = new MockBamReader(bamAlignments, refIdLookup);
            var mockFilter = new Mock<IAlignmentPairFilter>();
            mockFilter.Setup(x => x.TryPair(It.IsAny<BamAlignment>())).Returns<BamAlignment>(b => stagedReadPairs[b]);
            mockFilter.Setup(x => x.GetFlushableUnpairedReads()).Returns(flushableUnpaired);
            var source = new PairFilterReadPairSource(bamReader, new ReadStatusCounter(), skipDups, mockFilter.Object, refId);

            var readPairs = new List<ReadPair>();

            while (true)
            {
                var readPair = source.GetNextEntryUntilNull();
                if (readPair == null)
                {
                    break;
                }

                readPairs.Add(readPair);
            }

            return readPairs;
        }

    }
}