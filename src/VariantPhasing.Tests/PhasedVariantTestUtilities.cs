using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models.Alleles;
using VariantPhasing.Models;

namespace VariantPhasing.Tests
{
    public static class PhasedVariantTestUtilities
    {

        public static CalledAllele CreateDummyAllele(
          string chrom, int position, string refAllele, string altAllele, int depth, int altCalls)
        {
            return new CalledAllele(Pisces.Domain.Types.AlleleCategory.Snv)
            {
                Chromosome = chrom,
                Coordinate = position,
                Reference = refAllele,
                Alternate = altAllele,
                TotalCoverage = depth,
                AlleleSupport = altCalls,
                Type = Pisces.Domain.Types.AlleleCategory.Snv,
                ReferenceSupport = depth - altCalls,
                VariantQscore = 100
            };
        }

        public static VcfVariant CreateDummyVariant(
            string chrom, int position, string refAllele, string altAllele, int depth, int altCalls)
        {
            return new VcfVariant()
            {
                ReferenceName = chrom,
                ReferencePosition = position,
                ReferenceAllele = refAllele,
                VariantAlleles = new[] {altAllele},
                GenotypeTagOrder = new[] {"GT", "GQ", "AD", "DP", "VF", "NL", "SB", "NC"},
                InfoTagOrder = new[] {"DP"},
                Genotypes = new List<Dictionary<string, string>>()
                {
                    new Dictionary<string, string>()
                    {
                        {"GT", "0/1"},
                        {"GQ", "100"},
                        {"AD", (depth-altCalls).ToString() +"," + altCalls.ToString()},//"6830,156"
                        {"DP", depth.ToString()},
                        { "VF", "0.156" },//"0.05"},
                        {"NL", "20"},
                        {"SB", "-100.0000"},
                        {"NC","0.0100"}
                    }
                },
                InfoFields = new Dictionary<string, string>() {{"DP", depth.ToString()} }, //
                Filters = "PASS",
                Identifier = ".",
            };
        }

        public static VeadGroup CreateVeadGroup(List<Vead> veads)
        {
            var veadgroup = new VeadGroup(veads.First());
            foreach (var vead in veads.Skip(1))
            {
                veadgroup.AddSupport(vead);
            }
            return veadgroup;
        }

        public static Vead CreateVeadFromStringArray(string name, string[,] variants)
        {
            var numVariants = variants.GetLength(0);
            var variantSites = new VariantSite[numVariants];

            for (var i = 0; i < numVariants; i++)
            {
                var vs = new VariantSite { VcfReferenceAllele = variants[i, 0], VcfAlternateAllele = variants[i, 1] };
                variantSites[i] = vs;
            }

            return new Vead(name, variantSites);

        }
    }
}