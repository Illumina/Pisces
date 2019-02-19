using System.IO;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class CountsFileReaderTests
    {
        [Fact]
        public void ReadCountsFile()
        {
            var countsPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Expected.counts");

            var c = CountsFileReader.ReadCountsFile(countsPath);

            Assert.Equal(10.0, c.NumPossibleVariants);
            Assert.Equal(0.0, c.CountsByCategory[MutationCategory.AtoC]);
            Assert.Equal(2.0, c.CountsByCategory[MutationCategory.CtoG]); //two 0/1 and one 0/.

        }
    }
}
