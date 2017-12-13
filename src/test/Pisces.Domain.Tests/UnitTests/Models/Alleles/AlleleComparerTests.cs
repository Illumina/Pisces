using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models.Alleles
{
    public class AlleleComparerTests
    {
        [Fact]
        public void OrderVariants()
        {
            var chr10 = TestHelper.CreateDummyAllele("chr10", 123, "A", "C", 1000, 156);
            var chrX = TestHelper.CreateDummyAllele("chrX", 123, "A", "C", 1000, 156);
            var chrXSecond = TestHelper.CreateDummyAllele("chrX", 124, "A", "C", 1000, 156);
            var chrM = TestHelper.CreateDummyAllele("chrM", 123, "A", "C", 1000, 156);
            var chrMSecond = TestHelper.CreateDummyAllele("chrM", 124, "A", "C", 1000, 156);
            var chr9 = TestHelper.CreateDummyAllele("chr9", 123, "A", "C", 1000, 156);
            var chr9Second = TestHelper.CreateDummyAllele("chr9", 124, "A", "C", 1000, 156);
            var nonstandardChrZ = TestHelper.CreateDummyAllele("chrZ", 123, "A", "C", 1000, 156);
            var nonstandardChrA = TestHelper.CreateDummyAllele("chrA", 123, "A", "C", 1000, 156);

            // ---------------------------------------------------------------------------
            // When neither or both is on chrM, shouldn't matter if we set option to prioritize chrM
            // ---------------------------------------------------------------------------

            // Same chrom, different positions - numeric chrom
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, chr9Second, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, chr9Second, false));
            Assert.Equal(1, AlleleComparer.OrderAlleles(chr9Second, chr9, true));
            Assert.Equal(1, AlleleComparer.OrderAlleles(chr9Second, chr9, false));

            // Same chrom, different positions - chrX
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chrX, chrXSecond, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chrX, chrXSecond, false));
            Assert.Equal(1, AlleleComparer.OrderAlleles(chrXSecond, chrX, true));
            Assert.Equal(1, AlleleComparer.OrderAlleles(chrXSecond, chrX, false));

            // Same chrom, different positions - chrM
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chrM, chrMSecond, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chrM, chrMSecond, false));
            Assert.Equal(1, AlleleComparer.OrderAlleles(chrMSecond, chrM, true));
            Assert.Equal(1, AlleleComparer.OrderAlleles(chrMSecond, chrM, false));

            // Different chroms, one is >=10 (direct string compare would not sort these chroms correctly)
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, chr10, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, chr10, false));

            // One numeric, one chrX
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, chrX, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, chrX, false));

            // Same chrom, same position
            Assert.Equal(0, AlleleComparer.OrderAlleles(chr9, chr9, true));
            Assert.Equal(0, AlleleComparer.OrderAlleles(chrX, chrX, true));
            Assert.Equal(0, AlleleComparer.OrderAlleles(chrM, chrM, true));


            // ---------------------------------------------------------------------------
            // If one is on chrM, option to prioritize chrM matters
            // ---------------------------------------------------------------------------

            // One numeric, one chrM
            Assert.Equal(1, AlleleComparer.OrderAlleles(chr9, chrM, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, chrM, false));

            // One chrX, one chrM
            Assert.Equal(1, AlleleComparer.OrderAlleles(chrX, chrM, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chrX, chrM, false));

            // ---------------------------------------------------------------------------
            // Nonstandard chroms should be below numerics and then ordered alphabetically
            // ---------------------------------------------------------------------------

            // One numeric, one weird
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, nonstandardChrZ, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, nonstandardChrZ, false));

            // One chrX, one weird
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chrX, nonstandardChrZ, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chrX, nonstandardChrZ, false));

            // One chrM, one weird
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chrX, nonstandardChrZ, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chrX, nonstandardChrZ, false));

            // One numeric, one funny
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, nonstandardChrA, true));
            Assert.Equal(-1, AlleleComparer.OrderAlleles(chr9, nonstandardChrA, false));

            // One chrX, one funny
            Assert.Equal(1, AlleleComparer.OrderAlleles(chrX, nonstandardChrA, true));
            Assert.Equal(1, AlleleComparer.OrderAlleles(chrX, nonstandardChrA, false));

            // One chrM, one funny
            Assert.Equal(1, AlleleComparer.OrderAlleles(chrX, nonstandardChrA, true));
            Assert.Equal(1, AlleleComparer.OrderAlleles(chrX, nonstandardChrA, false));
        }

    }
}
