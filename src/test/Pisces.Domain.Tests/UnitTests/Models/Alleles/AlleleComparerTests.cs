using Pisces.Domain.Models.Alleles;
using TestUtilities;
using System.Collections.Generic;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models.Alleles
{
    public class AlleleComparerTests
    {
        List<string> _chrMFirstOrder = new List<string>() { "chrM", "chr1", "chr2", "chr3", "chr4", "chr5, chr6", "chr7", "chr8", "chr9", "chr10",
                                                            "chr11", "chr12", "chr13", "chr14", "chr15, chr16", "chr17", "chr18", "chr19", "chr20",
                                                            "chr21", "chr22", "chrX", "chrY" };
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

            var alleleCompareByLoci = new AlleleCompareByLoci();
            var alleleCompareByLociChrMFirst = new AlleleCompareByLoci(_chrMFirstOrder);

            // ---------------------------------------------------------------------------
            // When neither or both is on chrM, shouldn't matter if we set option to prioritize chrM
            // ---------------------------------------------------------------------------

            // Same chrom, different positions - numeric chrom
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chr9, chr9Second));
            Assert.Equal(-1, alleleCompareByLociChrMFirst.OrderAlleles(chr9, chr9Second));
            Assert.Equal(1, alleleCompareByLoci.OrderAlleles(chr9Second, chr9));
            Assert.Equal(1, alleleCompareByLociChrMFirst.OrderAlleles(chr9Second, chr9));

            // Same chrom, different positions - chrX
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chrX, chrXSecond));
            Assert.Equal(-1, alleleCompareByLociChrMFirst.OrderAlleles(chrX, chrXSecond));
            Assert.Equal(1, alleleCompareByLoci.OrderAlleles(chrXSecond, chrX));
            Assert.Equal(1, alleleCompareByLociChrMFirst.OrderAlleles(chrXSecond, chrX));

            // Same chrom, different positions - chrM
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chrM, chrMSecond));
            Assert.Equal(-1, alleleCompareByLociChrMFirst.OrderAlleles(chrM, chrMSecond));
            Assert.Equal(1, alleleCompareByLoci.OrderAlleles(chrMSecond, chrM));
            Assert.Equal(1, alleleCompareByLociChrMFirst.OrderAlleles(chrMSecond, chrM));

            // Different chroms, one is >=10 (direct string compare would not sort these chroms correctly)
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chr9, chr10));
            Assert.Equal(-1, alleleCompareByLociChrMFirst.OrderAlleles(chr9, chr10));

            // One numeric, one chrX
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chr9, chrX));
            Assert.Equal(-1, alleleCompareByLociChrMFirst.OrderAlleles(chr9, chrX));

            // Same chrom, same position
            Assert.Equal(0, alleleCompareByLoci.OrderAlleles(chr9, chr9));
            Assert.Equal(0, alleleCompareByLoci.OrderAlleles(chrX, chrX));
            Assert.Equal(0, alleleCompareByLoci.OrderAlleles(chrM, chrM));


            // ---------------------------------------------------------------------------
            // If one is on chrM, option to prioritize chrM matters
            // ---------------------------------------------------------------------------

            // One numeric, one chrM
            Assert.Equal(1, alleleCompareByLociChrMFirst.OrderAlleles(chr9, chrM));
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chr9, chrM));

            // One chrX, one chrM
            Assert.Equal(1, alleleCompareByLociChrMFirst.OrderAlleles(chrX, chrM));
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chrX, chrM));

            // ---------------------------------------------------------------------------
            // Nonstandard chroms should be below numerics and then ordered alphabetically
            // ---------------------------------------------------------------------------

            // One numeric, one weird
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chr9, nonstandardChrZ));
            Assert.Equal(-1, alleleCompareByLociChrMFirst.OrderAlleles(chr9, nonstandardChrZ));

            // One chrX, one weird
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chrX, nonstandardChrZ));
            Assert.Equal(-1, alleleCompareByLociChrMFirst.OrderAlleles(chrX, nonstandardChrZ));

            // One chrM, one weird
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chrX, nonstandardChrZ));
            Assert.Equal(-1, alleleCompareByLociChrMFirst.OrderAlleles(chrX, nonstandardChrZ));

            // One numeric, one funny
            Assert.Equal(-1, alleleCompareByLoci.OrderAlleles(chr9, nonstandardChrA));
            Assert.Equal(-1, alleleCompareByLociChrMFirst.OrderAlleles(chr9, nonstandardChrA));

            // One chrX, one funny
            Assert.Equal(1, alleleCompareByLoci.OrderAlleles(chrX, nonstandardChrA));
            Assert.Equal(1, alleleCompareByLociChrMFirst.OrderAlleles(chrX, nonstandardChrA));

            // One chrM, one funny
            Assert.Equal(1, alleleCompareByLoci.OrderAlleles(chrX, nonstandardChrA));
            Assert.Equal(1, alleleCompareByLociChrMFirst.OrderAlleles(chrX, nonstandardChrA));
        }

    }
}
