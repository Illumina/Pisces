using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hygea.Tests;
using CommandLine.Util;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class HygeaOptionsTests
    {
        private string _existingBamPath = Path.Combine(TestPaths.LocalTestDataDirectory, "var123var35.bam");
        private string _existingBamPath2 = Path.Combine(TestPaths.LocalTestDataDirectory, "var123var35_removedSQlines.bam");
        private string _existingGenome = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");
        private string _existingPriorsFile = Path.Combine(TestPaths.LocalTestDataDirectory, "priors.vcf");

        private Dictionary<string, Action<HygeaOptions>> GetOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<HygeaOptions>>();

            optionsExpectationsDict.Add("-minBaseQuality 40", (o) => Assert.Equal(40, o.MinimumBaseCallQuality));
            optionsExpectationsDict.Add(@"-BAMPaths " + _existingBamPath, (o) => Assert.Equal(_existingBamPath, o.BAMPaths[0]));
            optionsExpectationsDict.Add("-maxThreads 5", (o) => Assert.Equal(5, o.MaxNumThreads));
            optionsExpectationsDict.Add("-minDenovoFreq 0.555", (o) => Assert.Equal(0.555f, o.IndelFreqCutoff));
            optionsExpectationsDict.Add("-chrFilter chr9", (o) => Assert.Equal("chr9", o.ChromosomeFilter));
            optionsExpectationsDict.Add(@"-priorsFile " + _existingPriorsFile, (o) => Assert.Equal(_existingPriorsFile, o.PriorsPath));
            optionsExpectationsDict.Add(@"-genomeFolders " + _existingGenome, (o) => Assert.Equal(1, o.GenomePaths.Length));  //the num genome paths needs to agree with the num bam paths
            optionsExpectationsDict.Add(@"-outFolder C:\out", (o) => Assert.Equal(@"C:\out", o.OutputDirectory));
            optionsExpectationsDict.Add(@"-maxIndelSize 75", (o) => Assert.Equal(75, o.MaxIndelSize));
            optionsExpectationsDict.Add(@"-remaskSoftclips true", (o) => Assert.Equal(true, o.RemaskSoftclips));
            optionsExpectationsDict.Add(@"-maskPartialInsertion false", (o) => Assert.Equal(false, o.MaskPartialInsertion));
            optionsExpectationsDict.Add(@"-minimumUnanchoredInsertionLength 3", (o) => Assert.Equal(3, o.MinimumUnanchoredInsertionLength));
            optionsExpectationsDict.Add(@"-allowRescoringOrigZero false", (o) => Assert.Equal(false, o.AllowRescoringOrigZero));
            optionsExpectationsDict.Add(@"-maxRealignShift 500", (o) => Assert.Equal(500, o.MaxRealignShift));
            optionsExpectationsDict.Add(@"-indelCoefficient -10", (o) => Assert.Equal(-10, o.IndelCoefficient));
            optionsExpectationsDict.Add(@"-indelLengthCoefficient -9", (o) => Assert.Equal(-9, o.IndelLengthCoefficient));
            optionsExpectationsDict.Add(@"-mismatchCoefficient -8", (o) => Assert.Equal(-8, o.MismatchCoefficient));
            optionsExpectationsDict.Add(@"-softclipCoefficient -7", (o) => Assert.Equal(-7, o.SoftclipCoefficient));
            optionsExpectationsDict.Add(@"-anchorLengthCoefficient -6", (o) => Assert.Equal(-6, o.AnchorLengthCoefficient));
            optionsExpectationsDict.Add(@"-useAlignmentScorer true", (o) => Assert.Equal(true, o.UseAlignmentScorer));

            return optionsExpectationsDict;
        }

        [Fact]
        public void CommandLineWhitespaceParse()
        {
            var optionsExpectations = GetOptionsExpectations();
            Action<HygeaOptions> expectations = null;
            foreach (var option in optionsExpectations.Values)
            {
                expectations += option;
            }

            //Test with multiple options strung together by spaces.
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys), expectations);
            // extra spaces between ok
            ExecuteParsingTest(string.Join("   ", optionsExpectations.Keys), expectations);

            //Different separator shouldn't work
            TestParsingFail<FormatException>(string.Join(";", optionsExpectations.Keys));

            //Order shouldn't matter
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys.OrderByDescending(o => o)), expectations);
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys.OrderBy(o => o)), expectations);
        }

        [Fact]
        public void CommandLineParsing_Errors()
        {
            // focus on mal-formed arguments

            // argument value errors
            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { "-b", "89.5" }));
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-B", "20.4", "-blah" }));
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-f", "hi!" }));
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-f", "^%$" }));
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-f", "true" }));
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-Chromosome", "10" }));
        }

        [Fact]
        public void NumberOfBams()
        {
            var bams = new List<string>();
            var genomes = new List<string>();

            try
            {
                //0 bams - should error
                ExecuteValidationTest((o) => { o.BAMPaths = new string[] { }; }, false);

                //1 bams - should be ok
                ExecuteValidationTest((o) => { o.BAMPaths = new string[] { _existingBamPath }; }, true);

                for (int i = 0; i < 97; i++)
                {
                    var bam = "BAM_" + i + ".bam";
                    var genome = "Genome_" + i + ".bam";
                    bams.Add(bam);
                    genomes.Add(genome);
                    using (File.Create(bam))
                    {
                    }
                    using (File.Create(genome))
                    {
                    }
                }

                //96 bams - should be ok
                ExecuteValidationTest((o) => { o.BAMPaths = bams.Take(96).ToArray(); }, true);

                //97 bams - should error <- deliberately removed cap a long time ago, to support increasing plexity.
                //ExecuteValidationTest((o) => { o.BAMPaths = bams.ToArray(); }, false);

                ExecuteValidationTest((o) => { o.BAMPaths = bams.Take(96).ToArray(); }, true);

                //1536 bams - should be ok  <- highest current plexity
                ExecuteValidationTest((o) => { o.BAMPaths = bams.Take(1536).ToArray(); }, true);

                //3072 bams - should be ok
                ExecuteValidationTest((o) => { o.BAMPaths = bams.Take(3072).ToArray(); }, true);

            }
            finally
            {
                foreach (var bam in bams)
                {
                    File.Delete(bam);
                }
                foreach (var genome in genomes)
                {
                    File.Delete(genome);
                }
            }
        }

        [Fact]
        public void Validate()
        {
            // ---------------------
            // verify default should be valid
            // ---------------------
            var option = GetBasicOptions();
            option.Validate();

            // ---------------------------------------------------
            // Either BAMPath should be specified.
            // ---------------------------------------------------
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new[] { "bampath1.bam" };
            }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = new[] { _existingBamPath }; }, true); //this needs to be an exisitng bampath if we want it to pass.

            // ---------------------------------------------------
            // BAMFolder
            // Folder should exist
            // 1 Genome Path should be specified when BAMFolder is specified.
            // ---------------------------------------------------
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = new[] { @"C:\NonexistantBAMFolder" };
                o.GenomePaths = new[] { _existingGenome };
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = new[] { TestPaths.LocalTestDataDirectory };
                o.GenomePaths = new[] { "folder1", "folder2" };
            }, false);

            // ---------------------
            // BAM Paths
            // Duplicate BAMPaths detected.
            // BAM Path does not exist.
            // ---------------------
            ExecuteValidationTest((o) => { o.BAMPaths = new string[0]; }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = new[] { _existingBamPath, _existingBamPath }; }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = new[] { "nonexistant.bam" }; }, false);

            // genome paths
            ExecuteValidationTest((o) => { o.GenomePaths = null; }, false);
            ExecuteValidationTest((o) => { o.GenomePaths = new string[0]; }, false);
            ExecuteValidationTest((o) => { o.GenomePaths = new[] { _existingGenome, _existingGenome }; }, false);
            ExecuteValidationTest((o) => { o.GenomePaths = new[] { "nonexistant" }; }, false);
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new[] { _existingBamPath, _existingBamPath2 };
                o.GenomePaths = new[] { _existingGenome, _existingGenome };
            }, true);  // dup genomes ok

            // ---------------------
            // Priors Path
            // Must exist
            // ---------------------
            ExecuteValidationTest((o) => o.PriorsPath = "nonexistant.vcf", false);

            // ---------------------
            // verify parameters
            // ---------------------
            ExecuteValidationTest((o) => { o.MinimumBaseCallQuality = 0; }, true);
            ExecuteValidationTest((o) => { o.MinimumBaseCallQuality = -1; }, false);

            ExecuteValidationTest((o) => { o.IndelFreqCutoff = 0f; }, true);
            ExecuteValidationTest((o) => { o.IndelFreqCutoff = 1f; }, true);
            ExecuteValidationTest((o) => { o.IndelFreqCutoff = -0.99f; }, false);
            ExecuteValidationTest((o) => { o.IndelFreqCutoff = 1.01f; }, false);

            ExecuteValidationTest((o) => { o.OutputDirectory = TestPaths.LocalTestDataDirectory; }, true); // True for valid path

            ExecuteValidationTest((o) => { o.MaxNumThreads = 1; }, true);
            ExecuteValidationTest((o) => { o.MaxNumThreads = 0; }, false);

            ExecuteValidationTest((o) => { o.MaxIndelSize = 101; }, false);
            ExecuteValidationTest((o) => { o.MaxIndelSize = 0; }, false);
            ExecuteValidationTest((o) => { o.MaxIndelSize = 100; }, true);
        }

        [Fact]
        public void PopulateBAMPaths()
        {
            //Happy Path
            var options_1 = new HygeaOptions()
            {
                BAMPaths = new string[] { TestPaths.LocalTestDataDirectory },
                GenomePaths = new[] { _existingGenome },
            };

            Assert.NotNull(options_1.BAMPaths);
            Assert.True(options_1.BAMPaths.Length > 0);

            //no bam files found
            var options_3 = new HygeaOptions()
            {
                BAMPaths = new string[] { TestPaths.LocalTestDataDirectory },
                GenomePaths = new[] { _existingGenome },
            };
            var appOptions = new HygeaOptionParser() { Options = options_3 };
            appOptions.ValidateOptions();
            Assert.IsType<ArgumentException>(appOptions.ParsingResult.Exception);
        }


        private void TestParsingFail<T>(string arguments)
        {
            var ParseResult = GetParsedApplicationOptions(arguments).ParsingResult;

            Assert.IsType<T>(ParseResult.Exception);
            Assert.True(ParseResult.ExitCode != 0);

        }

        private void ExecuteParsingTest(string arguments, Action<HygeaOptions> assertions = null)
        {
            var options = GetParsedOptions(arguments);
            assertions(options);
        }

        private HygeaOptionParser GetParsedApplicationOptions(string arguments)
        {
            var parsedOptions = new HygeaOptionParser();
            parsedOptions.ParseArgs(arguments.Split(' '));
            return parsedOptions;
        }

        private HygeaOptions GetParsedOptions(string arguments)
        {
            var parsedOptions = GetParsedApplicationOptions(arguments);
            return parsedOptions.HygeaOptions;
        }

        private void ExecuteValidationTest(Action<HygeaOptions> testSetup, bool shouldPass)
        {
            var options = GetBasicOptions();
            var parser = new HygeaOptionParser() { Options=options};
            testSetup(options);
            parser.ValidateOptions();

            if (shouldPass)
                Assert.True(parser.HadSuccess);
            else
                Assert.True(parser.ParsingFailed);
        }

        private HygeaOptions GetBasicOptions()
        {
           var basicOptions =  new HygeaOptions()
            {
                BAMPaths = new[] { _existingBamPath },
                GenomePaths = new[] { _existingGenome }
            };

            basicOptions.SetIODirectories("Hygea");

            return basicOptions;
        }

        [Fact]
        public void GetLogFolder()
        {
            var options = GetBasicOptions();
            Assert.Equal(Path.Combine(Path.GetDirectoryName(_existingBamPath), "HygeaLogs"), options.LogFolder);

            // when multiple bam paths, use first bam file
            options = new HygeaOptions();
            var nonExistingBam = Path.Combine(TestPaths.LocalScratchDirectory, "nonexistant.bam");
            options.BAMPaths = new[] { nonExistingBam, _existingBamPath };
            options.SetIODirectories("Hygea");
            Assert.Equal(Path.Combine(Path.GetDirectoryName(nonExistingBam), "HygeaLogs"), options.LogFolder);

            // use output folder if provided
            options = new HygeaOptions();
            options.BAMPaths = new[] { _existingBamPath };
            options.OutputDirectory = @"C:\SomeOutput";
            options.SetIODirectories("Hygea");
            Assert.Equal(Path.Combine(options.OutputDirectory, "HygeaLogs"), options.LogFolder);
        }



        [Fact]
        public void ParseConstructor()
        {
            // happy path
            var options = GetParsedApplicationOptions(
                    string.Format("-bamPaths {0} -genomeFolders {1} -minBaseQuality 15", _existingBamPath, _existingGenome)).HygeaOptions;

            Assert.Equal(_existingBamPath, options.BAMPaths[0]);
            Assert.Equal(_existingGenome, options.GenomePaths[0]);
            Assert.Equal(15, options.MinimumBaseCallQuality);

            // verify we are expanding bamfolder to filepaths
            options = GetParsedApplicationOptions(
               string.Format("-bamPaths {0} -genomeFolders {1}", Path.GetDirectoryName(_existingBamPath), _existingGenome)).HygeaOptions;

            Assert.True(!string.IsNullOrEmpty(options.BAMPaths[0]));

            // verify that we are validating
            var applicationOptions1 = GetParsedApplicationOptions(
               string.Format("-unknown {0} -genomeFolders {1}", Path.GetDirectoryName(_existingBamPath), _existingGenome));
            Assert.IsType<ArgumentException>(applicationOptions1.ParsingResult.Exception);

            var applicationOptions2 = GetParsedApplicationOptions(
             string.Format("-bamFile {0}", _existingBamPath));
            Assert.IsType<ArgumentException>(applicationOptions2.ParsingResult.Exception);


            // check we are setting max threads          
            var applicationOptions3 = GetParsedApplicationOptions(
              string.Format("-bamPaths {0} -genomeFolders {1} -maxThreads 1", _existingBamPath, _existingGenome));
            Assert.Equal(1, applicationOptions3.HygeaOptions.MaxNumThreads);

            var applicationOptions4 = GetParsedApplicationOptions(
                string.Format("-bamPaths {0} -genomeFolders {1} -maxThreads 100", _existingBamPath, _existingGenome));
            Assert.Equal(100, applicationOptions4.HygeaOptions.MaxNumThreads);

        }

        [Fact]
        public void PrintOptionsTest()
        {
            // "help|h" should disply help. At least check it doesnt crash.

            try
            {
                Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-h" }));
                Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "--h" }));
                Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-Help" }));
            }
            catch
            {
                Assert.True(false);
            }
        }
    }
}
