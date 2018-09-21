using System;
using System.Linq;
using System.Collections.Generic;
using Pisces.Calculators;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;
namespace Pisces.Calculators.Tests
{
    public class GenotypeCalculatorTests
    {

        [Fact]
        [Trait("ReqID", "PICS-961")]
        public void SomaticGenotypeScenarios()
        {
            ExecuteSomaticGenotypeTest(99, 0.5f, false, Genotype.HeterozygousAltRef, new List<FilterType> { FilterType.LowDepth });
            ExecuteSomaticGenotypeTest(99, 0.5f, true, Genotype.HomozygousRef, new List<FilterType> { FilterType.LowDepth });
            ExecuteSomaticGenotypeTest(25, 0.5f, false, Genotype.AltLikeNoCall, new List<FilterType> { FilterType.LowDepth });
            ExecuteSomaticGenotypeTest(25, 0.5f, true, Genotype.RefLikeNoCall, new List<FilterType> { FilterType.LowDepth });

            ExecuteSomaticGenotypeTest(100, 0, true, Genotype.HomozygousRef, new List<FilterType> { }); // shouldnt matter what freqs are
            ExecuteSomaticGenotypeTest(100, 0.009f, false, Genotype.HomozygousAlt, new List<FilterType> { });
            ExecuteSomaticGenotypeTest(100, 0.01f, false, Genotype.HeterozygousAltRef, new List<FilterType> { });

        }

        [Fact]
        [Trait("ReqID", "PICS-961")]
        public void DiploidGenotypeScenarios()
        {
            //https://confluence.illumina.com/display/BIOINFO/Pisces+Germline+Variant+Calling+Requirements


            //req 1.1 0/0 situation
            ExecuteDiploidGenotypeTest(Genotype.HomozygousRef, 1, new List<float> { 0.80f }, new List<float> { 0.19f });  // '0/0'
            ExecuteDiploidGenotypeTest(Genotype.HomozygousRef, 0, new List<float> { 0.80f }, new List<float> { });  // '0/0'

            //req 1.2 0/1 situation
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { 0.80f }, new List<float> { 0.20f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { 0.70f }, new List<float> { 0.30f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { 0.21f }, new List<float> { 0.69f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 1, new List<float> { 0.69f }, new List<float> { 0.30f, 0.01f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { }, new List<float> { 0.20f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 0, new List<float> { }, new List<float> { 0.30f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 1, new List<float> { }, new List<float> { 0.30f, 0.01f }); // '0/1'
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 2, new List<float> { }, new List<float> { 0.01f, 0.02f, 0.30f }); // '0/1'

            //req 1.2 1/. situation
            ExecuteDiploidGenotypeTest(Genotype.AltAndNoCall, 0, new List<float> { 0.10f }, new List<float> { 0.70f }); // '0/1'


            //req 1.3 1/1 situation
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { 0.10f }, new List<float> { 0.71f }); // '1/1'
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { 0.10f }, new List<float> { 0.99f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { 0.10f }, new List<float> { 1.0f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { }, new List<float> { 0.71f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { }, new List<float> { 0.99f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 0, new List<float> { }, new List<float> { 1.0f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 1, new List<float> { 0.10f }, new List<float> { 0.99f, 0.01f });
            ExecuteDiploidGenotypeTest(Genotype.HomozygousAlt, 1, new List<float> { }, new List<float> { 0.99f, 0.01f });

            //req 2.2 ./.  Multi allelic situation -> ./.
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.20f }, new List<float> { 0.40f, 0.40f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f, 0.02f });
            ExecuteDiploidGenotypeTest(Genotype.Alt12LikeNoCall, 0, new List<float> { 0.01f }, new List<float> { 0.40f, 0.39f });
            ExecuteDiploidGenotypeTest(Genotype.Alt12LikeNoCall, 0, new List<float> { 0.0f }, new List<float> { 0.20f, 0.40f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { }, new List<float> { 0.20f, 0.40f, 0.02f });


            //req 2.3 ./. alt-like Multi allelic situation-> ./.
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f, 0.20f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.30f }, new List<float> { 0.20f, 0.30f, 0.30f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.80f }, new List<float> { 0.20f, 0.20f });


            //req 2.3 ./. alt-like Multi allelic situation-> ./.
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.20f, 0.40f, 0.20f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 2, new List<float> { 0.30f }, new List<float> { 0.20f, 0.30f, 0.30f });
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.80f }, new List<float> { 0.20f, 0.20f });

            //req 2.4.a ./. alt-like Multi allelic situation -> 0/1
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAltRef, 1, new List<float> { 0.60f }, new List<float> { 0.40f, 0.01f });

            //req 2.4.b ./. alt-like Multi allelic situation -> 1/2
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAlt1Alt2, 0, new List<float> { }, new List<float> { 0.50f, 0.50f });
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAlt1Alt2, 0, new List<float> { 0.01f }, new List<float> { 0.40f, 0.40f });
            ExecuteDiploidGenotypeTest(Genotype.HeterozygousAlt1Alt2, 1, new List<float> { 0.01f }, new List<float> { 0.35f, 0.55f, 0.01f });

