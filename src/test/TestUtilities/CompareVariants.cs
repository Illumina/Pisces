using System.Collections.Generic;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;
using Xunit;

namespace TestUtilities
{
    public static class CompareVariants
    {
        public static void AssertSameVariants_QScoreAgnostic(string file1, string file2)
        {
            var variant1List = new List<CalledAllele>();
            var variant2List = new List<CalledAllele>();

            using (var reader1 = new AlleleReader(file1))
            {
                reader1.GetNextVariants(out variant1List);
                using (var reader2 = new AlleleReader(file2))
                {
                    reader2.GetNextVariants(out variant2List);

                    Assert.Equal(variant1List.Count, variant2List.Count);

                    for (int i = 0; i < variant1List.Count; i++)
                    {
                        var variant1 = variant1List[i];
                        var variant2 = variant2List[i];

                        Assert.Equal(variant1.Genotype, variant2.Genotype);
                        Assert.Equal(variant1.AlternateAllele, variant2.AlternateAllele);
                    }
                }
            }
        }
    }
}
