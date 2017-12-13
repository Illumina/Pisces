using Pisces.Domain.Options;
using Xunit;

namespace Pisces.Domain.Tests
{
    public class OptionsHelperTests
    {
        [Fact]
        public void ListOfParamsToStringArray()
        {
            var paramsString = "[abc,def]";

            Assert.Equal(new[] { "abc", "def" }, OptionHelpers.ListOfParamsToStringArray(paramsString));
        }
    }
}