            //req 2.5 depth less than required depth to call -> no call
            ExecuteDiploidGenotypeTest(Genotype.RefAndNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 1000);
            ExecuteDiploidGenotypeTest(Genotype.AltAndNoCall, 1, new List<float> { 0.10f }, new List<float> { 0.21f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 1000);
            ExecuteDiploidGenotypeTest(Genotype.RefLikeNoCall, 2, new List<float> { 0.20f }, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 10);
            ExecuteDiploidGenotypeTest(Genotype.AltLikeNoCall, 1, new List<float> { 0.10f }, new List<float> { 0.21f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 10);
        }


        private void ExecuteDiploidGenotypeTest(
            Genotype expectedGenotype, int expectedNumAllelesToPrune,
           List<float> refFrequencies, List<float> altFrequencies)
        {
            ExecuteDiploidGenotypeTest(expectedGenotype, expectedNumAllelesToPrune, refFrequencies, altFrequencies, new List<FilterType> { },1000);
        }

        private void ExecuteDiploidGenotypeTest(
            Genotype expectedGenotype, int expectedNumAllelesToPrune,
            List<float> refFrequencies, List<float> altFrequencies, List<FilterType> filters, int coverage)
        {
            var alleles = new List<CalledAllele>();
            double refFreq = 0;
            foreach (float rf in refFrequencies)
            {
                var variant = TestHelper.CreatePassingVariant(true);
                variant.AlleleSupport = (int)(rf * coverage);
                variant.TotalCoverage = (int)coverage;
                variant.ReferenceSupport = variant.AlleleSupport;
                alleles.Add(variant);
                refFreq = rf;
            }

            if (refFreq == 0)
                refFreq = (1.0 - altFrequencies.Sum());

            foreach (float vf in altFrequencies)
            {
                var variant = TestHelper.CreatePassingVariant(false);
                variant.AlleleSupport = (int)(vf * coverage);
                variant.TotalCoverage = (int)coverage;
                variant.ReferenceSupport = (int)(refFreq * coverage);
                alleles.Add(variant);
            }

            //set filters for at least one allele. they should affect all results.
            alleles[0].Filters = filters;

            DiploidGenotypeCalculator GTC = GetOriginalSettings();

            GTC.MinDepthToGenotype = 100;
            var allelesToPrune = GTC.SetGenotypes(alleles);

            Assert.Equal(expectedNumAllelesToPrune, allelesToPrune.Count);
            foreach (var allele in alleles)
            {
                Assert.Equal(expectedGenotype, allele.Genotype);
            }
        }

        public static DiploidGenotypeCalculator GetOriginalSettings()
        {
            return new DiploidGenotypeCalculator(
                new Domain.Options.DiploidThresholdingParameters(new float[] { .20F, .70F, .80F }),
                    new Domain.Options.DiploidThresholdingParameters(new float[] { .20F, .70F, .80F }),
                    0, 0, 0);
        }

        [Fact]
        [Trait("ReqID", "PICS-961")]
        private void ExecuteDiploidIndelGenotypeTest()
        {
            //test cases:
            // (1) SNP + indel + indel
            // (2) indel + SNP + SNP
            // (3) 3 indels (OK)
            // (4) 3 indels - ploidy violation

            Genotype expectedGenotype = Genotype.HeterozygousAlt1Alt2;
            int expectedNumAllelesToPrune = 1;

            // (1) SNP + indel + indel
            // should be 1/2 with the lowest freq thrown out
            List<float> refFrequencies = new List<float>() { 0.40F, 0.60F, 0.90F };
            List<float> altFrequencies = new List<float>() { 0.60F,0.40F,0.10F };
            List<string> refAllele = new List<string>() { "A", "A", "ACT" };
            List<string> altAllele = new List<string>() { "C", "AGGG", "A" };
            double coverage = 1000;

            var alleles = new List<CalledAllele>();
            for (int i=0;i<3;i++)
            {
                var variant = TestHelper.CreatePassingVariant(false);
                variant.AlleleSupport = (int)(altFrequencies[i] * coverage);
                variant.TotalCoverage = (int)coverage;
                variant.ReferenceSupport = (int)(refFrequencies[i] * coverage);
                variant.AlternateAllele = altAllele[i];
                variant.ReferenceAllele = refAllele[i];
                alleles.Add(variant);
            }
            alleles[1].Type = AlleleCategory.Insertion;
            alleles[2].Type = AlleleCategory.Deletion;

            var GTC = new DiploidGenotypeCalculator();
            GTC.MinDepthToGenotype = 100;
            var allelesToPrune = GTC.SetGenotypes(alleles);

            Assert.Equal(expectedNumAllelesToPrune, allelesToPrune.Count);
            foreach (var allele in alleles)
            {
                Assert.Equal(expectedGenotype, allele.Genotype);
                Assert.Equal(0, allele.Filters.Count());
            }

            Assert.Equal(allelesToPrune[0].ReferenceAllele,"ACT");
            Assert.Equal(allelesToPrune[0].AlternateAllele,"A");
            Assert.Equal(allelesToPrune[0].Frequency,0.10F);


            // (2) indel + SNP + SNP
            // should be 1/2 with the lowest freq thrown out
            refFrequencies = new List<float>() { 0.40F, 0.20F,0.20F};
            altFrequencies = new List<float>() { 0.60F, 0.10F,0.40F};
            refAllele = new List<string>() { "A", "A", "A" };
            altAllele = new List<string>() { "ACCAT", "G", "C" };


            alleles = new List<CalledAllele>();
            for (int i = 0; i < 3; i++)
            {
                var variant = TestHelper.CreatePassingVariant(false);
                variant.AlleleSupport = (int)(altFrequencies[i] * coverage);
                variant.TotalCoverage = (int)coverage;
                variant.ReferenceSupport = (int)(refFrequencies[i] * coverage);
                variant.AlternateAllele = altAllele[i];
                variant.ReferenceAllele = refAllele[i];
                alleles.Add(variant);
            }
            alleles[0].Type = AlleleCategory.Insertion;

            GTC = new DiploidGenotypeCalculator();
            GTC.MinDepthToGenotype = 100;
            allelesToPrune = GTC.SetGenotypes(alleles);

            Assert.Equal(expectedNumAllelesToPrune, allelesToPrune.Count);
            foreach (var allele in alleles)
            {
                Assert.Equal(expectedGenotype, allele.Genotype);
                Assert.Equal(0, allele.Filters.Count());
            }

            Assert.Equal(allelesToPrune[0].ReferenceAllele, "A");
            Assert.Equal(allelesToPrune[0].AlternateAllele, "G");
            Assert.Equal(allelesToPrune[0].Frequency, 0.10F);


            // (3) 3 indels (OK)
            // should be 1/2 with the lowest freq thrown out
            refFrequencies = new List<float>() { 0.40F, 0.90F,0.60F};
            altFrequencies = new List<float>() { 0.60F, 0.10F,0.40F};
            refAllele = new List<string>() { "A", "ACT", "A" };
            altAllele = new List<string>() { "ACCAT", "A", "CC" };

            alleles = new List<CalledAllele>();
            for (int i = 0; i < 3; i++)
            {
                var variant = TestHelper.CreatePassingVariant(false);
                variant.AlleleSupport = (int)(altFrequencies[i] * coverage);
                variant.TotalCoverage = (int)coverage;
                variant.ReferenceSupport = (int)(refFrequencies[i] * coverage);
                variant.AlternateAllele = altAllele[i];
                variant.ReferenceAllele = refAllele[i];
                alleles.Add(variant);
            }
            alleles[0].Type = AlleleCategory.Insertion;
            alleles[1].Type = AlleleCategory.Deletion;
            alleles[2].Type = AlleleCategory.Insertion;

            GTC = new DiploidGenotypeCalculator();
            GTC.MinDepthToGenotype = 100;
            allelesToPrune = GTC.SetGenotypes(alleles);

            Assert.Equal(expectedNumAllelesToPrune, allelesToPrune.Count);
            foreach (var allele in alleles)
            {
                Assert.Equal(expectedGenotype, allele.Genotype);
                Assert.Equal(0, allele.Filters.Count());
            }

            Assert.Equal(allelesToPrune[0].ReferenceAllele, "ACT");
            Assert.Equal(allelesToPrune[0].AlternateAllele, "A");
            Assert.Equal(allelesToPrune[0].Frequency, 0.10F);


            // (4) 3 indels - ploidy violation
            // should be ./. with the lowest freq thrown out
            refFrequencies = new List<float>() { 0.60F, 0.60F, 0.60F };
            altFrequencies = new List<float>() { 0.31F, 0.30F, 0.31F };
            refAllele = new List<string>() { "A", "ACT", "A" };
            altAllele = new List<string>() { "ACCAT", "A", "AC" };

            expectedGenotype = Genotype.Alt12LikeNoCall;
            alleles = new List<CalledAllele>();
            for (int i = 0; i < 3; i++)
            {
                var variant = TestHelper.CreatePassingVariant(false);
                variant.AlleleSupport = (int)(altFrequencies[i] * coverage);
                variant.TotalCoverage = (int)coverage;
                variant.ReferenceSupport = (int)(refFrequencies[i] * coverage);
                variant.AlternateAllele = altAllele[i];
                variant.ReferenceAllele = refAllele[i];
                alleles.Add(variant);
            }
            alleles[0].Type = AlleleCategory.Insertion;
            alleles[1].Type = AlleleCategory.Deletion;
            alleles[2].Type = AlleleCategory.Insertion;

            GTC = new DiploidGenotypeCalculator();
            GTC.MinDepthToGenotype = 100;
            allelesToPrune = GTC.SetGenotypes(alleles);

            Assert.Equal(expectedNumAllelesToPrune, allelesToPrune.Count);
            foreach (var allele in alleles)
            {
                Assert.Equal(expectedGenotype, allele.Genotype);
                Assert.Equal(FilterType.MultiAllelicSite, allele.Filters[0]);
            }

            Assert.Equal(allelesToPrune[0].ReferenceAllele, "ACT");
            Assert.Equal(allelesToPrune[0].AlternateAllele, "A");
            Assert.Equal(allelesToPrune[0].Frequency, 0.30F);
        }


        private void ExecuteSomaticGenotypeTest(int totalCoverage, float refFrequency, bool isReference,
             Genotype expectedGenotype, List<FilterType> filters)
        {
            var variant = TestHelper.CreatePassingVariant(isReference);
            variant.Filters = filters;

            variant.TotalCoverage = totalCoverage;
            if (!isReference)
            {
                var refSupport = (int)(refFrequency * totalCoverage);
                variant.AlleleSupport = totalCoverage - refSupport;
                variant.ReferenceSupport = refSupport;
            }

            var GTC = new SomaticGenotypeCalculator();
            GTC.MinDepthToGenotype = 30;
            var allelesToPrune = GTC.SetGenotypes(new List<CalledAllele> { variant });

            Assert.Equal(0, allelesToPrune.Count);
            Assert.Equal(expectedGenotype, variant.Genotype);
        }

        [Fact]
        [Trait("ReqID", "PICS-961")]
        public void DiploidScenariosWithIndels()
        {
            // what if the frequencies of co-located variants are identical?
            //PICS-845 Handling co-located indels
            //chr19   3543478 rs763885961 GCC     G       45      LowDP DP = 6; GT: GQ: AD: DP: VF: NL: SB: NC./.:0:3,3:6:0.500:20:-100.0000:0.0000
            //chr19   3543478 rs752455174 GC      G       45      LowDP DP = 6;  GT: GQ: AD: DP: VF: NL: SB: NC./.:0:3,3:6:0.500:20:-100.0000:0.0000

            var indel1 = TestHelper.CreatePassingVariant(false);
            indel1.ReferenceAllele = "GCC";
            indel1.AlternateAllele = "G";
            indel1.TotalCoverage = 7;
            indel1.AlleleSupport = 3;

            var indel2 = TestHelper.CreatePassingVariant(false);
            indel2.ReferenceAllele = "GC";
            indel2.AlternateAllele = "G";
            indel2.TotalCoverage = 7;
            indel2.AlleleSupport = 3;

            var GTC = GetOriginalSettings();

            var allelesToPrune = GTC.SetGenotypes(new List<CalledAllele> { indel1, indel2 });

            Assert.Equal(0, allelesToPrune.Count);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, indel1.Genotype);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, indel2.Genotype);

