using Alignment.Domain.Sequencing;
using Gemini.Infrastructure;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{
    public class NonDebugStatusHandlerTests
    {
        [Fact]
        public void AddStatusCount()
        {
            var handler = new NonDebugStatusHandler();
            handler.AddStatusCount("x");
            handler.AddStatusCount("y");
            handler.AddStatusCount("x");
            // No counter to check, nothing added
        }

        [Fact]
        public void AddCombinedStatusStringTags()
        {
            var counter = new ReadStatusCounter();
            var handler = new DebugSummaryStatusHandler(counter);
            var pair = TestHelpers.GetPair("10M", "10M");
            TagUtils.ReplaceOrAddStringTag(ref pair.Read1.TagData, "HI", "read1_hi");
            TagUtils.ReplaceOrAddStringTag(ref pair.Read2.TagData, "HI", "read2_hi");

            var outAlignment = new BamAlignment(pair.Read1);
            TagUtils.ReplaceOrAddStringTag(ref outAlignment.TagData, "HI", "nothing");

            // Should  not update
            handler.AddCombinedStatusStringTags("HI", pair.Read1, pair.Read2, outAlignment);
            Assert.Equal("nothing", outAlignment.GetStringTag("HI"));
        }

        [Fact]
        public void UpdateStatusStringTag()
        {
            var counter = new ReadStatusCounter();
            var handler = new DebugSummaryStatusHandler(counter);
            var pair = TestHelpers.GetPair("10M", "10M");

            TagUtils.ReplaceOrAddStringTag(ref pair.Read1.TagData, "HI", "nothing");

            // Should  not update
            handler.UpdateStatusStringTag("HI", "newvalue", pair.Read1);
            Assert.Equal("nothing", pair.Read1.GetStringTag("HI"));
        }

        [Fact]
        public void AppendStatusStringTag()
        {
            var counter = new ReadStatusCounter();
            var handler = new DebugSummaryStatusHandler(counter);
            var pair = TestHelpers.GetPair("10M", "10M");

            TagUtils.ReplaceOrAddStringTag(ref pair.Read1.TagData, "HI", "nothing");

            // Should  not update
            handler.AppendStatusStringTag("HI", "newvalue", pair.Read1);
            Assert.Equal("nothing", pair.Read1.GetStringTag("HI"));
        }
    }
}