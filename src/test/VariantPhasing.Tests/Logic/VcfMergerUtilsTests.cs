using System;
using System.Collections.Generic;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using VariantPhasing.Logic;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public sealed class VcfMergerUtilsTests
    {
        [Fact]
        public void non_forced_alleles_return_as_they_are()
        {
            var calledAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Reference)
                {
                    ReferencePosition = 100,
                    ReferenceAllele = "A"
                },
                new CalledAllele
                {
                    ReferencePosition = 102,
                    ReferenceAllele = "G",
                    AlternateAllele = "T",
                },
                new CalledAllele
                {
                    ReferencePosition = 105,
                    ReferenceAllele = "T",
                    AlternateAllele = "C",
                    Filters = new List<FilterType>{FilterType.ForcedReport}

                },
                new CalledAllele
                {
                    ReferencePosition = 107,
                    ReferenceAllele = "GTG",
                    AlternateAllele = "TCA",
                }
            };

            var calledAlleleTuples = new List<Tuple<CalledAllele, string>>();

            foreach (var allele in calledAlleles)
            {
                calledAlleleTuples.Add(new Tuple<CalledAllele, string>(allele, ""));
            }

            var allelesAfterAdjust = VcfMergerUtils.AdjustForcedAllele(calledAlleleTuples);

            Assert.Equal(4,allelesAfterAdjust.Count);
        }

        [Fact]
        public void Forced_allele_is_removed_if_nonforced_alleles_contain_it()
        {
            var calledAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Mnv)
                {
                    ReferencePosition =  100,
                    ReferenceAllele = "ATCG",
                    AlternateAllele = "GTCC"
                },
                new CalledAllele
                {
                    ReferencePosition =  100,
                    ReferenceAllele = "ATCG",
                    AlternateAllele = "GTCC",
                    Filters = new List<FilterType>{FilterType.ForcedReport}
                }
            };

            var calledAlleleTuples = new List<Tuple<CalledAllele, string>>();

            foreach (var allele in calledAlleles)
            {
                calledAlleleTuples.Add(new Tuple<CalledAllele, string>(allele, ""));
            }

            var allelesAfterAdjust = VcfMergerUtils.AdjustForcedAllele(calledAlleleTuples);

            Assert.Equal(1, allelesAfterAdjust.Count);
        }

        [Fact]
        public void Forced_allele_genotype_remains_heterozygous_for_after_phasing()
        {
            var calledAlleles = new List<CalledAllele>
            {
                new CalledAllele
                {
                    ReferencePosition = 102,
                    ReferenceAllele = "GTC",
                    AlternateAllele = "TTG",
                },
                new CalledAllele
                {
                    ReferencePosition = 102,
                    ReferenceAllele = "G",
                    AlternateAllele = "C",
                    Filters = new List<FilterType>{FilterType.ForcedReport},
                    Genotype = Genotype.HeterozygousAltRef

                }
            };

            var calledAlleleTuples = new List<Tuple<CalledAllele, string>>();

            foreach (var allele in calledAlleles)
            {
                calledAlleleTuples.Add(new Tuple<CalledAllele, string>(allele, ""));
            }

            var allelesAfterAdjust = VcfMergerUtils.AdjustForcedAllele(calledAlleleTuples);

            Assert.Equal(2, allelesAfterAdjust.Count);
            Assert.Equal(Genotype.HeterozygousAltRef, allelesAfterAdjust[1].Item1.Genotype);
        }
    }
}