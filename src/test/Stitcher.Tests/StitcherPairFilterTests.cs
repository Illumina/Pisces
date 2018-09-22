using System.Linq;
using Moq;
using Alignment.Domain.Sequencing;
using BamStitchingLogic;
using StitchingLogic;
using Xunit;

namespace Stitcher.Tests
{
    public class StitcherPairFilterTests
    {
        [Fact]
        public void TryPair()
        {
            var dupIdentifier = new Mock<IDuplicateIdentifier>();

            var filter = new StitcherPairFilter(false, false, dupIdentifier.Object, new ReadStatusCounter());

            // First alignment the filter sees: not paired yet, hold on to it
            var firstInPair = CreateAlignment("abc");
            var tagUtils = new TagUtils();
            tagUtils.AddStringTag("SA", "chr1,100,+,3M,50,1");
            firstInPair.TagData = tagUtils.ToBytes();
            var pair = filter.TryPair(firstInPair);
            Assert.Null(pair);
            // Found the original alignment's pair -> return the pair, even though it has a supplementary which has not yet been encountered
            var secondInPair = CreateAlignment("abc");
            pair = filter.TryPair(secondInPair);
            Assert.NotNull(pair);
            // There should be nothing unpaired
            Assert.Equal(0, filter.GetFlushableUnpairedReads().Count());

            // Add another one and don't give it a pair (i.e. a singleton), there should be a single unpaired
            pair = filter.TryPair(CreateAlignment("singleton"));
            Assert.Null(pair);
            Assert.Equal(1, filter.GetFlushableUnpairedReads().Count());


            // Improper pairs should be treated as incomplete (like a singleton)
            Assert.Equal(0, filter.GetFlushableUnpairedReads().Count());
            var improperPairRead1 = CreateAlignment("improper", false);
            var improperPairRead2 = CreateAlignment("improper", false);
            pair = filter.TryPair(improperPairRead1);
            Assert.Null(pair);
            pair = filter.TryPair(improperPairRead2);
            Assert.Null(pair);
            // Should be able to get both of these reads back with unpaired
            Assert.Equal(2, filter.GetFlushableUnpairedReads().Count());



            ///
            /// LowMapQ filter tests both the filterPairLowMapQ and the minmapquality setting
            /// If filterPairLowMapQ == true, and one or both reads below mapQ both read pairs should be filtered and have 0 FlushableUnpairedReads
            /// If filterPairLowMapQ == false, and one read below mapQ, only one read should be filtered and have 1 FlushableUnpairedReads
            /// 

            // HappyCase, both reads mapQ = 30, filterPair on, Stitched so 0 flushable reads, minMapQ = 20
            LowMapQualityTest(30, 30, true, 0, 20, true);
            // r1MapQ = 10, r2MapQ = 30, filterPair on, expected to throw out both reads, minMapQ = 20)
            LowMapQualityTest(10, 30, true, 0, 20, false);
            // r1MapQ = 30, r2MapQ = 10, filterPair on, expected to throw out both reads, minMapQ = 20)
            LowMapQualityTest(10, 30, true, 0, 20, false);
            // r1MapQ = 30, r2MapQ = 10, filterPair off, expected to throw out 1 read, minMapQ = 20)
            LowMapQualityTest(10, 30, false, 1, 20, false);
            // r1MapQ = 10, r2MapQ = 30, filterPair off, expected to throw out 1 read, minMapQ = 20)
            LowMapQualityTest(10, 30, false, 1, 20, false);
            // r1MapQ = 3, r2MapQ = 20, filterPair on, expected to throw out both reads, minMapQ = 5)
            LowMapQualityTest(3, 20, true, 0, 5, false);
            // r1MapQ = 10, r2MapQ = 10, filterPair on, expected to throw out both reads, minMapQ = 20)
            LowMapQualityTest(10, 10, true, 0, 20, false);
            // r1MapQ = 10, r2MapQ = 10, filterPair off, expected to throw out both reads, minMapQ = 20)
            LowMapQualityTest(10, 10, false, 0, 20, false);
            // r1MapQ = 19, r2MapQ = 20, filterPair on, expected to throw out both reads, minMapQ = 20)
            LowMapQualityTest(19, 20, true, 0, 20, false);
            // r1MapQ = 19, r2MapQ = 20, filterPair off, expected to throw out 1 reads, minMapQ = 20)
            LowMapQualityTest(19, 20, false, 1, 20, false);




            // Non-overlapping pairs should be treated as incomplete (like a singleton)
            Assert.Equal(0, filter.GetFlushableUnpairedReads().Count());
            var nonOverlappingRead1 = CreateAlignment("noOverlap", true, 1, "3M2I");
            var nonOverlappingRead2 = CreateAlignment("noOverlap", true, 4, "2I4M");
            pair = filter.TryPair(nonOverlappingRead1);
            Assert.Null(pair);
            pair = filter.TryPair(nonOverlappingRead2);
            Assert.Null(pair);
            // Should be able to get both of these reads back with unpaired
            Assert.Equal(2, filter.GetFlushableUnpairedReads().Count());
        }

        public static BamAlignment CreateAlignment(string name, bool isProperPair = true, int position = 0, string cigarData="3M", bool isUnMapped = false, bool mateIsUnMapped = false, uint mapQ = 30)
        {
            var alignment = new BamAlignment
            {
                Name = name,
                Qualities = new byte[0],
                CigarData = new CigarAlignment(cigarData),
                Position = position
            };
            alignment.SetIsProperPair(isProperPair);
            alignment.SetIsUnmapped(isUnMapped);
            alignment.SetIsMateUnmapped(mateIsUnMapped);
            alignment.MapQuality = mapQ;

            return alignment;
        }

        private static void MappedReadTest(bool r1UnMapped, bool r1MateUnMapped, bool r2UnMapped, bool r2MateUnMapped)
        {
            var dupIdentifier = new Mock<IDuplicateIdentifier>();
            var filter = new StitcherPairFilter(false, false, dupIdentifier.Object, new ReadStatusCounter(),filterPairUnmapped:true);
            var unmappedRead = CreateAlignment("case1", true, 0, "3M", r1UnMapped, r1MateUnMapped);
            var mappedRead = CreateAlignment("case1", true, 0, "3M", r2UnMapped, r2MateUnMapped);
            var pair = filter.TryPair(mappedRead);
            Assert.Null(pair);
            pair = filter.TryPair(unmappedRead);
            Assert.Null(pair);
            Assert.Equal(0, filter.GetFlushableUnpairedReads().Count());
        }

        private static void LowMapQualityTest(uint r1MapQ, uint r2MapQ, bool filterPair, int expectedFragmentCount, uint MinMapQuality, bool shouldStitch)
        {
            var dupIdentifier = new Mock<IDuplicateIdentifier>();
            var filter = new StitcherPairFilter(false, false, dupIdentifier.Object, new ReadStatusCounter(), filterPairLowMapQ: filterPair, minMapQuality : MinMapQuality);
            var r1 = CreateAlignment("LowMap", true, 0, "3M", false, mapQ: r1MapQ);
            var r2 = CreateAlignment("LowMap", true, 0, "3M", false, mapQ: r2MapQ);
            var pair = filter.TryPair(r1);
            Assert.Null(pair);
            pair = filter.TryPair(r2);
            if (shouldStitch) Assert.NotNull(pair);
            else Assert.Null(pair);
            Assert.Equal(expectedFragmentCount, filter.GetFlushableUnpairedReads().Count());
        }
    }
}