            //check order does not matter
            allelesToPrune = GTC.SetGenotypes(new List<CalledAllele> { indel2, indel1 });

            Assert.Equal(0, allelesToPrune.Count);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, indel1.Genotype);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, indel2.Genotype);

            var comparer = new AlleleCompareByLociAndAllele();
            Assert.Equal(1, comparer.Compare(indel1, indel2));
            Assert.Equal(-1, comparer.Compare(indel2, indel1));

            //lets make double-sure this is deterministic
            var SortedList1 = GenotypeCalculatorUtilities.FilterAndOrderAllelesByFrequency(
                new List<CalledAllele> { indel1, indel2 }, allelesToPrune, 0.01);

            var SortedList2 = GenotypeCalculatorUtilities.FilterAndOrderAllelesByFrequency(
                new List<CalledAllele> { indel2, indel1 }, allelesToPrune, 0.01);

            Assert.Equal(SortedList1[0], SortedList2[0]);
            Assert.Equal(SortedList1[1], SortedList2[1]);
        }


        [Fact]
        [Trait("ReqID", "PICS-961")]
        public void DiploidScenariosWithMNVs()
        {
            // what if the frequencies of co-located variants are identical?
            //PICS-845 Handling co-located indels (same issue could happen with MNVs, so lets check that)
            
            var mnv1 = TestHelper.CreatePassingVariant(false);
            mnv1.ReferenceAllele = "GCC";
            mnv1.AlternateAllele = "GAG";
            mnv1.TotalCoverage = 7;
            mnv1.AlleleSupport = 3;

            var mnv2 = TestHelper.CreatePassingVariant(false);
            mnv2.ReferenceAllele = "GCC";
            mnv2.AlternateAllele = "GCG";
            mnv2.TotalCoverage = 7;
            mnv2.AlleleSupport = 3;

            var GTC = GetOriginalSettings();

            var allelesToPrune = GTC.SetGenotypes(new List<CalledAllele> { mnv1, mnv2 });

            Assert.Equal(0, allelesToPrune.Count);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, mnv1.Genotype);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, mnv2.Genotype);

            //check order does not matter
            allelesToPrune = GTC.SetGenotypes(new List<CalledAllele> { mnv2, mnv1 });

            Assert.Equal(0, allelesToPrune.Count);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, mnv1.Genotype);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, mnv2.Genotype);

            var comparer = new AlleleCompareByLociAndAllele();
            Assert.Equal(-1, comparer.Compare(mnv1, mnv2));
            Assert.Equal(1, comparer.Compare(mnv2, mnv1));

            //lets make double-sure this is deterministic
            var SortedList1 = GenotypeCalculatorUtilities.FilterAndOrderAllelesByFrequency(
                new List<CalledAllele> { mnv1, mnv2 }, allelesToPrune, 0.01);

            var SortedList2 = GenotypeCalculatorUtilities.FilterAndOrderAllelesByFrequency(
                new List<CalledAllele> { mnv2, mnv1 }, allelesToPrune, 0.01);

            Assert.Equal(SortedList1[0], SortedList2[0]);
            Assert.Equal(SortedList1[1], SortedList2[1]);
        }

       
    }
}
