using Pisces.Domain.Models;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models
{
    public class StrandBiasStatsTests
    {
        [Fact]
        public void Constructor()
        {
            var strandBiasStats = new StrandBiasStats(30, 300);

            Assert.Equal(30, strandBiasStats.Support);
            Assert.Equal(300, strandBiasStats.Coverage);
            Assert.Equal(0.1, strandBiasStats.Frequency);
        }
    }
}