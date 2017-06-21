using Xunit;

namespace VariantPhasing.Tests
{
    public class ApplicationOptionsTests
    {
        [Fact]
        public void LogFolder()
        {
            var options = new PhasingApplicationOptions();
            options.OutFolder = @"C:\Out";
            Assert.Equal(@"C:\Out\PhasingLogs",options.LogFolder);
        }
    }
}
