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
            Assert.Equal(@"C:\Out\PhasingLogs",options.LogFolder);
        }
    }
}
