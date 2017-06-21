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
            var pair = filter.TryPair(firstInPair);
            Assert.Null(pair);
            // Found the original alignment's pair -> return the pair
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

        public static BamAlignment CreateAlignment(string name, bool isProperPair = true, int position = 0, string cigarData="3M")
        {
            var alignment = new BamAlignment
            {
                Name = name,
                Qualities = new byte[0],
                CigarData = new CigarAlignment(cigarData),
                Position = position
            };
            alignment.SetIsProperPair(isProperPair);

            return alignment;
        }
    }
}