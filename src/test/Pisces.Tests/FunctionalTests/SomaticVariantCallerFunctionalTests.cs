using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CallVariants.Logic.Processing;
using Pisces.IO.Sequencing;
using TestUtilities.MockBehaviors;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Options;
using Xunit;


namespace Pisces.Tests.FunctionalTests
{
    public class SomaticVariantCallerFunctionalTests
    {
        private readonly string _bam_Sample = Path.Combine(TestPaths.LocalTestDataDirectory, "Sample_S1.bam");
        private readonly string _interval_Sample = Path.Combine(TestPaths.LocalTestDataDirectory, "Sample_S1.picard");
        private readonly string _interval_Sample_negative = Path.Combine(TestPaths.LocalTestDataDirectory, "Sample_S1_negative.picard");

        private readonly string _bamSmallS1 = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.bam");
        private readonly string _genomeChr19 = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");
        private readonly string _intervalsChr17Chr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17chr19.picard");

        // Some simple happy path tests to ensure the basic Allele search with and without references and intervals.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void SomaticVariantCaller_SimpleSnv()
        {
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");

            var expectedAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 3118942,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    Chromosome = "chr19"
                }
            };

            var vcfFilePath = Path.ChangeExtension(_bam_Sample, "vcf");

            // without reference calls
            File.Delete(vcfFilePath);
            functionalTestRunner.Execute(_bam_Sample, vcfFilePath, null, expectedAlleles);

            // with reference calls
            File.Delete(vcfFilePath);
            functionalTestRunner.Execute(_bam_Sample, vcfFilePath, null, expectedAlleles, null, true, true, 102);

            // with reference calls and intervals
            File.Delete(vcfFilePath);
            functionalTestRunner.Execute(_bam_Sample, vcfFilePath, _interval_Sample, expectedAlleles, null, true, true, 11);

