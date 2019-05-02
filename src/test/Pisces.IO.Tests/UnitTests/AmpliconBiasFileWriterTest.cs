using System.IO;
using System.Linq;
using System.Collections.Generic;
using TestUtilities;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;
using Pisces.Calculators;

namespace Pisces.IO.Tests.UnitTests
{
    public class AmpliconBiasFileWriterTest

    {
        [Fact]
        [Trait("ReqID", "SDS-27")]
        public void Write()
        {
            var outFolder = Path.Combine(TestPaths.LocalScratchDirectory, "AmpliconBiasTestOutput");
            var outputFile = Path.Combine(outFolder, "AmpliconBiasWriterTests.txt");
            TestHelper.RecreateDirectory(outFolder);

            //check writing

            var chromosome = "chr1";
            var reference = "A";
            var alternate = "T";
            var position = 123;

            var BaseCalledAlleles = new List<CalledAllele>();
            var variant = new CalledAllele(AlleleCategory.Snv)
            {
                Chromosome = chromosome,
                ReferenceAllele = reference,
                AlternateAllele = alternate,
                ReferencePosition = position,
                SupportByAmplicon = new Domain.Models.AmpliconCounts
                {
                    AmpliconNames = new string[] { "Amplicon1", "Amplicon2", "Amplicon3" },
                    CountsForAmplicon = new int[] { 12, 4, 5 }
                },
                CoverageByAmplicon = new Domain.Models.AmpliconCounts
                {
                    AmpliconNames = new string[] { "Amplicon1", "Amplicon2", "Amplicon3" },
                    CountsForAmplicon = new int[] { 120, 4000, 5000 }
                }
            };
            AmpliconBiasCalculator.Compute(variant, 100, 0.01F);
            BaseCalledAlleles.Add(variant);
            var writer = new AmpliconBiasFileWriter(outputFile);

            writer.WriteHeader();
            writer.Write(BaseCalledAlleles);
            writer.Dispose();


            //check it reads in
            //Chr,Position,Reference,Alternate,Name,freq,obs support, expected support, prob its real, confidence Qscore, bias detected ?, Filter Variant?
            //chr1,123,A,T,Amplicon1,0.1,12,12,1,100,False,True,
            //chr1,123,A,T,Amplicon2,0.001,4,400,2.06343002707725E-165,0,True,True,
            //chr1,123,A,T,Amplicon3,0.001,5,500,1.87406134646469E-206,0,True,True,

            var biasFileContents = File.ReadAllLines(outputFile);
            Assert.True(biasFileContents.Length == 4);

            var header = biasFileContents.First();
            var expectedHeader = "Chr,Position,Reference,Alternate,Name,freq,obs support, expected support, prob its real, confidence Qscore, bias detected?, Filter Variant?";

            var data = biasFileContents.Skip(1).First();
            var expectedData = "chr1,123,A,T,Amplicon1,0.1,12,12,1,100,False,True,";


            // Make sure well-formed and populated with the right data
            Assert.Equal(expectedHeader, header);
            Assert.Equal(expectedData, data);
           
            //check IO
            Assert.Throws<IOException>(() => writer.WriteHeader());
            Assert.Throws<IOException>(() => writer.Write(BaseCalledAlleles));
            writer.Dispose();
        }

    }
}