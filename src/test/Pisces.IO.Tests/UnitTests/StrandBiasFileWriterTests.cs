using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;
using Pisces.Calculators;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.IO.Tests.UnitTests
{
    public class StrandBiasFileWriterTests
    {
        [Fact]
        [Trait("ReqID","SDS-27")]
        public void Write()
        {
            var outFolder = Path.Combine(TestPaths.LocalScratchDirectory,"StandBiasTestOutput");
            var outputFile = Path.Combine(outFolder, "StrandBiasWriterTests.txt");
            TestHelper.RecreateDirectory(outFolder);

            //check writing

            var chromosome = "chr1";
            var reference = "TTT";
            var alternate = "T";
            var position = 123;

            var BaseCalledAlleles = new List<CalledAllele>();
            var variant = new CalledAllele(AlleleCategory.Deletion)
            {
                Chromosome = chromosome,
                ReferenceAllele = reference,
                AlternateAllele = alternate,
                ReferencePosition = position,
                StrandBiasResults = new BiasResults()
                {
                    BiasAcceptable = true,
                    BiasScore = 1,
                    CovPresentOnBothStrands = true,
                    ForwardStats = StrandBiasCalculator.CreateStats(10, 100, .1, .1, StrandBiasModel.Poisson),
                    GATKBiasScore = .2,
                    OverallStats = StrandBiasCalculator.CreateStats(20, 200, .2, .2, StrandBiasModel.Poisson),
                    ReverseStats = StrandBiasCalculator.CreateStats(30, 300, .3, .3, StrandBiasModel.Poisson),
                    StitchedStats = StrandBiasCalculator.CreateStats(40, 400, .4, .4, StrandBiasModel.Poisson),
                    TestAcceptable = true,
                    TestScore = .5,
                    VarPresentOnBothStrands = true,
                }
            };
            BaseCalledAlleles.Add(variant);
            var writer = new StrandBiasFileWriter(outputFile);

            writer.WriteHeader();
            writer.Write(BaseCalledAlleles);
            writer.Dispose();


            //check it reads in

            var biasFileContents = File.ReadAllLines(outputFile);
            Assert.True(biasFileContents.Length == 2);

            var header = biasFileContents.First().Split('\t');
            var data = biasFileContents.Skip(1).First().Split('\t');
            var dict = header.Select((a, i) => new { key = a, data = data[i] })
                          .ToDictionary(b => b.key, c => c.data);

            // Make sure well-formed and populated with the right data
            Assert.Equal(chromosome, dict["Chr"]);
            Assert.Equal(position.ToString(), dict["Position"]);
            Assert.Equal(reference, dict["Reference"]);
            Assert.Equal(alternate, dict["Alternate"]);
            Assert.Equal(variant.StrandBiasResults.OverallStats.ChanceFalsePos.ToString(), dict["Overall_ChanceFalsePos"]);
            Assert.Equal(variant.StrandBiasResults.ForwardStats.ChanceFalsePos.ToString(), dict["Forward_ChanceFalsePos"]);
            Assert.Equal(variant.StrandBiasResults.ReverseStats.ChanceFalsePos.ToString(), dict["Reverse_ChanceFalsePos"]);
            Assert.Equal(variant.StrandBiasResults.OverallStats.ChanceFalseNeg.ToString(), dict["Overall_ChanceFalseNeg"]);
            Assert.Equal(variant.StrandBiasResults.ForwardStats.ChanceFalseNeg.ToString(), dict["Forward_ChanceFalseNeg"]);
            Assert.Equal(variant.StrandBiasResults.ReverseStats.ChanceFalseNeg.ToString(), dict["Reverse_ChanceFalseNeg"]);
            Assert.Equal(variant.StrandBiasResults.OverallStats.Frequency.ToString(), dict["Overall_Freq"]);
            Assert.Equal(variant.StrandBiasResults.ForwardStats.Frequency.ToString(), dict["Forward_Freq"]);
            Assert.Equal(variant.StrandBiasResults.ReverseStats.Frequency.ToString(), dict["Reverse_Freq"]);
            Assert.Equal(variant.StrandBiasResults.OverallStats.Support.ToString(), dict["Overall_Support"]);
            Assert.Equal(variant.StrandBiasResults.ForwardStats.Support.ToString(), dict["Forward_Support"]);
            Assert.Equal(variant.StrandBiasResults.ReverseStats.Support.ToString(), dict["Reverse_Support"]);
            Assert.Equal(variant.StrandBiasResults.OverallStats.Coverage.ToString(), dict["Overall_Coverage"]);
            Assert.Equal(variant.StrandBiasResults.ForwardStats.Coverage.ToString(), dict["Forward_Coverage"]);
            Assert.Equal(variant.StrandBiasResults.ReverseStats.Coverage.ToString(), dict["Reverse_Coverage"]);
            Assert.Equal(variant.StrandBiasResults.BiasAcceptable.ToString(), dict["BiasAcceptable?"]);
            Assert.Equal(variant.StrandBiasResults.VarPresentOnBothStrands.ToString(), dict["VarPresentOnBothStrands?"]);
            Assert.Equal(variant.StrandBiasResults.CovPresentOnBothStrands.ToString(), dict["CoverageAvailableOnBothStrands?"]);

            Assert.Throws<IOException>(() => writer.WriteHeader());
            Assert.Throws<IOException>(() => writer.Write(BaseCalledAlleles));
            writer.Dispose();
        }

    }
}
