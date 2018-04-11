using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.IO.Tests.UnitTests
{
    public class VcfReaderExtensionsTests 
    {
   
        [Fact]
        public void GetVariantsByChromosome()
        {
            var vcfReader =
                new VcfReader(Path.Combine(TestPaths.LocalTestDataDirectory, "VcfReader_Extensions.vcf"));

            //Simple case
            var output = vcfReader.GetVariantsByChromosome(true, true,
                new List<AlleleCategory> { AlleleCategory.Insertion, AlleleCategory.Mnv });
            Assert.Equal(1, output.Count);
            Assert.True(output.ContainsKey("chr1"));
            var candidateAlleles = new List<CandidateAllele>();
            output.TryGetValue("chr1", out candidateAlleles);
            Assert.Equal(2, candidateAlleles.Count);
            Assert.Equal(AlleleCategory.Mnv, candidateAlleles[0].Type);
            Assert.Equal(AlleleCategory.Insertion, candidateAlleles[1].Type);

            //Custom rule
            var filteredVcfReader =
                new VcfReader(Path.Combine(TestPaths.LocalTestDataDirectory, "VcfReader_Extensions.vcf"));
            var filteredOutput = filteredVcfReader.GetVariantsByChromosome(true, true,
                new List<AlleleCategory> { AlleleCategory.Insertion, AlleleCategory.Mnv }, candidate => candidate.ReferenceAllele.Length > 3);
            Assert.Equal(1, filteredOutput.Count);
            Assert.True(filteredOutput.ContainsKey("chr1"));
            var filteredCandidateAlleles = new List<CandidateAllele>();
            filteredOutput.TryGetValue("chr1", out filteredCandidateAlleles);
            Assert.Equal(1, filteredCandidateAlleles.Count);
            Assert.False(filteredCandidateAlleles.Any(c => c.ReferenceAllele.Length > 3));

        }

    }

}
