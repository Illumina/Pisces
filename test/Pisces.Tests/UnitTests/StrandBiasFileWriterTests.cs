using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.Logic;
using Pisces.Tests.MockBehaviors;
using CallVariants.Logic.Processing;
using Pisces.IO.Sequencing;
using TestUtilities;
using Pisces.Calculators;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Options;
using Pisces.IO;
using Xunit;

namespace Pisces.Tests.UnitTests
{
    public class StrandBiasFileWriterTests
    {
        [Fact]
        [Trait("ReqID","SDS-27")]
        public void Write()
        {
            var outputFile = Path.Combine(TestPaths.LocalTestDataDirectory, "StrandBiasWriterTests.txt");
            File.Delete(outputFile);

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

            //TODO RawCoverage/RawSupport tests


            Assert.Throws<IOException>(() => writer.WriteHeader());
            Assert.Throws<IOException>(() => writer.Write(BaseCalledAlleles));
            writer.Dispose();

        }

        private void Write_InFlow(bool threadByChr)
        {
            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "SBWriter_Sample_S1.bam");

            var vcfFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "SBWriter_Sample_S1.genome.vcf");
            var biasFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "SBWriter_Sample_S1.genome.ReadStrandBias.txt");

            if (threadByChr) biasFilePath = biasFilePath + "_chr19"; //Currently when threading by chrom we are outputting one bias file per chromsome. This is not a customer-facing deliverable and is a low-priority feature.

            var expectedBiasResultsPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Expected_Sample_S1.ReadStrandBias.txt");

            var genomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");

            var applicationOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                IntervalPaths = null,
                GenomePaths = new[] { genomeDirectory },
                OutputBiasFiles = true,
                DebugMode = true,
                VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                { OutputGvcfFile = true }
            };

            // Using GenomeProcessor
            //If OutputBiasFiles is true, should output one bias file per vcf
            var factory = new MockFactoryWithDefaults(applicationOptions);
            var genome = factory.GetReferenceGenome(genomeDirectory);
            CreateAndExecuteProcessor(threadByChr, factory, genome);

            Assert.True(File.Exists(biasFilePath));                 

            //All variants that are present in VCF where ref!=alt should be included
            var biasFileContents = File.ReadAllLines(biasFilePath);
            var alleles = VcfReader.GetAllVariantsInFile(vcfFilePath);
            var variantCalls = alleles.Where(a => a.VariantAlleles[0] != ".").ToList();
            foreach (var variantCall in variantCalls)
            {
                Console.WriteLine(variantCall);
                Assert.True(biasFileContents.Count(l => l.Split('\t')[0] == variantCall.ReferenceName &&
                                                        l.Split('\t')[1] == variantCall.ReferencePosition.ToString() &&
                                                        l.Split('\t')[2] == variantCall.ReferenceAllele &&
                                                        l.Split('\t')[3] == variantCall.VariantAlleles.First()) == 1);
            }
            foreach (var refCall in alleles.Where(a => a.VariantAlleles[0] == ".").ToList())
            {
                Assert.False(biasFileContents.Count(l => l.Split('\t')[0] == refCall.ReferenceName &&
                                                         l.Split('\t')[1] == refCall.ReferencePosition.ToString() &&
                                                         l.Split('\t')[2] == refCall.ReferenceAllele &&
                                                         l.Split('\t')[3] == refCall.VariantAlleles.First()) == 1);
            }

            //Bias files should have expected contents
            var expectedBiasFileContents = File.ReadAllLines(expectedBiasResultsPath);
            Assert.Equal(expectedBiasFileContents,biasFileContents);

            //If OutputBiasFiles is false, should not output any bias files
            File.Delete(biasFilePath);

            applicationOptions.OutputBiasFiles = false;
            factory = new MockFactoryWithDefaults(applicationOptions);
            genome = factory.GetReferenceGenome(genomeDirectory);
            CreateAndExecuteProcessor(threadByChr, factory, genome);
            Assert.False(File.Exists(biasFilePath));

        }

        private static void CreateAndExecuteProcessor(bool threadByChr, MockFactoryWithDefaults factory, Genome genome)
        {
            if (threadByChr)
            {
                var processor = new GenomeProcessor(factory, genome);
                processor.Execute(1);
            }
            else
            {
                var processor = new GenomeProcessor(factory, genome);
                processor.Execute(1);
            }
        }

        [Fact]
        public void Write_GenomeProcessor()
        {
            Write_InFlow(false);
        }
    }
}
