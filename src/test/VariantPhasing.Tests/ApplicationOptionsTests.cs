using Xunit;

namespace VariantPhasing.Tests
{
    public class ApplicationOptionsTests
    {
        [Fact]
        public void LogFolder()
        {
            var options = new ScyllaApplicationOptions();
            options.OutputDirectory = @"C:\Out";
            options.SetIODirectories("Scylla");
            Assert.Equal(@"C:\Out\ScyllaLogs", options.LogFolder);
        }
    }
}
