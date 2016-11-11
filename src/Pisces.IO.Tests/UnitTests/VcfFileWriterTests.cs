using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TestUtilities;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.IO.Sequencing;
using Xunit;

namespace Pisces.IO.Tests
{
    public class VcfFileWriterTests
    {
        static int _estimatedBaseCallQuality = 23;

        private readonly List<CalledAllele> _defaultCandidates = new List<CalledAllele>()
        {
            new CalledAllele(AlleleCategory.Snv)
            {
                Chromosome = "chr1",
                Coordinate = 123,
                Reference = "A",
                Alternate = "T",
                VariantQscore = 25,
                GenotypeQscore = 25,
                Genotype = Genotype.HeterozygousAltRef,
                FractionNoCalls = float.Parse("0.001"),
                Filters = new List<FilterType>() {},
                NoiseLevelApplied = _estimatedBaseCallQuality
            },
            new CalledAllele()
            {
                Chromosome = "chr1",
                Coordinate = 567,
                Reference = "A",
                Alternate = ".",
                VariantQscore = 20,
                GenotypeQscore = 20,
                Genotype = Genotype.HeterozygousAltRef,
                FractionNoCalls = float.Parse("0.001"),
                Filters = new List<FilterType>() {FilterType.LowDepth, FilterType.LowVariantQscore, FilterType.StrandBias},
                NoiseLevelApplied = _estimatedBaseCallQuality
            },
            new CalledAllele(AlleleCategory.Mnv)
            {
                Chromosome = "chr1",
                Coordinate = 234,
                Reference = "ATCA",
                Alternate = "TCGC",
                VariantQscore = 25,
                GenotypeQscore = 25,
                Genotype = Genotype.HeterozygousAltRef,
                FractionNoCalls = float.Parse("0.001"),
                Filters = new List<FilterType>() {},
                NoiseLevelApplied = _estimatedBaseCallQuality
            },
            new CalledAllele()
            {
                Chromosome = "chr1",
                Coordinate = 456,
                Reference = "A",
                Alternate = "T",
                Genotype = Genotype.HomozygousRef,
                VariantQscore = 27,
                GenotypeQscore = 27,
                FractionNoCalls = float.Parse("0.0124"),
                TotalCoverage = 99,
                AlleleSupport = 155,
                StrandBiasResults = new StrandBiasResults() {GATKBiasScore = float.Parse("0.25")},
                NoiseLevelApplied = _estimatedBaseCallQuality
            },
            new CalledAllele(AlleleCategory.Snv)
            {
                Chromosome = "chr1",
                Coordinate = 678,
                Reference = "A",
                Alternate = "T",
                VariantQscore = 25,
                GenotypeQscore = 25,
                Genotype = Genotype.HeterozygousAltRef,
                FractionNoCalls = float.Parse("0.001"),
                Filters = new List<FilterType>() {FilterType.LowDepth},
                NoiseLevelApplied = _estimatedBaseCallQuality
            }
        };

        public VcfFileWriterTests()
        {
            _defaultCandidates = _defaultCandidates.OrderBy(c => c.Coordinate).ThenBy(c => c.Reference).ThenBy(c => c.Alternate).ToList();
        }

        [Fact]
        public void TestSomaticStyleWithVariants()
        {
            var outputFile = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_AdHoc.vcf");
            File.Delete(outputFile);

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

            var writer = new VcfFileWriter(outputFile,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    MinFrequencyThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23,
                    AllowMultipleVcfLinesPerLoci = true,          
                },
                context);
            
