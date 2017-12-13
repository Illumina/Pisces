using System.Collections.Generic;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Logic.VariantCalling;
using Xunit;

namespace Pisces.Tests.UnitTests.VariantCalling
{
    public class DiploidLocusProcessorTests
    {
        [Fact]
        public void ForceAllele_at_ref_site_get_ref_genotype_and_genotypeQscore()
        {
            var calledAllele1 = new CalledAllele(AlleleCategory.Snv)
            {
                Filters = new List<FilterType> { FilterType.ForcedReport,FilterType.LowDepth},
                Genotype = Genotype.AltLikeNoCall,
                GenotypeQscore = 10
            };
            var calledAllele2 = new CalledAllele(AlleleCategory.Reference)
            {
                Genotype = Genotype.HomozygousRef,
                GenotypeQscore = 100
            };

            var allelesInPos = new List<CalledAllele>
            {
                calledAllele1,
                calledAllele2
            };
            var processor = new DiploidLocusProcessor();
            processor.Process(allelesInPos);

            Assert.Equal(100,calledAllele1.GenotypeQscore);
            Assert.Equal(Genotype.HomozygousRef,calledAllele1.Genotype);

        }

        [Fact]
        public void ForceAllele_at_nocall_site_get_genotype_no_call()
        {
            var calledAllele1 = new CalledAllele(AlleleCategory.Snv)
            {
                Filters = new List<FilterType> { FilterType.ForcedReport, FilterType.LowDepth },
                Genotype = Genotype.AltLikeNoCall,
                GenotypeQscore = 10
            };
            var calledAllele2 = new CalledAllele(AlleleCategory.Insertion)
            {
                Genotype = Genotype.AltLikeNoCall,
                GenotypeQscore =20
            };

            var allelesInPos = new List<CalledAllele>
            {
                calledAllele1,
                calledAllele2
            };
            var processor = new DiploidLocusProcessor();
            processor.Process(allelesInPos);

            Assert.Equal(20, calledAllele1.GenotypeQscore);
            Assert.Equal(Genotype.AltLikeNoCall, calledAllele1.Genotype);
        }

        [Fact]
        public void ForcedAllele_at_heterozygous_site_get_genotype_others()
        {
            var calledAllele1 = new CalledAllele(AlleleCategory.Snv)
            {
                Filters = new List<FilterType> { FilterType.ForcedReport, FilterType.LowDepth },
                Genotype = Genotype.AltLikeNoCall,
                GenotypeQscore = 10
            };
            var calledAllele2 = new CalledAllele(AlleleCategory.Insertion)
            {
                Genotype = Genotype.HeterozygousAltRef,
                GenotypeQscore = 40
            };

            var allelesInPos = new List<CalledAllele>
            {
                calledAllele1,
                calledAllele2
            };
            var processor = new DiploidLocusProcessor();
            processor.Process(allelesInPos);

            Assert.Equal(40, calledAllele1.GenotypeQscore);
            Assert.Equal(Genotype.Others, calledAllele1.Genotype);
        }

        [Fact]
        public void GenotypeQScore_is_the_min_of_nonForced_Allele()
        {
            var calledAllele1 = new CalledAllele(AlleleCategory.Snv)
            {
                Filters = new List<FilterType> { FilterType.ForcedReport, FilterType.LowDepth },
                Genotype = Genotype.AltLikeNoCall,
                GenotypeQscore = 10
            };
            var calledAllele2 = new CalledAllele(AlleleCategory.Insertion)
            {
                Genotype = Genotype.HeterozygousAlt1Alt2,
                GenotypeQscore = 40
            };
            var calledAllele3 = new CalledAllele(AlleleCategory.Insertion)
            {
                Genotype = Genotype.HeterozygousAlt1Alt2,
                GenotypeQscore = 100
            };


            var allelesInPos = new List<CalledAllele>
            {
                calledAllele1,
                calledAllele2,
                calledAllele3
            };
            var processor = new DiploidLocusProcessor();
            processor.Process(allelesInPos);

            Assert.Equal(40, calledAllele1.GenotypeQscore);
            Assert.Equal(40, calledAllele2.GenotypeQscore);
            Assert.Equal(40, calledAllele3.GenotypeQscore);
            Assert.Equal(Genotype.Others, calledAllele1.Genotype);
        }

    }
}