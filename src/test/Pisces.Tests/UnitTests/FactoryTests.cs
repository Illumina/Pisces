using System.IO;
using System.Linq;
using System.Collections.Generic;
using TestUtilities;
using Pisces.Domain.Options;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.IO;
using Pisces.IO.Sequencing;
using Xunit;
using System;

namespace Pisces.Tests.UnitTests
{
    public class FactoryTests
    {
        // multiple bams, one default genome
        private const string CHR19 = "chr19";
        private const string CHR17 = "chr17";
        private const int CHR19_LENGTH = 3119100;
        private const int CHR17_LENGTH = 7573100;

        private readonly string _bamChr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr19.bam");
        private readonly string _bamChr17Chr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam");

        private readonly string _bamChr17Chr19Dup = Path.Combine(TestPaths.LocalTestDataDirectory,
            "Chr17Chr19_removedSQlines.bam");

        private readonly string _intervalsChr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr19.picard");

        private readonly string _intervalsChr17 = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17only.picard");
        private readonly string _intervalsChr17Chr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17chr19.picard");

        private readonly string _intervalsInvalid = Path.Combine(TestPaths.LocalTestDataDirectory,
            "invalidIntervals.picard");

        private readonly string _genomeChr19 = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");
        private readonly string _genomeChr17Chr19 = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");
        private readonly string _genomeChr1Chr19_fake = Path.Combine(TestPaths.SharedGenomesDirectory, "fakeChr1Chr19");
        private readonly List<CalledAllele> _defaultCandidates = new List<CalledAllele>()
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    Chromosome = "chr1",
                    ReferencePosition = 123,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    VariantQscore = 25,
                    Genotype = Genotype.HeterozygousAltRef,
                    //FractionNoCalls = float.Parse("0.001"),
                    Filters = new List<FilterType>() {}
                },
                new CalledAllele(AlleleCategory.Mnv)
                {
                    Chromosome = "chr1",
                    ReferencePosition = 234,
                    ReferenceAllele = "ATCA",
                    AlternateAllele = "TCGC",
                    VariantQscore = 25,
                    Genotype = Genotype.HeterozygousAltRef,
                    //FractionNoCalls = float.Parse("0.001"),
                    Filters = new List<FilterType>() {}
                },
                new CalledAllele()
                {
                    Chromosome = "chr1",
                    ReferencePosition = 456,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    Genotype = Genotype.HomozygousRef,
                    VariantQscore = 27,
                    //FractionNoCalls = float.Parse("0.0124"),
                    TotalCoverage = 99,
                    AlleleSupport = 155,
                    StrandBiasResults = new BiasResults() {GATKBiasScore = float.Parse("0.25")}
                },
                new CalledAllele()
                {
                    Chromosome = "chr1",
                    ReferencePosition = 567,
                    ReferenceAllele = "A",
                    AlternateAllele = ".",
                    VariantQscore = 20,
                    Genotype = Genotype.HeterozygousAltRef,
                    //FractionNoCalls = float.Parse("0.001"),
                    Filters = new List<FilterType>() {FilterType.LowDepth, FilterType.LowVariantQscore, FilterType.StrandBias}
                },
                new CalledAllele(AlleleCategory.Snv)
                {
                    Chromosome = "chr1",
                    ReferencePosition = 678,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    VariantQscore = 25,
                    Genotype = Genotype.HeterozygousAltRef,
                    //FractionNoCalls = float.Parse("0.001"),
                    Filters = new List<FilterType>() {FilterType.LowDepth}
                }
            };

        [Fact]
        [Trait("ReqID", "SDS-9")]
        public void OneGenome_AllBams()
        {
            var factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] { _bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup },
                GenomePaths = new[] { _genomeChr19 }
            });

            Assert.True(factory.WorkRequests.TrueForAll(w=>w.GenomeDirectory==_genomeChr19));
        }

        [Fact]
        [Trait("ReqID", "SDS-10")]
        public void GenomePerBam()
        {
            var bams = new[] {_bamChr19, _bamChr17Chr19, "testBam"};
            var genomes = new[] {_genomeChr19, _genomeChr17Chr19, "testGenome"};
            var factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = bams,
                GenomePaths = genomes
            });

            for (int i = 0; i < bams.Length; i++)
            {
                Assert.Equal(genomes[i],factory.WorkRequests.First(w=>w.BamFilePath == bams[i]).GenomeDirectory);
            }
        }

        [Fact]
        public void GetGenome_OneDefault()
        {
            // -----------------------------------------------
            // verify only genome chromosomes returned
            // -----------------------------------------------
            var factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] {_bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup},
                GenomePaths = new[] {_genomeChr19}
            });

            var genome = factory.GetReferenceGenome(_genomeChr19);
            Assert.Equal(_genomeChr19, genome.Directory);
            Assert.Equal(1, genome.ChromosomesToProcess.Count());
            Assert.Equal(CHR19, genome.ChromosomesToProcess.First());
            Assert.Equal(1, genome.ChromosomeLengths.Count());
            Assert.Equal(CHR19, genome.ChromosomeLengths.First().Item1);

            var reference = genome.GetChrReference(CHR19);
            Assert.Equal(CHR19, reference.Name);
            Assert.Equal(Path.Combine(_genomeChr19, CHR19 + ".fa"), reference.FastaPath);
            Assert.Equal(genome.ChromosomeLengths.First().Item2, reference.Sequence.Length);

            // -----------------------------------------------
            // verify multiple genome chromosomes returned
            // -----------------------------------------------
            factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] {_bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup},
                GenomePaths = new[] {_genomeChr17Chr19}
            });

            genome = factory.GetReferenceGenome(_genomeChr17Chr19);

            Assert.Equal(_genomeChr17Chr19, genome.Directory);
            Assert.Equal(2, genome.ChromosomesToProcess.Count());
            Assert.Equal(CHR17, genome.ChromosomesToProcess.First());
            Assert.Equal(CHR19, genome.ChromosomesToProcess.Last());

            Assert.Equal(2, genome.ChromosomeLengths.Count());
            Assert.Equal(CHR17, genome.ChromosomeLengths.First().Item1);
            Assert.Equal(CHR17_LENGTH, genome.ChromosomeLengths.First().Item2);
            Assert.Equal(CHR19, genome.ChromosomeLengths.Last().Item1);
            Assert.Equal(CHR19_LENGTH, genome.ChromosomeLengths.Last().Item2);

            reference = genome.GetChrReference(CHR17);

            Assert.Equal(CHR17, reference.Name);
            Assert.Equal(Path.Combine(_genomeChr17Chr19, CHR17 + CHR19 + ".fa"), reference.FastaPath);
            Assert.Equal(CHR17_LENGTH, reference.Sequence.Length);

            // -----------------------------------------------
            // verify filtered by bam
            // -----------------------------------------------
            factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] {_bamChr17Chr19Dup},
                GenomePaths = new[] {_genomeChr1Chr19_fake}
            });

            genome = factory.GetReferenceGenome(_genomeChr1Chr19_fake);

            Assert.Equal(1, genome.ChromosomesToProcess.Count());
            Assert.Equal(CHR19, genome.ChromosomesToProcess.First());

            // -----------------------------------------------
            // verify filtered by intervals
            // -----------------------------------------------
            factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] {_bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup},
                IntervalPaths = new[] {_intervalsChr17, _intervalsChr17, _intervalsChr17},
                GenomePaths = new[] {_genomeChr17Chr19}
            });

            genome = factory.GetReferenceGenome(_genomeChr17Chr19);
            Assert.Equal(1, genome.ChromosomesToProcess.Count());
            Assert.Equal(CHR17, genome.ChromosomesToProcess.First());

            factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] {_bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup},
                IntervalPaths = new[] {_intervalsChr19, _intervalsChr19, _intervalsChr19},
                GenomePaths = new[] {_genomeChr17Chr19}
            });

            genome = factory.GetReferenceGenome(_genomeChr17Chr19);
            Assert.Equal(1, genome.ChromosomesToProcess.Count());
            Assert.Equal(CHR19, genome.ChromosomesToProcess.First());

            factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] {_bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup},
                IntervalPaths = new[] {_intervalsChr17Chr19, _intervalsChr17Chr19, _intervalsChr17Chr19},
                GenomePaths = new[] {_genomeChr17Chr19}
            });

            genome = factory.GetReferenceGenome(_genomeChr17Chr19);
            Assert.Equal(2, genome.ChromosomesToProcess.Count());
            Assert.Equal(CHR17, genome.ChromosomesToProcess.First());
            Assert.Equal(CHR19, genome.ChromosomesToProcess.Last());
        }

        [Fact]
        public void GetGenome_Multiple()
        {
            // -----------------------------------------------
            // verify only genome chromosomes returned
            // -----------------------------------------------
            var factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] {_bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup},
                GenomePaths = new[] {_genomeChr17Chr19, _genomeChr17Chr19, _genomeChr1Chr19_fake}
            });

            var genome = factory.GetReferenceGenome(_genomeChr17Chr19);
            Assert.Equal(_genomeChr17Chr19, genome.Directory);
            Assert.Equal(2, genome.ChromosomesToProcess.Count());

            genome = factory.GetReferenceGenome(_genomeChr1Chr19_fake);
            Assert.Equal(_genomeChr1Chr19_fake, genome.Directory);
            Assert.Equal(1, genome.ChromosomesToProcess.Count());
        }

        [Fact]
        [Trait("ReqID","SDS-11")]
        [Trait("ReqID", "SDS-12")]
        public void GetIntervals()
        {
            var factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] { _bamChr19, _bamChr17Chr19 },
                IntervalPaths = new[] { _intervalsChr17, _intervalsChr19 },
                GenomePaths = new[] { _genomeChr17Chr19 }
            });

            // verify fetch intervals for bam
            // should match exactly what is in the interval file, not filtered by bam
            var intervals = factory.GetIntervals(_bamChr19);
            Assert.Equal(1, intervals.Count());
            Assert.Equal(1, intervals["chr17"].Count());

            intervals = factory.GetIntervals(_bamChr17Chr19);
            Assert.Equal(2, intervals.Count());
            Assert.Equal(2, intervals["chr19"].Count());
            Assert.Equal(5, intervals["chr7"].Count());

            // -----------------------------------------------
            // verify invalid interval file will result in no intervals 
            // -----------------------------------------------
            factory = new Factory(new PiscesApplicationOptions()
            {
                BAMPaths = new[] {_bamChr19},
                IntervalPaths = new[] {_intervalsInvalid},
                GenomePaths = new[] {_genomeChr17Chr19}
            });

            intervals = factory.GetIntervals(_bamChr19);
            Assert.Equal(0, intervals.Count());
        }

        // Verify default output files to same directory as BAM files by default using the BAM file names as a template for output.
        [Fact]
        [Trait("ReqID", "SDS-13")]
        [Trait("ReqID", "SDS-15")]
        public void DefaultVCFOutput()
        {
            var appOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] { _bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup },
                IntervalPaths = new[] { _intervalsChr17, _intervalsChr19, null },
                GenomePaths = new[] { _genomeChr17Chr19 },
                VariantCallingParameters = new VariantCallingParameters()
                {
                    MinimumCoverage = 10,
                    LowDepthFilter = 10,
                },
                VcfWritingParameters = new VcfWritingParameters()
                {
                    OutputGvcfFile = false
                }
            };

            var factory = new Factory(appOptions);

            var context = new VcfWriterInputContext
            {
                CommandLine = new [] { "myCommandLine"},
                SampleName = "mySample",
                ReferenceName = "myReference",
                ContigsByChr = new List<Tuple<string, long>>
                {
                    new Tuple<string, long>("chr1", 10001),
                    new Tuple<string, long>("chrX", 500)
                }
            };
            var outputFile = factory.GetOutputFile(appOptions.BAMPaths[0]);
            var writer = factory.CreateVcfWriter(outputFile, context);

            var candidates = _defaultCandidates;

            writer.WriteHeader();
            writer.Write(candidates);
            writer.Dispose();

            Assert.True(File.Exists(outputFile));
            Assert.Equal(outputFile, Path.ChangeExtension(_bamChr19, ".vcf"));

            var reader = new VcfReader(outputFile);
            var header = reader.HeaderLines;
            Assert.Equal(header[7], "##FILTER=<ID=q30,Description=\"Quality score less than 30\">");
            Assert.Equal(header[8], "##FILTER=<ID=SB,Description=\"Variant strand bias too high\">");
            Assert.Equal(header[9], "##FILTER=<ID=R5x9,Description=\"Repeats of part or all of the variant allele (max repeat length 5) in the reference greater than or equal to 9\">");
        }

        // Verify default output files to same directory as BAM files by default with the genome.vcf extension.
        [Fact]
        [Trait("ReqID", "SDS-24")]
        public void DefaultGVCFOutput()
        {
            var appOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] {_bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup},
                IntervalPaths = new[] {_intervalsChr17, _intervalsChr19, null},
                GenomePaths = new[] {_genomeChr17Chr19}
            };
            var gVCFOption = new[] {"-gVCF", "true"};
            appOptions.UpdateOptions(gVCFOption);

            var factory = new Factory(appOptions);

            var context = new VcfWriterInputContext
            {
                CommandLine = new[] { "myCommandLine" },
                SampleName = "mySample",
                ReferenceName = "myReference",
                ContigsByChr = new List<Tuple<string, long>>
                {
                    new Tuple<string, long>("chr1", 10001),
                    new Tuple<string, long>("chrX", 500)
                }
            };
            var outputFile = factory.GetOutputFile(appOptions.BAMPaths[0]);
            var writer = factory.CreateVcfWriter(outputFile, context);

            var candidates = _defaultCandidates;

            writer.WriteHeader();
            writer.Write(candidates);
            writer.Dispose();

            Assert.True(File.Exists(outputFile));
            Assert.Equal(outputFile, Path.ChangeExtension(_bamChr19, "genome.vcf"));
        }

        // Verify the format of the gVCF file is the same as a vcf file.
        [Fact]
        [Trait("ReqID", "SDS-25")]
        public void GvcfHeaderFormat()
        {

            var appOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] {_bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup},
                IntervalPaths = new[] {_intervalsChr17, _intervalsChr19, null},
                GenomePaths = new[] {_genomeChr17Chr19}
            };
            var gVCFOption = new[] {"-gVCF", "true"};
            appOptions.UpdateOptions(gVCFOption);

            var factory = new Factory(appOptions);

            var context = new VcfWriterInputContext
            {
                CommandLine = new[] { "myCommandLine" },
                SampleName = "mySample",
                ReferenceName = "myReference",
                ContigsByChr = new List<Tuple<string, long>>
                {
                    new Tuple<string, long>("chr1", 10001),
                    new Tuple<string, long>("chrX", 500)
                }
            };
            var outputFile = factory.GetOutputFile(appOptions.BAMPaths[0]);
            var writer = factory.CreateVcfWriter(outputFile, context);

            var candidates = _defaultCandidates;
            
            writer.WriteHeader();
            writer.Write(candidates);
            writer.Dispose();
           
            // Time to read the header
            //moved to GvcfWritingTests
            //VcfFileWriterTests.VcfFileFormatValidation(outputFile, 5);
        }

        // If the -OutFolder option uses an invalid folder, execution must end and inform the user of the failure.
        // This could come into play due to permissions issues.
        [Fact]
        [Trait("ReqID", "SDS-26")]
        public void InvalidVcfOutputFolder()
        {
            var appOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] {_bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup},
                IntervalPaths = new[] {_intervalsChr17, _intervalsChr19, null},
                GenomePaths = new[] {_genomeChr17Chr19}
            };
            Assert.False(Directory.Exists("56:\\Illumina\\OutputFolder"));
            var outputFolder = Path.Combine("56:\\Illumina\\OutputFolder");
//            var outputFile = Path.Combine(outputFolder, "VcfFileWriterTests.vcf");
            var outputFileOptions = new[] {"-OutFolder", outputFolder};
            appOptions.UpdateOptions(outputFileOptions);
            Assert.Throws<ArgumentException>(() => appOptions.Validate());
        }
    }
}