            var candidates = new List<CalledAllele>()
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    AlleleSupport = 5387,
                    TotalCoverage = 5394,
                    Chromosome = "chr4",
                    Coordinate = 55141055,
                    Reference = "A",
                    Alternate = "G",
                    Filters = new List<FilterType>() {},
                    FractionNoCalls = 0,
                    Genotype = Genotype.HomozygousAlt,
                    NumNoCalls = 0,
                    ReferenceSupport = 7,
                    NoiseLevelApplied =_estimatedBaseCallQuality
                }
            };

            writer.WriteHeader();
            writer.Write(candidates);
            writer.Dispose();

            Assert.Throws<Exception>(() => writer.WriteHeader());
            Assert.Throws<Exception>(() => writer.Write(candidates));
            writer.Dispose();

            var variantLine = @"chr4	55141055	.	A	G	0	PASS	DP=5394	GT:GQ:AD:DP:VF:NL:SB:NC	1/1:0:7,5387:5394:0.9987:23:0.0000:0.0000";
            var fileLines = File.ReadAllLines(outputFile);
            Assert.True(fileLines.Contains(variantLine));
        }

        [Fact]
        public void TestDiploidStyleWithVariantsAndPadding()
        {
            var outputFile = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_Crushed_Padded.vcf");
            File.Delete(outputFile);

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

            var writer = new VcfFileWriter(outputFile,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    MinFrequencyThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23,
                    AllowMultipleVcfLinesPerLoci = false,
                },
                context);

            var candidates = new List<CalledAllele>()
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    AlleleSupport = 2387,
                    TotalCoverage = 5394,
                    Chromosome = "chr4",
                    Coordinate = 7,
                    Reference = "C",
                    Alternate = "A",
                    Filters = new List<FilterType>() {},
                    FractionNoCalls = 0,
                    Genotype = Genotype.HomozygousAlt,
                    NumNoCalls = 0,
                    ReferenceSupport = 7,
                    NoiseLevelApplied = _estimatedBaseCallQuality
                },

                new CalledAllele(AlleleCategory.Snv)
                {
                    AlleleSupport = 2387,
                    TotalCoverage = 5394,
                    Chromosome = "chr4",
                    Coordinate = 10,
                    Reference = "A",
                    Alternate = "G",
                    Filters = new List<FilterType>() {},
                    FractionNoCalls = 0,
                    Genotype = Genotype.HeterozygousAlt1Alt2,
                    NumNoCalls = 0,
                    ReferenceSupport = 7,
                    NoiseLevelApplied = _estimatedBaseCallQuality
                },

                new CalledAllele(AlleleCategory.Deletion)
                {
                    AlleleSupport = 2000,
                    TotalCoverage = 5394,
                    Chromosome = "chr4",
                    Coordinate = 10,
                    Reference = "AA",
                    Alternate = "G",
                    Filters = new List<FilterType>() {},
                    FractionNoCalls = 0,
                    Genotype = Genotype.HeterozygousAlt1Alt2,
                    NumNoCalls = 0,
                    ReferenceSupport = 7,
                    NoiseLevelApplied = _estimatedBaseCallQuality
                }
            };

            var chr4Intervals = new List<Region>()
            {
                new Region(2, 3),
                new Region(6, 8),
                new Region(10, 11)
            };


            var referenceSeq = string.Join(string.Empty, Enumerable.Repeat("C", 15));
          
            writer.WriteHeader();

            // write chr4 in increments
            var chr4Name = "chr4";
            var chr4Mapper = new RegionMapper(new ChrReference() { Name = chr4Name, Sequence = referenceSeq },
                new ChrIntervalSet(chr4Intervals, chr4Name), 23);
            writer.Write(candidates.Where(v => v.Chromosome == chr4Name && v.Coordinate == 7), chr4Mapper);
            writer.Write(candidates.Where(v => v.Chromosome == chr4Name && v.Coordinate == 10), chr4Mapper);
            writer.WriteRemaining(chr4Mapper);

            writer.Dispose();

            Assert.Throws<Exception>(() => writer.WriteHeader());
            Assert.Throws<Exception>(() => writer.Write(candidates));
            writer.Dispose();

            Compare(outputFile, outputFile.Replace(".vcf", "_expected.vcf"));

        }



        [Fact]
        public void TestDiploidStyleWithVariants()
        {
            var outputFile = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_Crushed.vcf");
            File.Delete(outputFile);

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

            var writer = new VcfFileWriter(outputFile,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    MinFrequencyThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23,
                    AllowMultipleVcfLinesPerLoci = false,
                },
                context);

            var candidates = new List<CalledAllele>()
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    AlleleSupport = 2387,
                    TotalCoverage = 5394,
                    Chromosome = "chr4",
                    Coordinate = 55141055,
                    Reference = "A",
                    Alternate = "G",
                    Filters = new List<FilterType>() {},
                    FractionNoCalls = 0,
                    Genotype = Genotype.HeterozygousAlt1Alt2,
                    NumNoCalls = 0,
                    ReferenceSupport = 7,
                    NoiseLevelApplied = 23
                },

                new CalledAllele(AlleleCategory.Deletion)
                {
                    AlleleSupport = 2000,
                    TotalCoverage = 5394,
                    Chromosome = "chr4",
                    Coordinate = 55141055,
                    Reference = "AA",
                    Alternate = "G",
                    Filters = new List<FilterType>() {},
                    FractionNoCalls = 0,
                    Genotype = Genotype.HeterozygousAlt1Alt2,
                    NumNoCalls = 0,
                    ReferenceSupport = 7,
                    NoiseLevelApplied = 23
                }
            };

            writer.WriteHeader();
            writer.Write(candidates);
            writer.Dispose();

            Assert.Throws<Exception>(() => writer.WriteHeader());
            Assert.Throws<Exception>(() => writer.Write(candidates));
            writer.Dispose();

            var variantLine = @"chr4	55141055	.	AA	GA,G	0	PASS	DP=5394	GT:GQ:AD:DP:VF:NL:SB:NC	1/2:0:2387,2000:5394:0.8133:23:0.0000:0.0000";
            var fileLines = File.ReadAllLines(outputFile);
            Assert.True(fileLines.Contains(variantLine));
        }


        [Fact]
        [Trait("ReqID", "SDS-14")]
        public void ConfiguredVCFOutput()
        {
            // Paths for currently existing and new folder paths
            var existingOutputFolder = Path.Combine(UnitTestPaths.TestDataDirectory);
            var existingOutputFile = Path.Combine(existingOutputFolder, "VcfFileWriterTests.vcf");
            var newOutputFolder = Path.Combine(UnitTestPaths.TestDataDirectory, "SDS-14");
            var newOutputFile = Path.Combine(newOutputFolder, "VcfFileWriterTests.vcf");

            // Test -OutFolder works for pre-existing folders.
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

            var writer = new VcfFileWriter(existingOutputFile,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23
                },
                context);

            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            Assert.True(File.Exists(existingOutputFile));

            // Test -OutFolder for entirely new directories.
            context = new VcfWriterInputContext
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

            // Delete the folder if it exists and ensure it's deleted
            if (Directory.Exists(newOutputFolder))
            {
                Directory.Delete(newOutputFolder, true);
            }
        }

        // The header section at the top of the VCF file will confirm to spec for SDS-16.
        [Fact]
        [Trait("ReqID", "SDS-16")]
        public void VcfHeaderTest()
        {
            var outputFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_SDS-16.vcf");
            File.Delete(outputFilePath);

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

            var writer = new VcfFileWriter(
                outputFilePath,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23,
                    RMxNFilterMaxLengthRepeat = 5,
                    RMxNFilterMinRepetitions = 9
                },
                context);

            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            VcfFileFormatValidation(outputFilePath);
        }

        // Test that the INFO and FORMAT sections are correct.
        [Fact]
        [Trait("ReqID", "SDS-17")]
        public void InfoFormatHeader()
        {
            var outputFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_SDS-17.vcf");
            File.Delete(outputFilePath);

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
            var config = new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23
                };

            var writer = new VcfFileWriter(outputFilePath, config, context);

            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            // Time to read the header
            var testFile = File.ReadAllLines(outputFilePath);
            bool formatNL = false, formatSB = false, formatNC = false;
            foreach (var x in testFile)
            {
                if (Regex.IsMatch(x, "##INFO="))
                {
                    switch (x.Split(',')[0])
                    {
                        case "##INFO=<ID=DP":
                            Assert.True(Regex.IsMatch(x, "^##INFO=<ID=DP,Number=1,Type=Integer,Description=\"Total Depth\">$"));
                            break;
                        default:
                            Assert.True(false, "An info is listed which does not match any from the req.`");
                            break;
                    }
                }
                else if (Regex.IsMatch(x, "##FORMAT="))
                {
                    switch (x.Split(',')[0])
                    {
                        case "##FORMAT=<ID=GT":
                            Assert.True(Regex.IsMatch(x, "^##FORMAT=<ID=GT,Number=1,Type=String,Description=\"Genotype\">$"));
                            break;
                        case "##FORMAT=<ID=GQ":
                            Assert.True(Regex.IsMatch(x, "^##FORMAT=<ID=GQ,Number=1,Type=Integer,Description=\"Genotype Quality\">$"));
                            break;
                        case "##FORMAT=<ID=AD":
                            Assert.True(Regex.IsMatch(x, "^##FORMAT=<ID=AD,Number=\\.,Type=Integer,Description=\"Allele Depth\">$"));
                            break;
                        case "##FORMAT=<ID=DP":
                            Assert.True(Regex.IsMatch(x, "^##FORMAT=<ID=DP,Number=1,Type=Integer,Description=\"Total Depth Used For Variant Calling\">$"));
                            break;
                        case "##FORMAT=<ID=VF":
                            Assert.True(Regex.IsMatch(x, "^##FORMAT=<ID=VF,Number=.,Type=Float,Description=\"Variant Frequency\">$"));
                            break;
                        case "##FORMAT=<ID=NL":
                            Assert.True(Regex.IsMatch(x, "^##FORMAT=<ID=NL,Number=1,Type=Integer,Description=\"Applied BaseCall Noise Level\">$"));
                            formatNL = true;
                            break;
                        case "##FORMAT=<ID=SB":
                            Assert.True(Regex.IsMatch(x, "^##FORMAT=<ID=SB,Number=1,Type=Float,Description=\"StrandBias Score\">$"));
                            formatSB = true;
                            break;
                        case "##FORMAT=<ID=NC":
                            Assert.True(Regex.IsMatch(x, "^##FORMAT=<ID=NC,Number=1,Type=Float,Description=\"Fraction of bases which were uncalled or with basecall quality below the minimum threshold\">$"));
                            formatNC = true;
                            break;
                        default:
                            Assert.True(false, "A format is listed which does not match any of those listed for the req.");
                            break;
                    }
                }
            }

            if (config.ShouldOutputStrandBiasAndNoiseLevel)
            {
                Assert.True(formatNL);
            }

            if (config.ShouldOutputStrandBiasAndNoiseLevel)
            {
                Assert.True(formatSB);
            }

            if (config.ShouldOutputNoCallFraction)
            {
                Assert.True(formatNC);
            }
        }

        // Test the Filter Header section is correct.
        [Fact]
        [Trait("ReqID", "SDS-18")]
        public void FilterHeader()
        {
            var outputFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_SDS-18.vcf");
            File.Delete(outputFilePath);

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

            // Variant strand bias too high or coverage on only one strand 
            var config = new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23,
                    IndelRepeatFilterThreshold = 10,
                    PloidyModel = PloidyModel.Diploid,
                    GenotypeQualityFilterThreshold = 25,
                    RMxNFilterMaxLengthRepeat = 5,
                    RMxNFilterMinRepetitions = 9
                };

            var writer = new VcfFileWriter(outputFilePath, config, context);

            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            VcfHeaderFormatTester(config, outputFilePath);

            // filters not enabled
            config = new VcfWriterConfig
            {
                VariantQualityFilterThreshold = 20,
                FrequencyFilterThreshold = 0.007f,
                EstimatedBaseCallQuality = 23,
                PloidyModel = PloidyModel.Somatic,
            };
            writer = new VcfFileWriter(outputFilePath, config, context);

            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            VcfHeaderFormatTester(config, outputFilePath);
        }

        // Test the format of the data section in the vcf file.
        [Fact]
        [Trait("ReqID", "SDS-19")]
        public void VCFDataSection()
        {
            var outputFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_SDS-19.vcf");
            File.Delete(outputFilePath);

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

            var writer = new VcfFileWriter(
                outputFilePath,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23
                },
                context);
            
            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            var testFile = File.ReadAllLines(outputFilePath);

            var oldPosition = 0;
            foreach (var x in testFile.Where(x => Regex.IsMatch(x.Split('\t')[0], "^chr\\d+")))
            {
                Assert.True(Regex.IsMatch(x, "^chr\\d+\t\\d+\t.+\t.+\t\\d+\t\\S+\tDP=\\d+\t.+\t.+"));
                
                // at a minimum, should be ordered by coordinate.
                var position = int.Parse(x.Split('\t')[1]);
                Assert.True(position>=oldPosition);
                oldPosition = position;
            }
        }

        // Test the format of the header for the data section in the vcf file.
        [Fact]
        [Trait("ReqID", "SDS-20")]
        public void VCFDataHeaderSection()
        {
            var outputFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_SDS-20.vcf");
            File.Delete(outputFilePath);

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

            var writer = new VcfFileWriter(
                outputFilePath,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23
                },
                context);
            
            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            var testFile = File.ReadAllLines(outputFilePath);
            foreach (var x in testFile.Where(x => Regex.IsMatch(x.Split('\t')[0], "^#CHROM")))
            {
                Assert.True(Regex.IsMatch(x, "^#CHROM\\sPOS\\sID\\sREF\\sALT\\sQUAL\\sFILTER\\sINFO\\sFORMAT\\smySample"));
            }
        }

        // Validate proper output from each allele called.
        [Fact]
        [Trait("ReqID", "SDS-21")]
        public void DataAlleleCheck()
        {
            var outputFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_SDS-21.vcf");
            File.Delete(outputFilePath);

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

            var writer = new VcfFileWriter(
                outputFilePath,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23
                },
                context);

            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            var testFile = File.ReadAllLines(outputFilePath);
            var chromCount = 0;
            var formatList = string.Empty;
            foreach (var x in testFile)
            {
                if (Regex.IsMatch(x, "^##FORMAT"))
                {
                    if (formatList == string.Empty)
                    {
                        formatList = x.Split(',')[0].Substring(13);
                    }
                    else
                    {
                        formatList += ":" + x.Split(',')[0].Substring(13);
                    }
                }
                else if (Regex.IsMatch(x, "^chr\\d+\t"))
                {
                    var y = x.Split('\t');
                    Assert.True(Regex.IsMatch(y[0], "chr\\d+"));
                    Assert.True(Regex.IsMatch(y[1], "\\d+"));
                    Assert.True(Regex.IsMatch(y[2], "\\."));
                    Assert.True(Regex.IsMatch(y[3], "([ACGT\\.])+"));
                    Assert.True(Regex.IsMatch(y[4], "([ACGT\\.])+"));
                    Assert.True(Regex.IsMatch(y[5], "\\d+"));
                    Assert.True(Regex.IsMatch(y[6], ".+"));
                    Assert.True(Regex.IsMatch(y[7], "DP=\\d+"));
                    Assert.True(Regex.IsMatch(y[8], formatList));
                    Assert.True(Regex.IsMatch(y[9], ".+"));
                    chromCount++;
                }
            }

            Assert.Equal(chromCount, 5);
        }

        [Fact]
        [Trait("ReqID", "SDS-23")]
        public void DataFormatCheck()
        {
            var outputFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_SDS-23.vcf");
            File.Delete(outputFilePath);

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

            var config = new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23
                };

            var writer = new VcfFileWriter(outputFilePath, config, context);

            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            var testFile = File.ReadAllLines(outputFilePath);
            var formatList = string.Empty;
            bool caseNL = false, caseSB = false, caseNC = false;
            foreach (var x in testFile)
            {
                if (Regex.IsMatch(x, "^##FORMAT"))
                {
                    var formatField = x.Split(',')[0].Substring(13);
                    switch (formatField)
                    {
                        case "NL":
                            if (config.ShouldOutputStrandBiasAndNoiseLevel)
                                caseNL = true;
                            break;
                        case "SB":
                            if (config.ShouldOutputStrandBiasAndNoiseLevel)
                                caseSB = true;
                            break;
                        case "NC":
                            if (config.ShouldOutputNoCallFraction)
                                caseNC = true;
                            break;
                    }

                    if (formatList == string.Empty)
                    {
                        formatList = x.Split(',')[0].Substring(13);
                    }
                    else
                    {
                        formatList += ":" + x.Split(',')[0].Substring(13);
                    }
                }

                if (Regex.IsMatch(x, "^chr\\d+\t"))
                {
                    var y = x.Split('\t');
                    Assert.True(Regex.IsMatch(y[8], formatList));
                }
            }

            if ((!config.ShouldOutputStrandBiasAndNoiseLevel && caseNL) ||
                (config.ShouldOutputStrandBiasAndNoiseLevel && !caseNL))
                Assert.True(false, "Incorrect setting for ShouldOutputStrandBiasAndNoiseLevel and NL format");

            if ((!config.ShouldOutputStrandBiasAndNoiseLevel && caseSB) ||
                (config.ShouldOutputStrandBiasAndNoiseLevel && !caseSB))
                Assert.True(false, "Incorrect setting for ShouldOutputStrandBiasAndNoiseLevel and SB format");

            if ((!config.ShouldOutputNoCallFraction && caseNC) || (config.ShouldOutputNoCallFraction && !caseNC))
                Assert.True(false, "Incorrect setting for NoCall and NC format");
        }

        // Tests that the VcfFileWriter fails when the writer fails to be able to write to the file.
        [Fact]
        [Trait("ReqID", "SDS-26")]
        public void WriterError()
        {
            var outputFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_SDS-26_2.vcf");
            File.Delete(outputFilePath);
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

            var writer = new VcfFileWriter(
                outputFilePath,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23
                },
                context);

            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            Assert.Throws<Exception>(() => writer.WriteHeader());
            Assert.Throws<Exception>(() => writer.Write(_defaultCandidates));
        }

        // A standard input returns an expected output in the same folder.
        [Fact]
        public void Test1()
        {
            var outputFile = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_Test1.vcf");
            File.Delete(outputFile);

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

            var writer = new VcfFileWriter(
                outputFile,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = 500,
                    VariantQualityFilterThreshold = 20,
                    GenotypeQualityFilterThreshold = 26,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    MinFrequencyThreshold = 0.05f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23
                },
                context);

            writer.WriteHeader();
            writer.Write(_defaultCandidates);
            writer.Dispose();

            Compare(outputFile, outputFile.Replace(".vcf", "_expected.vcf"));
        }

        // Tests that a standard input returns an expected output in the same folder.
        [Fact]
        public void Test2()
        {
            var outputFile = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_Test2.vcf");
            File.Delete(outputFile);

            var context = new VcfWriterInputContext
            {
                CommandLine = new [] { "myCommandLine"},
                SampleName = "mySample",
                ReferenceName = "myReference",
                ContigsByChr = new List<Tuple<string, long>>
                {
                    new Tuple<string, long>("chr10", 123),
                    new Tuple<string, long>("chr9", 5)
                }
            };

            var writer = new VcfFileWriter(
                outputFile,
                new VcfWriterConfig(),
                context);
            
            writer.WriteHeader();
            writer.Write(_defaultCandidates.Where(c => !c.Filters.Any()));
            writer.Dispose();

            Compare(outputFile, outputFile.Replace(".vcf", "_expected.vcf"));
        }

        // Tests that a standard input returns an expected output in the same folder.
        // no strand bias threshold but filtering on single strand
        [Fact]
        public void Test3()
        {
            var outputFile = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_Test3.vcf");
            File.Delete(outputFile);

            var context = new VcfWriterInputContext
            {
                CommandLine = new [] { "myCommandLine"},
                SampleName = "mySample",
                ReferenceName = "myReference"
            };

            var writer = new VcfFileWriter(
                outputFile,
                new VcfWriterConfig
                {
                    ShouldFilterOnlyOneStrandCoverage = true,
                    FrequencyFilterThreshold = 0.01f,
                    MinFrequencyThreshold = 0.01f,
                },
                context);

            writer.WriteHeader();
            writer.Dispose();

            Compare(outputFile, outputFile.Replace(".vcf", "_expected.vcf"));
        }

        // Tests that a standard input returns an expected output in the same folder.
        // VcfWriterConfig configured to have a strand bias threshold but not filtering on single strand.
        [Fact]
        public void Test4()
        {
            // strand bias threshold but not filtering on single strand
            var outputFile = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_Test4.vcf");
            File.Delete(outputFile);

            var context = new VcfWriterInputContext
            {
                CommandLine = new [] { "myCommandLine"},
                SampleName = "mySample",
                ReferenceName = "myReference"
            };

            var writer = new VcfFileWriter(
                outputFile,
                new VcfWriterConfig
                {
                    StrandBiasFilterThreshold = 5,
                    FrequencyFilterThreshold = 0.01f,
                    MinFrequencyThreshold = 0.01f,
                },
                context);

            writer.WriteHeader();
            writer.Dispose();

            Compare(outputFile, outputFile.Replace(".vcf", "_expected.vcf"));
        }

        private void Compare(string expectedFilePath, string resultFilePath)
        {
            var expectedLines = File.ReadAllLines(expectedFilePath);
            var resultLines = File.ReadAllLines(resultFilePath);

            Assert.Equal(resultLines.Length, expectedLines.Length);

            for (var i = 0; i < resultLines.Length; i++)
            {
                var resultLine = resultLines[i];
                var expectedLine = expectedLines[i];

                if (resultLine.StartsWith("##fileDate") || resultLine.StartsWith("##source"))
                    continue;

                if (resultLine.Contains("_cmdline"))
                    Assert.Equal(resultLine.Substring(resultLine.IndexOf("_cmdline")), expectedLine.Substring(expectedLine.IndexOf("_cmdline")));
                else
                    Assert.Equal(resultLine, expectedLine);
            }
        }

        private void VcfHeaderFormatTester(VcfWriterConfig config, string outputFile)
        {
            // Time to read the header
            var testFile = File.ReadAllLines(outputFile);
            bool formatLowDP = false, formatQ = false, formatSB = false, formatLowFreq = false, formatLowGQ = false, formatMulti = false, formatRepeat = false, formatRMxN = false;
            foreach (var x in testFile.Where(x => Regex.IsMatch(x, "##FILTER=")))
            {
                switch (x.Split(',')[0])
                {
                    case "##FILTER=<ID=LowDP":
                        Assert.True(Regex.IsMatch(x, "^##FILTER=<ID=LowDP,Description=\"Low coverage \\(DP tag\\), therefore no genotype called\">$"));
                        formatLowDP = true;
                        break;
                    case "##FILTER=<ID=SB":
                        if (config.StrandBiasFilterThreshold.HasValue && config.ShouldFilterOnlyOneStrandCoverage)
                            Assert.True(Regex.IsMatch(x, "^##FILTER=<ID=SB,Description=\"(Variant strand bias too high or coverage on only one strand)\">$"));
                        else if (config.StrandBiasFilterThreshold.HasValue)
                            Assert.True(Regex.IsMatch(x, "^##FILTER=<ID=SB,Description=\"(Variant strand bias too high)\">$"));
                        else if (config.ShouldFilterOnlyOneStrandCoverage)
                            Assert.True(Regex.IsMatch(x, "^##FILTER=<ID=SB,Description=\"(Variant support on only one strand)\">$"));
                        else
                            Assert.True(false, "StrandBias filter header does not match any expected filter.");

                        formatSB = true;                 
                        break;
                    case "##FILTER=<ID=LowVariantFreq":
                        formatLowFreq = true;
                        break;
                    case "##FILTER=<ID=LowGQ":
                        formatLowGQ = true;
                        break;
                    case "##FILTER=<ID=MultiAllelicSite":
                        formatMulti = true;
                        break;
                    default:
                        if (Regex.IsMatch(x, string.Format("##FILTER=<ID=q{0}", config.VariantQualityFilterThreshold)))
                        {
                            Assert.True(Regex.IsMatch(x, string.Format("^##FILTER=<ID=q{0},Description=\"Quality score less than {0}\">$", config.VariantQualityFilterThreshold)));
                            formatQ = true;
                        }
                        else if (Regex.IsMatch(x, string.Format("##FILTER=<ID=R{0}x{1}", config.RMxNFilterMaxLengthRepeat ?? 0, config.RMxNFilterMinRepetitions ?? 0)))
                        {
                            formatRMxN = true;
                        }
                        else if (Regex.IsMatch(x, string.Format("##FILTER=<ID=R{0}", config.IndelRepeatFilterThreshold ?? 0)))
                        {
                            formatRepeat = true;
                        }
                        else
                        {
                            Assert.True(false, "A filter is listed which does not match any of the specified filters.");
                        }

                        break;
                }
            }

            Assert.Equal(formatQ, config.VariantQualityFilterThreshold > 0);

            Assert.Equal(formatLowDP, config.DepthFilterThreshold > 0);

            Assert.Equal(formatSB, config.ShouldFilterOnlyOneStrandCoverage || config.StrandBiasFilterThreshold.HasValue);

            Assert.Equal(formatLowFreq, config.FrequencyFilterThreshold > 0.0f);

            Assert.Equal(formatLowGQ, config.GenotypeQualityFilterThreshold.HasValue);

            Assert.Equal(formatMulti, config.PloidyModel == PloidyModel.Diploid);

            Assert.Equal(formatRepeat, config.IndelRepeatFilterThreshold.HasValue);

            Assert.Equal(formatRMxN, config.RMxNFilterMaxLengthRepeat.HasValue && config.RMxNFilterMinRepetitions.HasValue);
        }

        // Utility function to test the format of Vcf files.
        public static void VcfFileFormatValidation(string inputFile, int? expectedCandidateCount = null)
        {
            var observedCandidateCount = 0;
            // Time to read the header
            var testFile = File.ReadAllLines(inputFile);

            Assert.True(testFile.Length >= 9);
            bool ff = false,
                fd = false,
                so = false,
                csvc = false,
                rf = false,
                info = false,
                filter = false,
                format = false,
                contig = false;

            foreach (var x in testFile)
            {
                switch (x.Split('=')[0])
                {
                    case "##fileformat":
                        Assert.True(Regex.IsMatch(x, "^##fileformat=VCFv4\\.1"));
                        ff = true;
                        break;
                    case "##fileDate":
                        Assert.True(Regex.IsMatch(x, "^##fileDate="));
                        CultureInfo enUS = new CultureInfo("en-US");
                        DateTime dateValue;
                        Assert.NotNull(DateTime.TryParseExact(x.Split('=')[1],
                            "YYYYMMDD", enUS, DateTimeStyles.None, out dateValue));
                        fd = true;
                        break;
                    case "##source":
                        Assert.True(Regex.IsMatch(x, "^##source=\\S+\\W\\d.\\d.\\d.\\d"));
                        so = true;
                        break;
                    case "##Pisces.IO.Tests_cmdline":  // should be calling assembly name, for unit test that is .Tests
                        Assert.True(Regex.IsMatch(x, "^##Pisces.IO.Tests_cmdline=.+"));
                        csvc = true;
                        break;
                    case "##Pisces.Tests_cmdline":  // should be calling assembly name, for unit test that is .Tests
                        Assert.True(Regex.IsMatch(x, "^##Pisces.Tests_cmdline=.+"));
                        csvc = true;
                        break;
                    case "##reference":
                        Assert.True(Regex.IsMatch(x, "^##reference="));
                        rf = true;
                        break;
                    case "##INFO":
                        Assert.True(Regex.IsMatch(x, "^##INFO=.+>$"));
                        info = true;
                        break;
                    case "##FILTER":
                        Assert.True(Regex.IsMatch(x, "^##FILTER=.+>$"));
                        filter = true;
                        break;
                    case "##FORMAT":
                        Assert.True(Regex.IsMatch(x, "^##FORMAT=<.+>"));
                        format = true;
                        break;
                    case "##contig":
                        Assert.True(Regex.IsMatch(x, "^##contig=<ID=\\S+,length=\\d+>"));
                        contig = true;
                        break;
                    default:
                        if (Regex.IsMatch(x.Split('\t')[0], "^chr\\d+"))
                        {
                            observedCandidateCount++;
                            break;
                        }

                        if (Regex.IsMatch(x.Split('\t')[0], "^#CHROM"))
                            break;

                        Assert.True(false, "Unrecognized section.");
                        break;
                }
            }

            // Ensure the correct number of candidates are listed.
            if (expectedCandidateCount.HasValue)
                Assert.Equal(expectedCandidateCount, observedCandidateCount);

            Assert.True(ff && fd && so && csvc && rf && info && filter && format && contig,
                "Missing a section of the header");
        }

        [Fact]
        public void Padding()
        {
            var outputFile = Path.Combine(UnitTestPaths.TestDataDirectory, "WriterUnitTestOutput", "output.vcf");

            var writer = new VcfFileWriter(outputFile, new VcfWriterConfig(), new VcfWriterInputContext());

            // Test setup
            // -------------------
            // intervals
            // chr1: 2-3, 6-8, 10-11   
            // chr2: 4-4                
            // chr3: 7-8
            // chr4: 4-5
            //
            // variants 
            // chr1:7       
            // chr2:8       note: expect allele @ 8 (it's not writer or mapper's job to prune, just pad)
            // chr3 none!  
            // chr4:2

            var referenceSeq = string.Join(string.Empty, Enumerable.Repeat("C", 15));

            var variants = new List<CalledAllele>
            {
                GetBasicVariantAtPosition("chr1", 7),
                GetBasicVariantAtPosition("chr1", 10),
                GetBasicVariantAtPosition("chr2", 8),
                GetBasicVariantAtPosition("chr4", 2)
            };

            var chr1Intervals = new List<Region>()
            {
                new Region(2, 3),
                new Region(6, 8),
                new Region(10, 11)
            };

            var chr2Intervals = new List<Region>()
            {
                new Region(4, 4)
            };

            var chr3Intervals = new List<Region>()
            {
                new Region(7, 8)
            };

            var chr4Intervals = new List<Region>()
            {
                new Region(4, 5)
            };

            Action<string, List<Region>> writeChr = (chrName, intervals) =>
            {
                var mapper = new RegionMapper(new ChrReference() { Name = chrName, Sequence = referenceSeq }, new ChrIntervalSet(intervals, chrName), 20);
                writer.Write(variants.Where(v => v.Chromosome == chrName), mapper);
                writer.WriteRemaining(mapper);
            };

            writer.WriteHeader();

            // write chr1 in increments
            var chr1Name = "chr1";
            var chr1Mapper = new RegionMapper(new ChrReference() { Name = chr1Name, Sequence = referenceSeq }, new ChrIntervalSet(chr1Intervals, chr1Name), 20);
            writer.Write(variants.Where(v => v.Chromosome == chr1Name && v.Coordinate == 7), chr1Mapper);
            writer.Write(variants.Where(v => v.Chromosome == chr1Name && v.Coordinate == 10), chr1Mapper);
            writer.WriteRemaining(chr1Mapper);

            writeChr("chr2", chr2Intervals);
            writeChr("chr3", chr3Intervals);
            writeChr("chr4", chr4Intervals);

            writer.Dispose();

            // expected output
            var expectations = new List<Tuple<string, bool>>()
            {
                new Tuple<string, bool>("chr1:2", false),
                new Tuple<string, bool>("chr1:3", false),
                new Tuple<string, bool>("chr1:6", false),
                new Tuple<string, bool>("chr1:7", true),
                new Tuple<string, bool>("chr1:8", false),
                new Tuple<string, bool>("chr1:10", true),
                new Tuple<string, bool>("chr1:11", false),
                new Tuple<string, bool>("chr2:4", false),
                new Tuple<string, bool>("chr2:8", true),
                new Tuple<string, bool>("chr3:7", false),
                new Tuple<string, bool>("chr3:8", false),
                new Tuple<string, bool>("chr4:2", true),
                new Tuple<string, bool>("chr4:4", false),
                new Tuple<string, bool>("chr4:5", false),


            };
            using (var reader = new VcfReader(outputFile))
            {
                var alleles = reader.GetVariants().ToList();

                Assert.Equal(expectations.Count, alleles.Count);

                for (var i = 0; i < expectations.Count; i ++)
                {
                    var allele = alleles[i];
                    var expected = expectations[i];

                    Assert.Equal(expected.Item1, allele.ReferenceName + ":" + allele.ReferencePosition);
                    Assert.Equal(expected.Item2 ? "T" : ".", allele.VariantAlleles[0]);
                    Assert.Equal(expected.Item2 ? "PASS" : "LowDP", allele.Filters);
                    Assert.Equal(expected.Item2 ? "100" : "0", allele.InfoFields["DP"]);
                }
            }
        }

        private CalledAllele GetBasicVariantAtPosition(string chrName, int position)
        {
            return new CalledAllele(AlleleCategory.Snv)
            {
                Chromosome =chrName,
                Coordinate = position,
                Reference = "C",
                Alternate = "T",
                TotalCoverage = 100
            };
        }
    }
}