            // with reference calls and intervals that dont overlap variant
            File.Delete(vcfFilePath);
            functionalTestRunner.Execute(_bam_Sample, vcfFilePath, _interval_Sample_negative, new List<CalledAllele>(), null, true, true, 10);
        }

        // Time for some BAMProcessor tests along with a Mock Genome to ensure we're testing exactly what we want.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void BasicIntervalTest()
        {

            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam");
            var vcfFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.vcf");

            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");

            var expectedAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 3118942,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    Chromosome = "chr19"
                },
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 7572985,
                    ReferenceAllele = "T",
                    AlternateAllele = "C",
                    Chromosome = "chr17"
                }
            };

            // thread by chr
            functionalTestRunner.Execute(bamFilePath, vcfFilePath, null, expectedAlleles, threadByChr: true);
        }

        [Fact]
        [Trait("Category", "BamTesting")]
        public void IntervalTestingWithVcf()
        {
            var bamFile1Path = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam"); //has data from chr17,7572952 and chr19,3118883
            var interval1Path = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17int.picard");  //chr 17 only
            var outDir = Path.Combine(TestPaths.LocalTestDataDirectory, "IntervalTests");
            var vcfFile1Path = Path.Combine(outDir, "Chr17Chr19.vcf");  //only results from chr17
            var vcfExpectedFile1 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.expected.vcf");  //only results from chr17


            var genomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "fourChrs");

            var factory = MakeVcfFactory(new List<string> { bamFile1Path },
                new List<string> { interval1Path }, outDir);

            var genome1 = factory.GetReferenceGenome(genomeDirectory);

            var processor = new GenomeProcessor(factory, genome1);
            var chrs = genome1.ChromosomesToProcess;

            Assert.Equal("chr17", chrs[0]);

            processor.InternalExecute(10);
            Assert.Equal(1, genome1.ChromosomesToProcess.Count);
            Assert.Equal("chr17", genome1.ChromosomesToProcess[0]);

            var reader1 = new VcfReader(vcfFile1Path);

            var filters1Results = GetFilters(reader1);
            var contigs1Results = GetContigs(reader1);
            var vcf1Results = reader1.GetVariants().ToList();


            //the expected results:
            var readerExp1 = new VcfReader(vcfExpectedFile1);

            var filters1Expected = GetFilters(readerExp1);
            var contigs1Expected = GetContigs(readerExp1);
            var vcf1Expected = readerExp1.GetVariants().ToList();

            Assert.Equal(3, filters1Results.Count);
            Assert.Equal(1, contigs1Results.Count);
            Assert.Equal(1, vcf1Results.Count);

            //check variants and contigs all come out the same
            for (int i = 0; i < contigs1Expected.Count; i++)
                Assert.Equal(contigs1Expected[i], contigs1Results[i]);

            for (int i = 0; i < filters1Expected.Count; i++)
                Assert.Equal(filters1Expected[i].ToString(), filters1Results[i].ToString());

            for (int i = 0; i < vcf1Expected.Count; i++)
                Assert.Equal(vcf1Expected[i].ToString(), vcf1Results[i].ToString());


            reader1.Dispose();
            File.Delete(vcfFile1Path);

        }

        [Fact]
        [Trait("Category", "BamTesting")]
        //Test we get the same results when using muliple samples and intervals, in the same order.
        //Fist test running two samples together, then test running two samples individualy, then test it with threadByChrOn/
        //Nothing strange should happen..
        public void IntervalTestingWithMultipleSamples()  //based on a real bug when a gvcf was found was out of order, that only happened for multiple-bam runs with different interval files.
        {
            var bamFile1Path = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam"); //has data from chr17,7572952 and chr19,3118883
            var bamFile2Path = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17again.bam");
            var interval1Path = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17int.picard");  //chr 17 only
            var interval2Path = Path.Combine(TestPaths.LocalTestDataDirectory, "poorlyOrdered.picard"); //disordered, chr 19 first.
            var outDir = Path.Combine(TestPaths.LocalTestDataDirectory, "IntervalTests");
            var vcfFile1Path = Path.Combine(outDir, "Chr17Chr19.genome.vcf");  //only results from chr17
            var vcfFile2Path = Path.Combine(outDir, "Chr17again.genome.vcf");//show results from chr17 and 19
            var vcfExpectedFile1 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.expected.genome.vcf");  //only results from chr17
            var vcfExpectedFile2 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17again.expected.genome.vcf");//show results from chr17 and 19


            var genomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "fourChrs");
            var twoSampleFactory = MakeFactory(new List<string> { bamFile1Path, bamFile2Path },
                new List<string> { interval1Path, interval2Path }, outDir);

            var firstSampleFactory = MakeFactory(new List<string> { bamFile1Path },
                new List<string> { interval1Path }, outDir);

            var secondSampleFactory = MakeFactory(new List<string> { bamFile2Path },
                new List<string> { interval2Path }, outDir);


            //regular two-sample run mode.

            var genome = twoSampleFactory.GetReferenceGenome(genomeDirectory);
            var genome1 = firstSampleFactory.GetReferenceGenome(genomeDirectory);
            var genome2 = secondSampleFactory.GetReferenceGenome(genomeDirectory);

            var processor = new GenomeProcessor(twoSampleFactory, genome);

            var chrs = genome.ChromosomesToProcess;

            Assert.Equal("chr7", chrs[0]);
            Assert.Equal("chr8", chrs[1]);
            Assert.Equal("chr17", chrs[2]);
            Assert.Equal("chr19", chrs[3]);

            processor.InternalExecute(10);
            chrs = genome.ChromosomesToProcess;
            Assert.Equal("chr7", chrs[0]);
            Assert.Equal("chr8", chrs[1]);
            Assert.Equal("chr17", chrs[2]);
            Assert.Equal("chr19", chrs[3]);

            //jsut be aware, when we porcess the samples individually, we use different genome lists.
            Assert.Equal(4, genome.ChromosomesToProcess.Count);
            Assert.Equal(1, genome1.ChromosomesToProcess.Count);
            Assert.Equal(4, genome2.ChromosomesToProcess.Count);
            Assert.Equal("chr17", genome1.ChromosomesToProcess[0]);
            Assert.Equal("chr7", genome2.ChromosomesToProcess[0]);
            Assert.Equal("chr19", genome2.ChromosomesToProcess[3]);

            var reader1 = new VcfReader(vcfFile1Path);
            var reader2 = new VcfReader(vcfFile2Path);

            var contigs1Results = GetContigs(reader1);
            var contigs2Results = GetContigs(reader2);
            var vcf1Results = reader1.GetVariants().ToList();
            var vcf2Results = reader2.GetVariants().ToList();


            //the expected results:
            var readerExp1 = new VcfReader(vcfExpectedFile1);
            var readerExp2 = new VcfReader(vcfExpectedFile2);

            var contigs1Expected = GetContigs(readerExp1);
            var contigs2Expected = GetContigs(readerExp2);
            var vcf1Expected = readerExp1.GetVariants().ToList();
            var vcf2Expected = readerExp2.GetVariants().ToList();

            Assert.Equal(4, contigs1Results.Count);
            Assert.Equal(4, contigs2Results.Count);
            Assert.Equal(11, vcf1Results.Count);
            Assert.Equal(71, vcf2Results.Count);

            //check variants and contigs all come out the same
            CheckForOrdering(contigs1Results, contigs2Results, contigs1Expected, contigs2Expected, vcf1Expected, vcf2Expected);

            reader1.Dispose();
            reader2.Dispose();
            File.Delete(vcfFile1Path);
            File.Delete(vcfFile2Path);

            //now check again, processing them separately
            processor = new GenomeProcessor(firstSampleFactory, genome1);
            processor.InternalExecute(10);
            processor = new GenomeProcessor(secondSampleFactory, genome2);
            processor.InternalExecute(10);

            reader1 = new VcfReader(vcfFile1Path);
            reader2 = new VcfReader(vcfFile2Path);

            contigs1Results = GetContigs(reader1);
            contigs2Results = GetContigs(reader2);
            vcf1Results = reader1.GetVariants().ToList();
            vcf2Results = reader2.GetVariants().ToList();

            //check variants all come out the same (the contigs will be different as shown)
            CheckForOrdering(contigs1Results, contigs2Results,
                new List<string>() { "chr17" }, contigs2Expected, vcf1Expected, vcf2Expected);

            reader1.Dispose();
            reader2.Dispose();
            File.Delete(vcfFile1Path);

            //now check again, processing them "thread by chr" way
            processor = new GenomeProcessor(twoSampleFactory, genome, false);
            processor.InternalExecute(10);

            reader1 = new VcfReader(vcfFile1Path);
            reader2 = new VcfReader(vcfFile2Path);

            contigs1Results = GetContigs(reader1);
            contigs2Results = GetContigs(reader2);
            vcf1Results = reader1.GetVariants().ToList();
            vcf2Results = reader2.GetVariants().ToList();

            //check variants all come out the same (the contigs will be back to normal)
            CheckForOrdering(contigs1Results, contigs2Results,
                contigs2Expected, contigs2Expected, vcf1Expected, vcf2Expected);

            reader1.Dispose();
            reader2.Dispose();
            File.Delete(vcfFile1Path);
            File.Delete(vcfFile2Path);
        }

        private static void CheckForOrdering(List<string> contigs1Results, List<string> contigs2Results, List<string> contigs1Expected, List<string> contigs2Expected, List<VcfVariant> vcf1Expected, List<VcfVariant> vcf2Expected)
        {

            for (int i = 0; i < contigs1Expected.Count; i++)
                Assert.Equal(contigs1Expected[i], contigs1Results[i]);

            for (int i = 0; i < contigs2Expected.Count; i++)
                Assert.Equal(contigs2Expected[i], contigs2Results[i]);

            for (int i = 0; i < vcf1Expected.Count; i++)
                Assert.Equal(vcf1Expected[i].ToString(), vcf1Expected[i].ToString());

            for (int i = 0; i < vcf1Expected.Count; i++)
                Assert.Equal(vcf2Expected[i].ToString(), vcf2Expected[i].ToString());
        }

        private static List<string> GetContigs(VcfReader reader1)
        {
            return reader1.HeaderLines.Where(a => a.Contains("##contig=")).Select(a => a.Split('=')[2].Replace(",length", "")).ToList();
        }

        private static List<string> GetFilters(VcfReader reader1)
        {
            return reader1.HeaderLines.Where(a => a.Contains("##FILTER")).Select(a => a.Split('=')[2]).ToList();
        }

        private static Factory MakeVcfFactory(List<string> bamFilePaths, List<string> intervalPaths, string outDir)
        {

            var genomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "fourChrs");
            var twoSampleFactory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = bamFilePaths.ToArray(),
                IntervalPaths = intervalPaths.ToArray(),
                GenomePaths = new[] { genomeDirectory },
                OutputBiasFiles = true,
                DebugMode = false,
                BamFilterParameters = new Domain.Options.BamFilterParameters(),
                VariantCallingParameters = new Domain.Options.VariantCallingParameters(),
                CallMNVs = false,
                OutputDirectory = outDir,
                VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                {
                    OutputGvcfFile = false,
                }
            });

            return twoSampleFactory;
        }

        private static Factory MakeFactory(List<string> bamFilePaths, List<string> intervalPaths, string outDir)
        {

            var genomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "fourChrs");
            var twoSampleFactory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = bamFilePaths.ToArray(),
                IntervalPaths = intervalPaths.ToArray(),
                GenomePaths = new[] { genomeDirectory },
                OutputBiasFiles = true,
                DebugMode = false,
                CallMNVs = false,
                OutputDirectory = outDir,
                BamFilterParameters = new Domain.Options.BamFilterParameters()
                { MinimumBaseCallQuality = 20 },
                VariantCallingParameters = new Domain.Options.VariantCallingParameters(),
                VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                {
                    OutputGvcfFile = true,
                }
            });

            return twoSampleFactory;
        }

        // Time for some BAMProcessor tests along with a Mock Genome to ensure we're testing exactly what we want.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void BasicMnvTesting()
        {
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");

            // Mock ChrReference for the MockGenome.
            List<ChrReference> mockChrRef = new List<ChrReference>()
            {
                new ChrReference()
                {
                    Name = "chr1",
                    Sequence = "TTGTCAGTGCGCTTTTCCCAACACCACCTGCTCCGACCACCACCAGTTTGTACTCAGTCATTTCACACCAGCAAGAACCTGTTGGAAACCAGTAATCAGGGTTAATTGGCGGCG"
                }
            };

            var expectedAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Mnv)
                {
                    ReferencePosition = 27,
                    ReferenceAllele = "CCTGCTCCG",
                    AlternateAllele = "TTTGCTCCA",
                    Chromosome = "chr1"
                },
                new CalledAllele(AlleleCategory.Mnv)
                {
                    ReferencePosition = 27,
                    ReferenceAllele = "CC",
                    AlternateAllele = "TT",
                    Chromosome = "chr1"
                },
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 35,
                    ReferenceAllele = "G",
                    AlternateAllele = "A",
                    Chromosome = "chr1"
        }
            };

            // Testing small_S1.bam with a MockGenome.
            functionalTestRunner.Execute(_bamSmallS1, Path.ChangeExtension(_bamSmallS1, "vcf"), null, expectedAlleles, mockChrRef, doCheckReferences: true, doLog: true, collapse: false);
        }

        // Testing that intervals work properly under different circumstances.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void BasicSNVIntervalTesting()
        {
            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam");
            var vcfFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.vcf");
            var intervalFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17only.picard");
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");

            var expectedAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 3118942,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    Chromosome = "chr19"
                },
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 7572985,
                    ReferenceAllele = "T",
                    AlternateAllele = "C",
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
            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "edgeIndel_S2.bam");
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");

            var appOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                IntervalPaths = null,
                GenomePaths = new[] { Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19") },
                OutputBiasFiles = true,
                DebugMode = true,
                CallMNVs = true,
                UseMNVReallocation = false,
                MaxSizeMNV = 100,
                MaxGapBetweenMNV = 10,
                NoiseModelHalfWindow = 1,
                BamFilterParameters = new Domain.Options.BamFilterParameters()
                {
                    MinimumBaseCallQuality = 20,
                    MinimumMapQuality = 1,
                    OnlyUseProperPairs = false,
                },
                VariantCallingParameters = new Domain.Options.VariantCallingParameters()
                {
                    MaximumVariantQScore = 100,
                    MinimumVariantQScoreFilter = 30,
                    MinimumVariantQScore = 20,
                    MinimumCoverage = 10,
                    MinimumFrequency = 0.01f,
                    StrandBiasModel = StrandBiasModel.Extended,
                    NoiseModel = NoiseModel.Flat,
                    StrandBiasAcceptanceCriteria = 0.5f,
                    FilterOutVariantsPresentOnlyOneStrand = false,
                    ForcedNoiseLevel = -1,
                },
                VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                {
                    OutputGvcfFile = true,
                }
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

            var expectedAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Deletion)
                {
                    ReferencePosition = 107,
                    ReferenceAllele = "ATTT",
                    AlternateAllele = "A",
                    Chromosome = "chr7"
                }
            };

            functionalTestRunner.Execute(bamFilePath, Path.ChangeExtension(bamFilePath, "genome.vcf"), null, expectedAlleles, mockChrRef, applicationOptions: appOptions);
        }

        [Fact]
        [Trait("Category", "BamTesting")]
        public void InsertionAtEdgeOfDistribution()
        {
            // This test was brought forward to test Deletion at the edge from the previous tests. The test was listed as failing when stitching was included.
            // Notes from Old SVC: Make sure we can accurately insertions at the edge of the coverage distribution, and not accidentally mark them as SB
            // This test case was in response to a bug, where originally we called SB here when we should not.
            // chr7    116376907       .       ATTT    A       100.00  SB      DP=750;
            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "edgeIns_S2.bam");
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");

            var appOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                IntervalPaths = null,
                GenomePaths = new[] { Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19") },
                OutputBiasFiles = true,
                DebugMode = true,
                CallMNVs = true,
                UseMNVReallocation = false,
                MaxSizeMNV = 100,
                MaxGapBetweenMNV = 10,
                NoiseModelHalfWindow = 1,
                BamFilterParameters = new Domain.Options.BamFilterParameters()
                {
                    MinimumBaseCallQuality = 20,
                    MinimumMapQuality = 1,
                    OnlyUseProperPairs = false,
                },
                VariantCallingParameters = new Domain.Options.VariantCallingParameters()
                {
                    MaximumVariantQScore = 100,
                    MinimumVariantQScoreFilter = 30,
                    MinimumVariantQScore = 20,
                    MinimumCoverage = 10,
                    MinimumFrequency = 0.01f,
                    FilterOutVariantsPresentOnlyOneStrand = false,
                    ForcedNoiseLevel = -1,
                    NoiseModel = NoiseModel.Flat,
                    StrandBiasModel = StrandBiasModel.Extended,
                },
                VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                {
                    OutputGvcfFile = true,
                }
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

            var expectedAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Insertion)
                {
                    ReferencePosition = 110,
                    ReferenceAllele = "T",
                    AlternateAllele = "TGGG",
                    Chromosome = "chr7"
                }
            };

            functionalTestRunner.Execute(bamFilePath, Path.ChangeExtension(bamFilePath, "genome.vcf"), null, expectedAlleles, mockChrRef, applicationOptions: appOptions);

        }

        // Test variant calling dependent on the depth.
        [Fact]
        [Trait("Category", "BamTesting")]
        public void Pisces_LowDepthTest()
        {
            List<ChrReference> chrRef = new List<ChrReference>()
            {
                new ChrReference()
                {
                    Name = "chr19",
                    Sequence = "TTGTCAGTGCGCTTTTCCCAACACCACCTGCTCCGACCACCACCAGTTTGTACTCAGTCATTTCACACCAGCAAGAACCTGTTGGAAACCAGTAATCAGGGTTAATTGGCGGCGAAAAAAAAAAAAAAAAAAAAAAAAAA"
                }
            };

            var options = new PiscesApplicationOptions()
            {
                BAMPaths = new[] { _bamSmallS1 },
                GenomePaths = new[] { _genomeChr19 },
                //IntervalPaths = new[] { _intervalsChr17Chr19 },
                DebugMode = true,
                CallMNVs = true,
                UseMNVReallocation = false,
                MaxSizeMNV = 100,
                OutputDirectory = TestPaths.LocalTestDataDirectory,
                BamFilterParameters = new Domain.Options.BamFilterParameters()
                { MinimumBaseCallQuality = 20 },
                VariantCallingParameters = new Domain.Options.VariantCallingParameters()
                {
                    MinimumVariantQScore = 20,
                    MinimumCoverage = 1000,
                },
                VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                {
                    OutputGvcfFile = true,
                }
            };

            var vcfFilePath = Path.ChangeExtension(options.BAMPaths[0], "genome.vcf");

            var factory = new Factory(options);
            IGenome genomeRef;

            genomeRef = new MockGenome(chrRef, _genomeChr19);

            var bp = new GenomeProcessor(factory, genomeRef);
            bp.Execute(1);
            List<VcfVariant> coverage1000results = VcfReader.GetAllVariantsInFile(vcfFilePath);

            options = new PiscesApplicationOptions()
            {
                BAMPaths = new[] { _bamSmallS1 },
                GenomePaths = new[] { _genomeChr19 },
                // IntervalPaths = new[] { _intervalsChr17Chr19 },
                DebugMode = true,
                CallMNVs = true,
                UseMNVReallocation = false,
                OutputDirectory = TestPaths.LocalTestDataDirectory,
                VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                {
                    OutputGvcfFile = false,
                }
            };
            factory = new Factory(options);
            bp = new GenomeProcessor(factory, genomeRef);
            bp.Execute(1);
            List<VcfVariant> coverage10results = VcfReader.GetAllVariantsInFile(vcfFilePath);

            // Assert.NotEqual(coverage1000results.Count, coverage10results.Count);
            // Assert.Equal(coverage1000results.Count, 84);
            // Assert.Equal(coverage10results.Count, 100);
        }

        [Fact]
        [Trait("Category", "BamTesting")]
        public void StitchedCollapsedBamGroundTruth()
        {
            // SNP ground truth from TingTing
            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "collapsed.test.stitched.bam");
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "chr1");

            var appOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                IntervalPaths = null,
                GenomePaths = new[] { Path.Combine(TestPaths.SharedGenomesDirectory, "chr1") },
                OutputBiasFiles = true,
                DebugMode = true,
                CallMNVs = true,
                UseMNVReallocation = false,
                MaxSizeMNV = 100,
                MaxGapBetweenMNV = 10,
                NoiseModelHalfWindow = 1,
                BamFilterParameters = new Domain.Options.BamFilterParameters()
                {
                    MinimumBaseCallQuality = 20,
                    MinimumMapQuality = 1,
                    OnlyUseProperPairs = false,
                },
                VariantCallingParameters = new Domain.Options.VariantCallingParameters()
                {
                    MaximumVariantQScore = 100,
                    MinimumVariantQScoreFilter = 30,
                    MinimumVariantQScore = 20,
                    MinimumCoverage = 10,
                    MinimumFrequency = 0.01f,
                    FilterOutVariantsPresentOnlyOneStrand = false,
                    ForcedNoiseLevel = -1,
                    NoiseModel = NoiseModel.Flat,
                    StrandBiasModel = StrandBiasModel.Extended,
                },
                VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                {
                    OutputGvcfFile = true,
                    ReportRcCounts = true,
                    ReportTsCounts = true
                }
            };

            // Time to build the fake sequences for testing.
            var mockChrRef = new List<ChrReference>()
            {
                new ChrReference()
                {
                    // position 9770498 ~ 9770669  
                    Name = "chr1",
                    Sequence =  new string('N', 9770498-1) + "GAAGTAACAACGCAGGATGCCCCCTGGGGTGGACTGCCCCATGGAATTCTGGACCAAGGAGGAGAATCAGAGCGTTGTGGTTGACTTCCTGCTGCCCACAGGGGTCTACCTGAACTTCCCTGTGTCCCGCAATGCCAACCTCAGCACCATCAAGCAGGTATGGCCTCCATC"
                }
            };
            
            var expectedAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 9770596,
                    ReferenceAllele = "C",
                    AlternateAllele = "A",
                    Chromosome = "chr1"
                }
            };

            functionalTestRunner.Execute(bamFilePath, Path.ChangeExtension(bamFilePath, "genome.vcf"), null, expectedAlleles, mockChrRef, applicationOptions: appOptions);
            var truthvcfFilePath = Path.Combine(Path.GetDirectoryName(appOptions.BAMPaths[0]), "test_truth.stitched.genome.vcf");
            var stitchedCollapsedTruth = VcfReader.GetAllVariantsInFile(truthvcfFilePath);
            var stitchedCollapsedResults =  VcfReader.GetAllVariantsInFile(Path.ChangeExtension(bamFilePath, "genome.vcf"));

            foreach (var variantTruth in stitchedCollapsedTruth)
            {
                var variantActual= stitchedCollapsedResults.First(x => x.ReferencePosition == variantTruth.ReferencePosition);
                Assert.NotNull(variantActual);
                Assert.Equal(variantTruth.Genotypes[0]["US"], variantActual.Genotypes[0]["US"]);
            }
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
        public string OutputDirectory { get; set; }
        public static void CheckVariants(List<VcfVariant> calledAlleles, List<CalledAllele> expectedVariants)
        {
            Assert.Equal(expectedVariants.Count(), calledAlleles.Count());

            foreach (var calledSomaticVariant in expectedVariants)
            {
                Console.WriteLine("Looking for:");
                Console.WriteLine(calledSomaticVariant.Chromosome + " " + calledSomaticVariant.ReferencePosition + " " +
                                  calledSomaticVariant.ReferenceAllele + ">" + calledSomaticVariant.AlternateAllele);
            }

            Console.WriteLine("Found:");
            foreach (var expectedVariant in expectedVariants)
            {
                var foundVariants = calledAlleles.FindAll(v => v.ReferencePosition == expectedVariant.ReferencePosition && v.ReferenceAllele == expectedVariant.ReferenceAllele);

                var firstFound = foundVariants[0];
                foreach (var variant in foundVariants)
                {
                    Console.WriteLine(variant.ReferenceName + " " + variant.ReferencePosition + " " +
                                      variant.ReferenceAllele + ">" + variant.VariantAlleles[0]);
                }

                var matchVars = foundVariants.FindAll(v => v.VariantAlleles[0] == expectedVariant.AlternateAllele);
                Assert.Equal(matchVars.Count, 1); //shoud only ever be exactly 1 match.

                var matchVar = matchVars.First();
                if (matchVar != null)
                {
                    Assert.Equal(matchVar.ReferenceAllele, expectedVariant.ReferenceAllele);
                    Assert.Equal(matchVar.VariantAlleles[0], expectedVariant.AlternateAllele);
                    Assert.Equal(matchVar.ReferenceName, expectedVariant.Chromosome);
                }

            }
        }

        public void Execute(
            string bamFilePath,
            string vcfFilePath,
            string intervalPath,
            List<CalledAllele> expectedVariants,
            List<ChrReference> fakeReferences = null,
            bool doCheckVariants = true,
            bool doCheckReferences = false,
            int expectedNumCoveredPositions = 0,
            bool threadByChr = false,
            int doCountsOnly = 0,
            bool doLog = false,
            bool callMnvs = true,
            PiscesApplicationOptions applicationOptions = null,
            bool collapse = true)
        {
            if (doCheckReferences)
            {
                vcfFilePath = Path.ChangeExtension(vcfFilePath, "genome.vcf");
            }

            if (applicationOptions == null)
            {
                applicationOptions = new PiscesApplicationOptions
                {
                    BAMPaths = new[] { bamFilePath },
                    IntervalPaths = string.IsNullOrEmpty(intervalPath) ? null : new[] { intervalPath },
                    GenomePaths = new[] { GenomeDirectory },
                    OutputBiasFiles = true,
                    DebugMode = doLog,
                    CallMNVs = callMnvs,
                    MaxGapBetweenMNV = 10,
                    MaxSizeMNV = 15,
                    Collapse = collapse,
                    BamFilterParameters = new Domain.Options.BamFilterParameters()
                    { MinimumBaseCallQuality = 20 },
                    VariantCallingParameters = new Domain.Options.VariantCallingParameters(),
                    VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                    {
                        OutputGvcfFile = doCheckReferences,
                    }
                };
            }

            applicationOptions.OutputDirectory = OutputDirectory;

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
                var processor = new GenomeProcessor(factory, genome, false);

                processor.Execute(1);
            }
            else
            {
                var processor = new GenomeProcessor(factory, genome);

                processor.Execute(1);
            }

            var alleles = VcfReader.GetAllVariantsInFile(vcfFilePath);
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
                Assert.Equal(referenceAlleles.Count(),
                    alleles.Count(a => !variantCalls.Select(v => v.ReferencePosition).Contains(a.ReferencePosition)));
            }
        }

        private Factory GetFactory(PiscesApplicationOptions options)
        {
            return new Factory(options);
        }
    }
}