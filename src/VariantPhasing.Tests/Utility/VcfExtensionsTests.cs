using System.Collections.Generic;
using System.IO;
using Pisces.IO;
using TestUtilities;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace VariantPhasing.Tests.Utility
{
    public class VcfExtensionsTests
    {
        [Fact]
        public void OrderVariants()
        {
            var chr10 = PhasedVariantTestUtilities.CreateDummyVariant("chr10", 123, "A", "C", 1000, 156);
            var chrX = PhasedVariantTestUtilities.CreateDummyVariant("chrX", 123, "A", "C", 1000, 156);
            var chrXSecond = PhasedVariantTestUtilities.CreateDummyVariant("chrX", 124, "A", "C", 1000, 156);
            var chrM = PhasedVariantTestUtilities.CreateDummyVariant("chrM", 123, "A", "C", 1000, 156);
            var chrMSecond = PhasedVariantTestUtilities.CreateDummyVariant("chrM", 124, "A", "C", 1000, 156);
            var chr9 = PhasedVariantTestUtilities.CreateDummyVariant("chr9", 123, "A", "C", 1000, 156);
            var chr9Second = PhasedVariantTestUtilities.CreateDummyVariant("chr9", 124, "A", "C", 1000, 156);
            var nonstandardChrZ = PhasedVariantTestUtilities.CreateDummyVariant("chrZ", 123, "A", "C", 1000, 156);
            var nonstandardChrA = PhasedVariantTestUtilities.CreateDummyVariant("chrA", 123, "A", "C", 1000, 156);

            // ---------------------------------------------------------------------------
            // When neither or both is on chrM, shouldn't matter if we set option to prioritize chrM
            // ---------------------------------------------------------------------------

            // Same chrom, different positions - numeric chrom
            Assert.Equal(-1, Extensions.OrderVariants(chr9, chr9Second, true));
            Assert.Equal(-1, Extensions.OrderVariants(chr9, chr9Second, false));
            Assert.Equal(1, Extensions.OrderVariants(chr9Second, chr9, true));
            Assert.Equal(1, Extensions.OrderVariants(chr9Second, chr9, false));

            // Same chrom, different positions - chrX
            Assert.Equal(-1, Extensions.OrderVariants(chrX, chrXSecond, true));
            Assert.Equal(-1, Extensions.OrderVariants(chrX, chrXSecond, false));
            Assert.Equal(1, Extensions.OrderVariants(chrXSecond, chrX, true));
            Assert.Equal(1, Extensions.OrderVariants(chrXSecond, chrX, false));

            // Same chrom, different positions - chrM
            Assert.Equal(-1, Extensions.OrderVariants(chrM, chrMSecond, true));
            Assert.Equal(-1, Extensions.OrderVariants(chrM, chrMSecond, false));
            Assert.Equal(1, Extensions.OrderVariants(chrMSecond, chrM, true));
            Assert.Equal(1, Extensions.OrderVariants(chrMSecond, chrM, false));

            // Different chroms, one is >=10 (direct string compare would not sort these chroms correctly)
            Assert.Equal(-1, Extensions.OrderVariants(chr9, chr10, true));
            Assert.Equal(-1, Extensions.OrderVariants(chr9, chr10, false));

            // One numeric, one chrX
            Assert.Equal(-1, Extensions.OrderVariants(chr9, chrX, true));
            Assert.Equal(-1, Extensions.OrderVariants(chr9, chrX, false));

            // Same chrom, same position
            Assert.Equal(0, Extensions.OrderVariants(chr9, chr9, true));
            Assert.Equal(0, Extensions.OrderVariants(chrX, chrX, true));
            Assert.Equal(0, Extensions.OrderVariants(chrM, chrM, true));


            // ---------------------------------------------------------------------------
            // If one is on chrM, option to prioritize chrM matters
            // ---------------------------------------------------------------------------

            // One numeric, one chrM
            Assert.Equal(1, Extensions.OrderVariants(chr9, chrM, true));
            Assert.Equal(-1, Extensions.OrderVariants(chr9, chrM, false));

            // One chrX, one chrM
            Assert.Equal(1, Extensions.OrderVariants(chrX, chrM, true));
            Assert.Equal(-1, Extensions.OrderVariants(chrX, chrM, false));

            // ---------------------------------------------------------------------------
            // Nonstandard chroms should be below numerics and then ordered alphabetically
            // ---------------------------------------------------------------------------

            // One numeric, one weird
            Assert.Equal(-1, Extensions.OrderVariants(chr9, nonstandardChrZ, true));
            Assert.Equal(-1, Extensions.OrderVariants(chr9, nonstandardChrZ, false));

            // One chrX, one weird
            Assert.Equal(-1, Extensions.OrderVariants(chrX, nonstandardChrZ, true));
            Assert.Equal(-1, Extensions.OrderVariants(chrX, nonstandardChrZ, false));

            // One chrM, one weird
            Assert.Equal(-1, Extensions.OrderVariants(chrX, nonstandardChrZ, true));
            Assert.Equal(-1, Extensions.OrderVariants(chrX, nonstandardChrZ, false));

            // One numeric, one funny
            Assert.Equal(-1, Extensions.OrderVariants(chr9, nonstandardChrA, true));
            Assert.Equal(-1, Extensions.OrderVariants(chr9, nonstandardChrA, false));

            // One chrX, one funny
            Assert.Equal(1, Extensions.OrderVariants(chrX, nonstandardChrA, true));
            Assert.Equal(1, Extensions.OrderVariants(chrX, nonstandardChrA, false));

            // One chrM, one funny
            Assert.Equal(1, Extensions.OrderVariants(chrX, nonstandardChrA, true));
            Assert.Equal(1, Extensions.OrderVariants(chrX, nonstandardChrA, false));
        }

        [Fact]
        public void Convert()
        {
            var vcfVar = PhasedVariantTestUtilities.CreateDummyVariant("chr10", 123, "A", "C", 1000, 156);
            vcfVar.Genotypes[0]["GT"] = "0/1";
            var allele = Extensions.ConvertUnpackedVariant(vcfVar);

            Assert.Equal(vcfVar.ReferenceName, allele.Chromosome);
            Assert.Equal(vcfVar.VariantAlleles[0], allele.Alternate);
            Assert.Equal(vcfVar.ReferenceAllele, allele.Reference);
            Assert.Equal(vcfVar.ReferencePosition, allele.Coordinate);
            Assert.Equal(new List<FilterType>() { }, allele.Filters);
            Assert.Equal(Genotype.HeterozygousAltRef, allele.Genotype);
            Assert.Equal(AlleleCategory.Snv, allele.Type);

            vcfVar.Genotypes[0]["GT"] = "./.";
            vcfVar.Filters = "R5x9";
            allele = Extensions.ConvertUnpackedVariant(vcfVar);

            Assert.Equal(vcfVar.ReferenceName, allele.Chromosome);
            Assert.Equal(vcfVar.VariantAlleles[0], allele.Alternate);
            Assert.Equal(vcfVar.ReferenceAllele, allele.Reference);
            Assert.Equal(vcfVar.ReferencePosition, allele.Coordinate);
            Assert.Equal(new List<FilterType>() { FilterType.RMxN}, allele.Filters);
            Assert.Equal(Genotype.AltLikeNoCall, allele.Genotype);
            Assert.Equal(AlleleCategory.Snv, allele.Type);

            vcfVar.Genotypes[0]["GT"] = "1/2";
            vcfVar.Filters = "R5x9;SB";
            allele = Extensions.ConvertUnpackedVariant(vcfVar);

            Assert.Equal(vcfVar.ReferenceName, allele.Chromosome);
            Assert.Equal(vcfVar.VariantAlleles[0], allele.Alternate);
            Assert.Equal(vcfVar.ReferenceAllele, allele.Reference);
            Assert.Equal(vcfVar.ReferencePosition, allele.Coordinate);
            Assert.Equal(new List<FilterType>() { FilterType.RMxN, FilterType.StrandBias }, allele.Filters);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, allele.Genotype);
            Assert.Equal(AlleleCategory.Snv, allele.Type);

            vcfVar.Genotypes[0]["GT"] = "1/1";
            vcfVar.Filters = "R8;q30";
            allele = Extensions.ConvertUnpackedVariant(vcfVar);

            Assert.Equal(vcfVar.ReferenceName, allele.Chromosome);
            Assert.Equal(new List<FilterType>() { FilterType.IndelRepeatLength, FilterType.LowVariantQscore }, allele.Filters);
            Assert.Equal(Genotype.HomozygousAlt, allele.Genotype);
            Assert.Equal(AlleleCategory.Snv, allele.Type);

            vcfVar.Genotypes[0]["GT"] = "1/1";
            vcfVar.Filters = "lowvariantfreq;multiallelicsite";
            allele = Extensions.ConvertUnpackedVariant(vcfVar);

            Assert.Equal(vcfVar.ReferenceName, allele.Chromosome);
            Assert.Equal(new List<FilterType>() { FilterType.LowVariantFrequency, FilterType.MultiAllelicSite }, allele.Filters);
            Assert.Equal(Genotype.HomozygousAlt, allele.Genotype);
            Assert.Equal(AlleleCategory.Snv, allele.Type);
        }
    }
}
