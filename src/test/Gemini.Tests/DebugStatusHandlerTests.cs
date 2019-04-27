using Alignment.Domain.Sequencing;
using Gemini.Infrastructure;
using StitchingLogic;
using Xunit;

namespace Gemini.Tests
{
    public class DebugStatusHandlerTests
    {
        [Fact]
        public void AddStatusCount()
        {
            var counter = new ReadStatusCounter();
            var handler = new DebugStatusHandler(counter);
            handler.AddStatusCount("x");

            var statuses = counter.GetReadStatuses();
            Assert.Equal(1.0, statuses.Count);
            Assert.Equal(1.0, statuses["x"]);

            handler.AddStatusCount("y");
            statuses = counter.GetReadStatuses();
            Assert.Equal(2, statuses.Count);
            Assert.Equal(1, statuses["x"]);
            Assert.Equal(1, statuses["y"]);

            handler.AddStatusCount("x");
            statuses = counter.GetReadStatuses();
            Assert.Equal(2, statuses.Count);
            Assert.Equal(2, statuses["x"]);
            Assert.Equal(1, statuses["y"]);

        }

        [Fact]
        public void AddCombinedStatusStringTags()
        {
            var counter = new ReadStatusCounter();
            var handler = new DebugStatusHandler(counter);
            var pair = TestHelpers.GetPair("10M", "10M");
            pair.Read1.ReplaceOrAddStringTag("HI", "read1_hi");
            pair.Read2.ReplaceOrAddStringTag("HI", "read2_hi");

            var outAlignment = new BamAlignment(pair.Read1);
            handler.AddCombinedStatusStringTags("HI", pair.Read1, pair.Read2, outAlignment);
            Assert.Equal("read1_hi,read2_hi", outAlignment.GetStringTag("HI"));
        }

        [Fact]
        public void UpdateStatusStringTag()
        {
            var counter = new ReadStatusCounter();
            var handler = new DebugStatusHandler(counter);
            var pair = TestHelpers.GetPair("10M", "10M");
            pair.Read1.ReplaceOrAddStringTag("HI", "read1_hi");
            pair.Read2.ReplaceOrAddStringTag("HI", "read2_hi");

            var outAlignment = new BamAlignment(pair.Read1);
            outAlignment.ReplaceOrAddStringTag("HI", "nothing");

            // Should  not update
            handler.UpdateStatusStringTag("HI", "newvalue", outAlignment);
            Assert.Equal("newvalue", outAlignment.GetStringTag("HI"));
        }

        [Fact]
        public void AppendStatusStringTag()
        {
            var counter = new ReadStatusCounter();
            var handler = new DebugStatusHandler(counter);
            var pair = TestHelpers.GetPair("10M", "10M");

            pair.Read1.ReplaceOrAddStringTag("HI", "nothing");

            // Should  not update
            handler.AppendStatusStringTag("HI", "newvalue", pair.Read1);
            Assert.Equal("nothing,newvalue", pair.Read1.GetStringTag("HI"));
        }

    }
}