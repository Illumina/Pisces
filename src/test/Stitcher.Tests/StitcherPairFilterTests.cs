using System.Linq;
using Moq;
using Alignment.Domain.Sequencing;
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

            // Realistic Cases
            // R1 Unmapped, R1 Mate Unmapped, R2 Unmapped, R2 Mate Unmapped
            MappedReadTest(true, true, true, true);
            // R1 Unmapped, R1 Mate Mapped, R2 Mapped, R2 Mate Unmapped
            MappedReadTest(true, false, false, true);
            // R1 Mapped, R1 Mate Unmapped, R2 Unmapped, R2 Mate Mapped
            MappedReadTest(false, true, true, false);

            // Theoretical Cases
            // R1 Unmapped, R1 Mate Unmapped, R2 Unmapped, R2 Mate Mapped(*)
            MappedReadTest(true, true, true, false);
            // R1 Unmapped, R1 Mate Mapped(*), R2 Unmapped, R2 Mate Unmapped
            MappedReadTest(true, false, true, true);
            // R1 Mapped, R1 Mate Mapped(*), R2 Unmapped, R2 Mate Mapped
            MappedReadTest(false, false, true, false);
            // R1 Mapped, R1 Mate Unmapped, R2 Unmapped, R2 Mate Unmapped(*)
            MappedReadTest(false, true, true, true);

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
            var filter = new StitcherPairFilter(false, false, dupIdentifier.Object, new ReadStatusCounter(), filterForUnmappedReads: true);
            var unmappedRead = CreateAlignment("case1", true, 0, "3M", r1UnMapped, r1MateUnMapped);
            var mappedRead = CreateAlignment("case1", true, 0, "3M", r2UnMapped, r2MateUnMapped);
            var pair = filter.TryPair(mappedRead);
            Assert.Null(pair);
            pair = filter.TryPair(unmappedRead);
            Assert.Null(pair);
            Assert.Equal(0, filter.GetFlushableUnpairedReads().Count());
        }
    }
}