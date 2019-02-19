using System.IO;
using Xunit;

namespace GeminiMulti.Tests
{
    public class CliTaskCreatorTests
    {
        [Fact]
        public void GetCliTask()
        {
            var creator = new CliTaskCreator();
            var task = creator.GetCliTask(new[] {"--args1", "1thing", "-args2", "another"}, "chr1", Path.Combine("path", "with spaces","myexe"),
                "Outdir", 1);
            Assert.Equal("\"path\\with spaces\\myexe\" --args1 1thing -args2 another --chromRefId \"1\" --outFolder \"Outdir\"", task.CommandLineArguments);
        }
    }
}