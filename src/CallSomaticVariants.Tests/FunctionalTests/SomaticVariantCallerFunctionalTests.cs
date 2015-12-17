using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Tests.MockBehaviors;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;
using Moq;
using CallSomaticVariants.Logic.Processing;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.FunctionalTests
{
    public class SomaticVariantCallerFunctionalTests
    {
        private readonly string _bamSmallS1 = Path.Combine(UnitTestPaths.TestDataDirectory, "small_S1.bam");
        private readonly string _bam_SIM_DID_35_S1 = Path.Combine(UnitTestPaths.R1TestDirectory, "SIM_DID_35_S1.bam");
        
        private readonly string _genomeChr19 = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr19");

        private readonly string _intervalsChr17Chr19 = Path.Combine(UnitTestPaths.TestDataDirectory, "chr17chr19.picard");
        private readonly string _interval_SIM_DID_35_S1 = Path.Combine(UnitTestPaths.R1TestDirectory, "SIM_DID_35_S1.picard");
        


        // Some simple happy path tests to ensure the basic Allele search with and without references and intervals.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void SomaticVariantCaller_SimpleSnv()
        {
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");

            var expectedAlleles = new List<BaseCalledAllele>
            {
                new CalledVariant(AlleleCategory.Snv)
                {
                    Coordinate = 3118942,
                    Reference = "A",
                    Alternate = "T",
                    Chromosome = "chr19"
                }
            };

            var vcfFilePath = Path.ChangeExtension(_bam_SIM_DID_35_S1, "vcf");

            // without reference calls
            File.Delete(vcfFilePath);
            functionalTestRunner.Execute(_bam_SIM_DID_35_S1, vcfFilePath, null, expectedAlleles);

            // with reference calls
            File.Delete(vcfFilePath);
            functionalTestRunner.Execute(_bam_SIM_DID_35_S1, vcfFilePath, null, expectedAlleles, null, true, true, 102);

            // with reference calls and intervals
            File.Delete(vcfFilePath);
            functionalTestRunner.Execute(_bam_SIM_DID_35_S1, vcfFilePath, _interval_SIM_DID_35_S1, expectedAlleles, null, true, true, 11);
        }

        // Time for some BAMProcessor tests along with a Mock Genome to ensure we're testing exactly what we want.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void BasicIntervalTesting()
        {
            var bamFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35.bam");
            var vcfFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35.vcf");

            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");

            var expectedAlleles = new List<BaseCalledAllele>
            {
                new CalledVariant(AlleleCategory.Snv)
                {
                    Coordinate = 3118942,
                    Reference = "A",
                    Alternate = "T",
                    Chromosome = "chr19"
                },
                new CalledVariant(AlleleCategory.Snv)
                {
                    Coordinate = 7572985,
                    Reference = "T",
                    Alternate = "C",
                    Chromosome = "chr17"
                }
            };

            // thread by chr
            functionalTestRunner.Execute(bamFilePath, vcfFilePath, null, expectedAlleles, threadByChr: true);
        }

        // Time for some BAMProcessor tests along with a Mock Genome to ensure we're testing exactly what we want.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void BasicMnvTesting()
        {
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");

            // Mock ChrReference for the MockGenome.
            List<ChrReference> mockChrRef = new List<ChrReference>() 
            { 
                new ChrReference() 
                {
                    Name = "chr1",
                    Sequence = "TTGTCAGTGCGCTTTTCCCAACACCACCTGCTCCGACCACCACCAGTTTGTACTCAGTCATTTCACACCAGCAAGAACCTGTTGGAAACCAGTAATCAGGGTTAATTGGCGGCG"
                }
            };

            var expectedAlleles = new List<BaseCalledAllele>
            {
                new CalledVariant(AlleleCategory.Mnv)
                {
                    Coordinate = 27,
                    Reference = "CCTGCTCCG",
                    Alternate = "TTTGCTCCA",
                    Chromosome = "chr1"
                },
                new CalledVariant(AlleleCategory.Mnv)
                {
                    Coordinate = 27,
                    Reference = "CC",
                    Alternate = "TT",
                    Chromosome = "chr1"
                },
                new CalledVariant(AlleleCategory.Snv)
                {
                    Coordinate = 35,
                    Reference = "G",
                    Alternate = "A",
                    Chromosome = "chr1"
        }
            };

            // Testing small_S1.bam with a MockGenome.
            functionalTestRunner.Execute(_bamSmallS1, Path.ChangeExtension(_bamSmallS1, "vcf"), null, expectedAlleles, mockChrRef, doCheckReferences: true, doLog: true);
        }

        // Testing that intervals work properly under different circumstances.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void BasicSNVIntervalTesting()
        {
            var bamFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35.bam");
            var vcfFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35.vcf");
            var intervalFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "chr17only.picard");
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");

            var expectedAlleles = new List<BaseCalledAllele>
            {
                new CalledVariant(AlleleCategory.Snv)
                {
                    Coordinate = 3118942,
                    Reference = "A",
                    Alternate = "T",
                    Chromosome = "chr19"
                },
                new CalledVariant(AlleleCategory.Snv)
                {
                    Coordinate = 7572985,
                    Reference = "T",
                    Alternate = "C",
                    Chromosome = "chr17"
                }
            };

            // Spot an expected allele inside an interval.
            functionalTestRunner.Execute(bamFilePath, vcfFilePath, intervalFilePath, expectedAlleles.Where(a => a.Chromosome == "chr17").ToList());

            // Ignore indels spotted outside or overlapping the interval.
            functionalTestRunner.Execute(bamFilePath, vcfFilePath, intervalFilePath, expectedAlleles.Where(a => a.Chromosome == "chr17").ToList());
        }

        [Fact]
        [Trait("Category", "BamTesting")]
        public void DeletionAtEdgeOfDistribution()
        {
            // This test was brought forward to test Deletion at the edge from the previous tests. The test was listed as failing when stitching was included.
            // Notes from Old SVC: Make sure we can accurately deletions at the edge of the coverage distribution, and not accidentally mark them as SB
            // This test case was in response to a bug, where originally we called SB here when we should not.
            // chr7    116376907       .       ATTT    A       100.00  SB      DP=750;
            var bamFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "edgeIndel_S2.bam");
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");
            
            var appOptions = new ApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                IntervalPaths = null,
                GenomePaths = new[] { Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19") },
                OutputgVCFFiles = true,
                OutputBiasFiles = true,
                DebugMode = true,
                CallMNVs = true,
                UseMNVReallocation = false,
                MaxSizeMNV = 100,
                FilterOutVariantsPresentOnlyOneStrand = false,
                AppliedNoiseLevel = -1,
                MinimumBaseCallQuality = 20,
                MaximumVariantQScore = 100,
                FilteredVariantQScore = 30,
                MinimumVariantQScore = 20,
                MaxGapBetweenMNV = 10,
                MinimumCoverage = 10,
                MinimumMapQuality = 1,
                MinimumFrequency = 0.01f,
                StrandBiasAcceptanceCriteria = 0.5f,
                StrandBiasScoreMaximumToWriteToVCF = -100,
                StrandBiasScoreMinimumToWriteToVCF = 0,
                OnlyUseProperPairs = false,
                NoiseModelHalfWindow = 1,
                DoBamQC = BamQCOptions.VarCallOnly,
                NoiseModel = NoiseModel.Flat,
                GTModel = GenotypeModel.Symmetrical,
                StrandBiasModel = StrandBiasModel.Extended,
                RequireXCTagToStitch = true,
                StitchReads = false
            };

            var mockChrRef = new List<ChrReference>()
            {
                new ChrReference()
                {
                    // position 63
                    Name = "chr7",
                    Sequence = "NNNNNNNNNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNNNNNN" + "NN" +
                               "GTTGGTCTTCTATTTTATGCGAATTCTTCTAAGATTCCCAGGTTATTTATCATAAGAATTACATTTACATGGCAAATTTAGTTCTGTTCCTAGAAATATCTCCATGACAACCAAAAGGAACTCCTAATTTCTGGCACACATTACTTCAGGGGT"
                }
            };

            var expectedAlleles = new List<BaseCalledAllele>
            {
                new CalledVariant(AlleleCategory.Deletion)
                {
                    Coordinate = 107,
                    Reference = "ATTT",
                    Alternate = "A",
                    Chromosome = "chr7"
                }
            };

            functionalTestRunner.Execute(bamFilePath, Path.ChangeExtension(bamFilePath, "genome.vcf"), null, expectedAlleles, mockChrRef, applicationOptions:appOptions);
        }

        [Fact]
        [Trait("Category", "BamTesting")]
        public void InsertionAtEdgeOfDistribution()
        {
            // This test was brought forward to test Deletion at the edge from the previous tests. The test was listed as failing when stitching was included.
            // Notes from Old SVC: Make sure we can accurately insertions at the edge of the coverage distribution, and not accidentally mark them as SB
            // This test case was in response to a bug, where originally we called SB here when we should not.
            // chr7    116376907       .       ATTT    A       100.00  SB      DP=750;
            var bamFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "edgeIns_S2.bam");
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");

            var appOptions = new ApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                IntervalPaths = null,
                GenomePaths = new[] { Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19") },
                OutputgVCFFiles = true,
                OutputBiasFiles = true,
                DebugMode = true,
                CallMNVs = true,
                UseMNVReallocation = false,
                MaxSizeMNV = 100,
                FilterOutVariantsPresentOnlyOneStrand = false,
                AppliedNoiseLevel = -1,
                MinimumBaseCallQuality = 20,
                MaximumVariantQScore = 100,
                FilteredVariantQScore = 30,
                MinimumVariantQScore = 20,
                MaxGapBetweenMNV = 10,
                MinimumCoverage = 10,
                MinimumMapQuality = 1,
                MinimumFrequency = 0.01f,
                StrandBiasAcceptanceCriteria = 0.5f,
                StrandBiasScoreMaximumToWriteToVCF = -100,
                StrandBiasScoreMinimumToWriteToVCF = 0,
                OnlyUseProperPairs = false,
                NoiseModelHalfWindow = 1,
                DoBamQC = BamQCOptions.VarCallOnly,
                NoiseModel = NoiseModel.Flat,
                GTModel = GenotypeModel.Symmetrical,
                StrandBiasModel = StrandBiasModel.Extended,
                RequireXCTagToStitch = true,
                StitchReads = false
            };

            // Time to build the fake sequences for testing.
            var mockChrRef = new List<ChrReference>()
            {
                new ChrReference()
                {
                    // position 63
                    Name = "chr7",
                    Sequence = "NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN" +
                               "GTTGGTCTTCTATTTTATGCGAATTCTTCTAAGATTCCCAGGTTATTTATCATAAGAATTACATTTACATGGCAAATTTAGTTCTGTTCCTAGAAATATCTCCATGACAACCAAAAGGAACTCCTAATTTCTGGCACACATTACTTCAGGGGT"
                }
            };

            var expectedAlleles = new List<BaseCalledAllele>
            {
                new CalledVariant(AlleleCategory.Insertion)
                {
                    Coordinate = 110,
                    Reference = "T",
                    Alternate = "TGGG",
                    Chromosome = "chr7"
                }
            };

            functionalTestRunner.Execute(bamFilePath, Path.ChangeExtension(bamFilePath, "genome.vcf"), null, expectedAlleles, mockChrRef, applicationOptions:appOptions);

        }

        // Test variant calling dependent on the depth.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void CallSomaticVariants_LowDepthTest()
        {
            List<ChrReference> chrRef = new List<ChrReference>() 
            { 
                new ChrReference() 
                {
                    Name = "chr19",
                    Sequence = "TTGTCAGTGCGCTTTTCCCAACACCACCTGCTCCGACCACCACCAGTTTGTACTCAGTCATTTCACACCAGCAAGAACCTGTTGGAAACCAGTAATCAGGGTTAATTGGCGGCGAAAAAAAAAAAAAAAAAAAAAAAAAA"
                }
            };

            var options = new ApplicationOptions()
            {
                BAMPaths = new[] { _bamSmallS1 },
                GenomePaths = new[] { _genomeChr19 },
                //IntervalPaths = new[] { _intervalsChr17Chr19 },
                DebugMode = true,
                CallMNVs = true,
                UseMNVReallocation = false,
                MaxSizeMNV = 100,
                OutputgVCFFiles = true,
                MinimumCoverage = 1000,
                OutputFolder = UnitTestPaths.TestDataDirectory
            };

            var vcfFilePath = Path.ChangeExtension(options.BAMPaths[0], "genome.vcf");

            var factory = new Factory(options);
            IGenome genomeRef;

            genomeRef = new MockGenome(chrRef);
            
            var bp = new BamProcessor(factory, genomeRef);
            bp.Execute(1);
            List<VcfVariant> coverage1000results = VcfReader.GetAllVariantsInFile(vcfFilePath);

            options = new ApplicationOptions()
            {
                BAMPaths = new[] { _bamSmallS1 },
                GenomePaths = new[] { _genomeChr19 },
                // IntervalPaths = new[] { _intervalsChr17Chr19 },
                DebugMode = true,
                CallMNVs = true,
                UseMNVReallocation = false,
                OutputgVCFFiles = true,
                OutputFolder = UnitTestPaths.TestDataDirectory
            };
            factory = new Factory(options);
            bp = new BamProcessor(factory, genomeRef);
            bp.Execute(1);
            List<VcfVariant> coverage10results = VcfReader.GetAllVariantsInFile(vcfFilePath);

            // Assert.NotEqual(coverage1000results.Count, coverage10results.Count);
            // Assert.Equal(coverage1000results.Count, 84);
            // Assert.Equal(coverage10results.Count, 100);
        }

        private void CompareVariants(string expectedResultsFilePath, string actualResultsFilePath)
        {
            List<VcfVariant> results = VcfReader.GetAllVariantsInFile(actualResultsFilePath);
            List<VcfVariant> expected = VcfReader.GetAllVariantsInFile(expectedResultsFilePath);

            Assert.Equal(results.Count, expected.Count);

            for (int i = 0; i < results.Count; i++)
            {
                Assert.Equal(expected[i].ToString(), results[i].ToString());
            }
        }
    }

    public class SomaticVariantCallerFunctionalTestSetup
    {
        public string GenomeDirectory { get; set; }

        public static void CheckVariants(List<VcfVariant> calledAlleles, List<BaseCalledAllele> expectedVariants)
        {
            Assert.Equal(expectedVariants.Count(), calledAlleles.Count());

            foreach (var calledSomaticVariant in expectedVariants)
            {
                Console.WriteLine("Looking for:");
                Console.WriteLine(calledSomaticVariant.Chromosome + " " + calledSomaticVariant.Coordinate + " " +
                                  calledSomaticVariant.Reference + ">" + calledSomaticVariant.Alternate);
            }

            Console.WriteLine("Found:");
            foreach (var expectedVariant in expectedVariants)
            {
                var foundVariant = calledAlleles.First(v => v.ReferencePosition == expectedVariant.Coordinate && v.ReferenceAllele == expectedVariant.Reference);
                Console.WriteLine(foundVariant.ReferenceName + " " + foundVariant.ReferencePosition + " " +
                                  foundVariant.ReferenceAllele + ">" + foundVariant.VariantAlleles[0]);
                Assert.Equal(foundVariant.ReferenceAllele, expectedVariant.Reference);
                Assert.Equal(foundVariant.VariantAlleles[0], expectedVariant.Alternate);
                Assert.Equal(foundVariant.ReferenceName, expectedVariant.Chromosome);
            }
        }

        public void Execute(
            string bamFilePath, 
            string vcfFilePath, 
            string intervalPath, 
            List<BaseCalledAllele> expectedVariants, 
            List<ChrReference> fakeReferences = null, 
            bool doCheckVariants = true, 
            bool doCheckReferences = false, 
            int expectedNumCoveredPositions = 0, 
            bool threadByChr = false, 
            int doCountsOnly = 0,
            bool doLog = false,
            bool callMnvs = true,
            ApplicationOptions applicationOptions = null)
        {
            if (doCheckReferences)
            {
                vcfFilePath = Path.ChangeExtension(vcfFilePath, "genome.vcf");
            }

            if (applicationOptions == null)
            {
                applicationOptions = new ApplicationOptions
                {
                    BAMPaths = new[] {bamFilePath},
                    IntervalPaths = string.IsNullOrEmpty(intervalPath) ? null : new[] {intervalPath},
                    GenomePaths = new[] {GenomeDirectory},
                    OutputgVCFFiles = doCheckReferences,
                    OutputBiasFiles = true,
                    DebugMode = doLog,
                    MinimumBaseCallQuality = 20,
                    CallMNVs = callMnvs
                };
            }

            Logger.TryOpenLog(applicationOptions.LogFolder, ApplicationOptions.LogFileName);

            var factory = GetFactory(applicationOptions);

            IGenome genome;

            if (fakeReferences == null)
            {
                genome = factory.GetReferenceGenome(GenomeDirectory);
            }
            else
            {
                genome = new MockGenome(fakeReferences, GenomeDirectory);
            }

            if (threadByChr)
            {
                var processor = new BamProcessor(factory, genome);

                processor.Execute(1);
            }
            else
            {
                var processor = new GenomeProcessor(factory, genome);

                processor.Execute(1);
            }

            Logger.TryCloseLog();

            using (var reader = new VcfReader(vcfFilePath))
            {
                var alleles = reader.GetVariants().ToList();

                var variantCalls = alleles.Where(a => a.VariantAlleles[0] != ".").ToList();

                if (doCheckVariants)
                {
                    if (doCountsOnly > 0)
                    {
                        Assert.Equal(variantCalls.Count(), doCountsOnly);
                    }
                    else
                    {
                        CheckVariants(variantCalls, expectedVariants);
                    }
                }                    

                if (doCheckReferences)
                {
                    var referenceAlleles = alleles.Where(a => a.VariantAlleles[0] == ".").ToList();

                    // make sure no reference calls at variant positions
                    Assert.Equal(referenceAlleles.Count(), alleles.Count(a => !variantCalls.Select(v => v.ReferencePosition).Contains(a.ReferencePosition)));
                }
            }
        }

        private Factory GetFactory(ApplicationOptions options)
        {
            return new Factory(options);
        }
    }
}