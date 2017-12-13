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
        public void Convert()
        {
            var vcfVar = PhasedVariantTestUtilities.CreateDummyVariant("chr10", 123, "A", "C", 1000, 156);
            vcfVar.Genotypes[0]["GT"] = "0/1";
            var allele = Extensions.ConvertUnpackedVariant(vcfVar);

            Assert.Equal(vcfVar.ReferenceName, allele.Chromosome);
            Assert.Equal(vcfVar.VariantAlleles[0], allele.AlternateAllele);
            Assert.Equal(vcfVar.ReferenceAllele, allele.ReferenceAllele);
            Assert.Equal(vcfVar.ReferencePosition, allele.ReferencePosition);
            Assert.Equal(new List<FilterType>() { }, allele.Filters);
            Assert.Equal(Genotype.HeterozygousAltRef, allele.Genotype);
            Assert.Equal(AlleleCategory.Snv, allele.Type);
            Assert.Equal(0.0100f, allele.FractionNoCalls);

            vcfVar.Genotypes[0]["GT"] = "./.";
            vcfVar.Filters = "R5x9";
            allele = Extensions.ConvertUnpackedVariant(vcfVar);

            Assert.Equal(vcfVar.ReferenceName, allele.Chromosome);
            Assert.Equal(vcfVar.VariantAlleles[0], allele.AlternateAllele);
            Assert.Equal(vcfVar.ReferenceAllele, allele.ReferenceAllele);
            Assert.Equal(vcfVar.ReferencePosition, allele.ReferencePosition);
            Assert.Equal(new List<FilterType>() { FilterType.RMxN}, allele.Filters);
            Assert.Equal(Genotype.AltLikeNoCall, allele.Genotype);
            Assert.Equal(AlleleCategory.Snv, allele.Type);

            vcfVar.Genotypes[0]["GT"] = "1/2";
            vcfVar.Filters = "R5x9;SB";
            allele = Extensions.ConvertUnpackedVariant(vcfVar);

            Assert.Equal(vcfVar.ReferenceName, allele.Chromosome);
            Assert.Equal(vcfVar.VariantAlleles[0], allele.AlternateAllele);
            Assert.Equal(vcfVar.ReferenceAllele, allele.ReferenceAllele);
            Assert.Equal(vcfVar.ReferencePosition, allele.ReferencePosition);
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
