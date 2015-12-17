using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CallSomaticVariants.Logic;
using CallSomaticVariants.Logic.Calculators;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests
{
    public class VcfFileWriterTests
    {
        private readonly List<BaseCalledAllele> _defaultCandidates = new List<BaseCalledAllele>()
        {
            new CalledVariant(AlleleCategory.Snv)
            {
                Chromosome = "chr1",
                Coordinate = 123,
                Reference = "A",
                Alternate = "T",
                Qscore = 25,
                Genotype = Genotype.HeterozygousAlt,
                FractionNoCalls = float.Parse("0.001"),
                Filters = new List<FilterType>() {}
            },
            new CalledReference()
            {
                Chromosome = "chr1",
                Coordinate = 567,
                Reference = "A",
                Alternate = ".",
                Qscore = 20,
                Genotype = Genotype.HeterozygousAlt,
                FractionNoCalls = float.Parse("0.001"),
                Filters = new List<FilterType>() {FilterType.LowDepth, FilterType.LowQscore, FilterType.StrandBias}
            },
            new CalledVariant(AlleleCategory.Mnv)
            {
                Chromosome = "chr1",
                Coordinate = 234,
                Reference = "ATCA",
                Alternate = "TCGC",
                Qscore = 25,
                Genotype = Genotype.HeterozygousAlt,
                FractionNoCalls = float.Parse("0.001"),
                Filters = new List<FilterType>() {}
            },
            new CalledReference()
            {
                Chromosome = "chr1",
                Coordinate = 456,
                Reference = "A",
                Alternate = "T",
                Genotype = Genotype.HomozygousRef,
                Qscore = 27,
                FractionNoCalls = float.Parse("0.0124"),
                TotalCoverage = 99,
                AlleleSupport = 155,
                StrandBiasResults = new StrandBiasResults() {GATKBiasScore = float.Parse("0.25")}
            },
            new CalledVariant(AlleleCategory.Snv)
            {
                Chromosome = "chr1",
                Coordinate = 678,
                Reference = "A",
                Alternate = "T",
                Qscore = 25,
                Genotype = Genotype.HeterozygousAlt,
                FractionNoCalls = float.Parse("0.001"),
                Filters = new List<FilterType>() {FilterType.LowDepth}
            }
        };

        // When the -OutFolder is included the output files must go to the specified folder, and create it if it does not exist.
        [Fact]
        public void TestWithVariants()
        {
            var outputFile = Path.Combine(UnitTestPaths.TestDataDirectory, "VcfFileWriterTests_AdHoc.vcf");
            File.Delete(outputFile);

            var context = new VcfWriterInputContext
            {
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
                    StrandBiasFilterThreshold = 0.5f,
                    FrequencyFilterThreshold = 0.007f,
                    ShouldOutputNoCallFraction = true,
                    ShouldOutputStrandBiasAndNoiseLevel = true,
                    ShouldFilterOnlyOneStrandCoverage = true,
                    EstimatedBaseCallQuality = 23
                },
                context);

            var candidates = new List<BaseCalledAllele>()
            {
                new CalledVariant(AlleleCategory.Snv)
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
                    ReferenceSupport = 7
                }
            };

            writer.WriteHeader();
            writer.Write(candidates);
            writer.Dispose();

            Assert.Throws<Exception>(() => writer.WriteHeader());
            Assert.Throws<Exception>(() => writer.Write(candidates));
            writer.Dispose();

            var variantLine = @"chr4	55141055	.	A	G	0	PASS	DP=5394	GT:GQ:AD:VF:NL:SB:NC	1/1:0:7,5387:0.9987:23:0.0000:0.0000";
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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
                CommandLine = "myCommandLine",
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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

            TestHelper.VcfFileFormatValidation(outputFilePath);
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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
                        case "##INFO=<ID=TI":
                            Assert.True(Regex.IsMatch(x, "^##INFO=<ID=TI,Number=\\.,Type=String,Description=\"Transcript ID\">$"));
                            break;
                        case "##INFO=<ID=GI":
                            Assert.True(Regex.IsMatch(x, "^##INFO=<ID=GI,Number=\\.,Type=String,Description=\"Gene ID\">$"));
                            break;
                        case "##INFO=<ID=EXON":
                            Assert.True(Regex.IsMatch(x, "^##INFO=<ID=EXON,Number=0,Type=Flag,Description=\"Exon Region\">$"));
                            break;
                        case "##INFO=<ID=FC":
                            Assert.True(Regex.IsMatch(x, "^##INFO=<ID=FC,Number=\\.,Type=String,Description=\"Functional Consequence\">$"));
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
                        case "##FORMAT=<ID=VF":
                            Assert.True(Regex.IsMatch(x, "^##FORMAT=<ID=VF,Number=1,Type=Float,Description=\"Variant Frequency\">$"));
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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
                CommandLine = "myCommandLine",
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
                    QscoreFilterThreshold = 20,
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
                CommandLine = "myCommandLine",
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
                CommandLine = "myCommandLine",
                SampleName = "mySample",
                ReferenceName = "myReference"
            };

            var writer = new VcfFileWriter(
                outputFile,
                new VcfWriterConfig
                {
                    ShouldFilterOnlyOneStrandCoverage = true,
                    FrequencyFilterThreshold = 0.01f,
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
                CommandLine = "myCommandLine",
                SampleName = "mySample",
                ReferenceName = "myReference"
            };

            var writer = new VcfFileWriter(
                outputFile,
                new VcfWriterConfig
                {
                    StrandBiasFilterThreshold = 5,
                    FrequencyFilterThreshold = 0.01f,
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
            bool formatLowDP = false, formatQ = false, formatSB = false;
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
                    default:
                        if (Regex.IsMatch(x, string.Format("##FILTER=<ID=q{0}", config.QscoreFilterThreshold)))
                        {
                            Assert.True(Regex.IsMatch(x, string.Format("^##FILTER=<ID=q{0},Description=\"Quality below {0}\">$", config.QscoreFilterThreshold)));
                            formatQ = true;
                        }
                        else
                        {
                            Assert.True(false, "A filter is listed which does not match any of the specified filters.");
                        }

                        break;
                }
            }

            if (config.QscoreFilterThreshold > 0)
                Assert.True(formatQ);

            if (config.DepthFilterThreshold > 0)
                Assert.True(formatLowDP);

            if (config.ShouldOutputStrandBiasAndNoiseLevel)
                Assert.True(formatSB);
        }
    }
}
