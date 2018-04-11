using Xunit;

namespace VariantPhasing.Tests
{
    public class FactoryTests
    {
        [Fact]
        public void VcfPath()
        {
            var options = new ScyllaApplicationOptions() {VcfPath = "testPath"};
            var factory = new Factory(options);
            Assert.Equal("testPath", factory.VcfPath);
        }
    }
}
