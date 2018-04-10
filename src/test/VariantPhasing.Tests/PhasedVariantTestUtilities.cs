using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models.Alleles;
using VariantPhasing.Models;

namespace VariantPhasing.Tests
{
    public static class PhasedVariantTestUtilities
    {

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