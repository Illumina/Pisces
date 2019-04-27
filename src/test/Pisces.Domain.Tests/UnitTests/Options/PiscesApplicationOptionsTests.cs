using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using CommandLine.Util;
using CommandLine.Options;
using Xunit;


namespace Pisces.Domain.Tests
{
    public class PiscesApplicationOptionsTests
    {
        [Fact]
        [Trait("ReqID", "SDS-13")]
        [Trait("ReqID", "SDS-14")]
        public void CheckLogFolderTest()
        {
            var bamFilePath = Path.Combine(TestPaths.SharedBamDirectory, "Chr17Chr19.bam");
            var genomePath = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "PiscesApplicationsOptionsTests");
            var defaultLogFolder = "PiscesLogs";

            //check when an out folder is specified
            var options = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                GenomePaths = new[] { genomePath },
                VcfWritingParameters = new VcfWritingParameters()
                {
                    OutputGvcfFile = false
                },
                OutputDirectory = outDir
            };
            options.SetIODirectories("Pisces");
            Assert.Equal(Path.Combine(outDir, defaultLogFolder), options.LogFolder);

            //check when a bam is specified w/no out folder
            options = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                GenomePaths = new[] { genomePath },
                VcfWritingParameters = new VcfWritingParameters()
                {
                    OutputGvcfFile = false
                },
            };
            options.SetIODirectories("Pisces");
            Assert.Equal(Path.Combine(TestPaths.SharedBamDirectory, defaultLogFolder), options.LogFolder);

            //check when a bam parent folder does not exist
            options = new PiscesApplicationOptions
            {
                BAMPaths = new[] { "mybam.bam" },
                GenomePaths = new[] { genomePath },
                VcfWritingParameters = new VcfWritingParameters()
                {
                    OutputGvcfFile = false
                },
            };
            options.SetIODirectories("Pisces");
            Assert.Equal(defaultLogFolder, options.LogFolder);

        }

        private string _existingBamPath = Path.Combine(TestPaths.SharedBamDirectory, "Chr17Chr19.bam");
        private string _existingBamPath2 = Path.Combine(TestPaths.SharedBamDirectory, "Chr17Chr19_removedSQlines.bam");
        private string _existingGenome = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");
        private string _existingInterval = Path.Combine(TestPaths.SharedIntervalsdDirectory, "chr17only.picard");


        private Dictionary<string, Action<PiscesApplicationOptions>> GetOriginalOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<PiscesApplicationOptions>>();

            optionsExpectationsDict.Add("-minvq 40", (o) => Assert.Equal(40, o.VariantCallingParameters.MinimumVariantQScore));
            optionsExpectationsDict.Add("-minbq 41", (o) => Assert.Equal(41, o.BamFilterParameters.MinimumBaseCallQuality));
            optionsExpectationsDict.Add(@"-B C:\test.bam,C:\test2.bam", (o) => Assert.Equal(2, o.BAMPaths.Length));
            optionsExpectationsDict.Add("-c 4", (o) => Assert.Equal(4, o.VariantCallingParameters.MinimumCoverage));
            optionsExpectationsDict.Add("-d true", (o) => Assert.True(o.DebugMode));
            optionsExpectationsDict.Add("-debug true", (o) => Assert.True(o.DebugMode));
            optionsExpectationsDict.Add("-minvf 0.555", (o) => Assert.Equal(0.555f, o.VariantCallingParameters.MinimumFrequency));
            optionsExpectationsDict.Add("-vqfilter 45", (o) => Assert.Equal(45, o.VariantCallingParameters.MinimumVariantQScoreFilter));
            optionsExpectationsDict.Add("-ssfilter true", (o) => Assert.True(o.VariantCallingParameters.FilterOutVariantsPresentOnlyOneStrand));
            optionsExpectationsDict.Add(@"-g C:\genome,C:\genome2", (o) => Assert.Equal(2, o.GenomePaths.Length));
            optionsExpectationsDict.Add("-NL 40", (o) => Assert.Equal(40, o.VariantCallingParameters.ForcedNoiseLevel));
            optionsExpectationsDict.Add("-gVCF true", (o) => Assert.True(o.VcfWritingParameters.OutputGvcfFile));
            optionsExpectationsDict.Add("-CallMNVs true", (o) => Assert.True(o.CallMNVs));
            optionsExpectationsDict.Add("-MaxMNVLength 40", (o) => Assert.Equal(40, o.MaxSizeMNV));
            optionsExpectationsDict.Add("-MaxGapBetweenMNV 40", (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            optionsExpectationsDict.Add(@"-i C:\blah,C:\blah2", (o) => Assert.Equal(2, o.IntervalPaths.Length));
            optionsExpectationsDict.Add("-minmq 40", (o) => Assert.Equal(40, o.BamFilterParameters.MinimumMapQuality));
            optionsExpectationsDict.Add("-SBModel poisson", (o) => Assert.Equal(StrandBiasModel.Poisson, o.VariantCallingParameters.StrandBiasModel));
            optionsExpectationsDict.Add("-outputsbfiles true", (o) => Assert.True(o.OutputBiasFiles));
            optionsExpectationsDict.Add("-pp true", (o) => Assert.True(o.BamFilterParameters.OnlyUseProperPairs));
            optionsExpectationsDict.Add("-maxvq 400", (o) => Assert.Equal(400, o.VariantCallingParameters.MaximumVariantQScore));
            optionsExpectationsDict.Add("-sbfilter 0.7", (o) => Assert.Equal(0.7f, o.VariantCallingParameters.StrandBiasAcceptanceCriteria));
            optionsExpectationsDict.Add("-t 40", (o) => Assert.Equal(40, o.MaxNumThreads));
            optionsExpectationsDict.Add("-ReportNoCalls true", (o) => Assert.True(o.VcfWritingParameters.ReportNoCalls));
            optionsExpectationsDict.Add(@"-OutFolder C:\out", (o) => Assert.Equal(@"C:\out", o.OutputDirectory));
            optionsExpectationsDict.Add("-ThreadByChr true", (o) => Assert.Equal(true, o.ThreadByChr));
            optionsExpectationsDict.Add("-usestitchedXD true", (o) => Assert.Equal(true, o.UseStitchedXDInfo));

            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<PiscesApplicationOptions>> GetLongHandOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<PiscesApplicationOptions>>();

            optionsExpectationsDict.Add("-MinVariantQScore 40", (o) => Assert.Equal(40, o.VariantCallingParameters.MinimumVariantQScore));
            optionsExpectationsDict.Add("-MinBaseCallQuality 40", (o) => Assert.Equal(40, o.BamFilterParameters.MinimumBaseCallQuality));
            optionsExpectationsDict.Add(@"-BamPaths C:\test.bam,C:\test2.bam", (o) => Assert.Equal(2, o.BAMPaths.Length));
            optionsExpectationsDict.Add("-MinDp 40", (o) => Assert.Equal(40, o.VariantCallingParameters.MinimumCoverage));
            optionsExpectationsDict.Add("-debug true", (o) => Assert.True(o.DebugMode));
            optionsExpectationsDict.Add("-MinimumFrequency 0.555", (o) => Assert.Equal(0.555f, o.VariantCallingParameters.MinimumFrequency));
            optionsExpectationsDict.Add("-VariantQualityFilter 40", (o) => Assert.Equal(40, o.VariantCallingParameters.MinimumVariantQScoreFilter));
            optionsExpectationsDict.Add("-EnableSingleStrandFilter true", (o) => Assert.True(o.VariantCallingParameters.FilterOutVariantsPresentOnlyOneStrand));
            optionsExpectationsDict.Add(@"-GenomePaths C:\genome,C:\genome2", (o) => Assert.Equal(2, o.GenomePaths.Length));
            optionsExpectationsDict.Add("-NoiseLevelForQModel 40", (o) => Assert.Equal(40, o.VariantCallingParameters.ForcedNoiseLevel));
            optionsExpectationsDict.Add("-gVCF true", (o) => Assert.True(o.VcfWritingParameters.OutputGvcfFile));
            optionsExpectationsDict.Add("-CallMNVs true", (o) => Assert.True(o.CallMNVs));
            optionsExpectationsDict.Add("-MaxMNVLength 40", (o) => Assert.Equal(40, o.MaxSizeMNV));
            optionsExpectationsDict.Add("-MaxGapBetweenMNV 40", (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            optionsExpectationsDict.Add(@"-IntervalPaths C:\blah,C:\blah2", (o) => Assert.Equal(2, o.IntervalPaths.Length));
            optionsExpectationsDict.Add("-MinMapQuality 40", (o) => Assert.Equal(40, o.BamFilterParameters.MinimumMapQuality));
            optionsExpectationsDict.Add("-SBModel poisson", (o) => Assert.Equal(StrandBiasModel.Poisson, o.VariantCallingParameters.StrandBiasModel));
            optionsExpectationsDict.Add("-OutputSBFiles true", (o) => Assert.True(o.OutputBiasFiles));
            optionsExpectationsDict.Add("-OnlyUseProperPairs true", (o) => Assert.True(o.BamFilterParameters.OnlyUseProperPairs));
            optionsExpectationsDict.Add("-MaxVariantQScore 1000", (o) => Assert.Equal(1000, o.VariantCallingParameters.MaximumVariantQScore));
            optionsExpectationsDict.Add("-MaxAcceptableStrandBiasFilter 0.7", (o) => Assert.Equal(0.7f, o.VariantCallingParameters.StrandBiasAcceptanceCriteria));
            optionsExpectationsDict.Add("-MaxNumThreads 10", (o) => Assert.Equal(10, o.MaxNumThreads));
            optionsExpectationsDict.Add("-ReportNoCalls true", (o) => Assert.True(o.VcfWritingParameters.ReportNoCalls));
            optionsExpectationsDict.Add(@"-OutFolder C:\out", (o) => Assert.Equal(@"C:\out", o.OutputDirectory));
            optionsExpectationsDict.Add("-ThreadByChr true", (o) => Assert.Equal(true, o.ThreadByChr));
            optionsExpectationsDict.Add("-InsideSubProcess true", (o) => Assert.Equal(true, o.InsideSubProcess));
            optionsExpectationsDict.Add("-BaseLogName newlogname.txt", (o) => Assert.Equal("newlogname.txt", o.LogFileNameBase));
            optionsExpectationsDict.Add("--UseStitchedXD false", (o) => Assert.Equal(false, o.UseStitchedXDInfo));

            return optionsExpectationsDict;
        }


        [Fact]
        [Trait("ReqID", "SDS-1")]
        public void CommandLineWhitespaceParse()
        {
            var shortHandOptionsExpectations = GetOriginalOptionsExpectations();
            Action<PiscesApplicationOptions> shortHandExpectations = null;
            foreach (var option in shortHandOptionsExpectations.Values)
            {
                shortHandExpectations += option;
            }

            var longHandOptionsExpectations = GetLongHandOptionsExpectations();
            Action<PiscesApplicationOptions> longHandExpectations = null;
            foreach (var option in longHandOptionsExpectations.Values)
            {
                longHandExpectations += option;
            }

            //Test with multiple options strung together by spaces.
            ExecuteParsingOnlyTest(string.Join(" ", longHandOptionsExpectations.Keys), true, longHandExpectations);
            ExecuteParsingOnlyTest(string.Join(" ", shortHandOptionsExpectations.Keys), true, shortHandExpectations);

            //Different separator shouldn't work
            ExecuteParsingOnlyTest(string.Join(";", longHandOptionsExpectations.Keys), false, longHandExpectations);

            //Order shouldn't matter
            ExecuteParsingOnlyTest(string.Join(" ", longHandOptionsExpectations.Keys.OrderByDescending(o => o)), true, longHandExpectations);
            ExecuteParsingOnlyTest(string.Join(" ", longHandOptionsExpectations.Keys.OrderBy(o => o)), true, longHandExpectations);

        }


        [Fact]
        [Trait("ReqID", "SDS-2")]
        public void CommandLineParsing()
        {

            //TODO ensure that everything here is in line item in SDS-2 and vice versa
            // make sure arguments get mapped to the right fields
            ExecuteParsingOnlyTest("-minvq 40", true, (o) => Assert.Equal(40, o.VariantCallingParameters.MinimumVariantQScore));
            ExecuteParsingOnlyTest("-minbq 40", true, (o) => Assert.Equal(40, o.BamFilterParameters.MinimumBaseCallQuality));
            ExecuteParsingOnlyTest(@"-B C:\test.bam,C:\test2.bam", true, (o) => Assert.Equal(2, o.BAMPaths.Length));
            ExecuteParsingOnlyTest("-mindp 40", true, (o) => Assert.Equal(40, o.VariantCallingParameters.MinimumCoverage));
            ExecuteParsingOnlyTest("-d true", true, (o) => Assert.True(o.DebugMode));
            ExecuteParsingOnlyTest("-debug true", true, (o) => Assert.True(o.DebugMode));
            ExecuteParsingOnlyTest("-minvf 0.555", true, (o) => Assert.Equal(0.555f, o.VariantCallingParameters.MinimumFrequency));
            ExecuteParsingOnlyTest("-vqfilter 40", true, (o) => Assert.Equal(40, o.VariantCallingParameters.MinimumVariantQScoreFilter));
            ExecuteParsingOnlyTest("-ssfilter true", true, (o) => Assert.True(o.VariantCallingParameters.FilterOutVariantsPresentOnlyOneStrand));
            ExecuteParsingOnlyTest(@"-g C:\genome,C:\genome2", true, (o) => Assert.Equal(2, o.GenomePaths.Length));
            ExecuteParsingOnlyTest("-NL 40", true, (o) => Assert.Equal(40, o.VariantCallingParameters.ForcedNoiseLevel));
            ExecuteParsingOnlyTest("-gVCF true", true, (o) => Assert.True(o.VcfWritingParameters.OutputGvcfFile));
            ExecuteParsingOnlyTest("-CallMNVs true", true, (o) => Assert.True(o.CallMNVs));
            ExecuteParsingOnlyTest("-MaxMNVLength 40", true, (o) => Assert.Equal(40, o.MaxSizeMNV));
            ExecuteParsingOnlyTest("-MaxGapBetweenMNV 40", true, (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            ExecuteParsingOnlyTest(@"-i C:\blah,C:\blah2", true, (o) => Assert.Equal(2, o.IntervalPaths.Length));
            ExecuteParsingOnlyTest("-minmq 40", true, (o) => Assert.Equal(40, o.BamFilterParameters.MinimumMapQuality));
            ExecuteParsingOnlyTest("-SBModel poisson", true, (o) => Assert.Equal(StrandBiasModel.Poisson, o.VariantCallingParameters.StrandBiasModel));
            ExecuteParsingOnlyTest("-SBModel extended", true, (o) => Assert.Equal(StrandBiasModel.Extended, o.VariantCallingParameters.StrandBiasModel));
            ExecuteParsingOnlyTest("-SBModel random", false);
            ExecuteParsingOnlyTest("-outputsbfiles true", true, (o) => Assert.True(o.OutputBiasFiles));
            ExecuteParsingOnlyTest("-pp true", true, (o) => Assert.True(o.BamFilterParameters.OnlyUseProperPairs));
            ExecuteParsingOnlyTest("-maxvq 40", true, (o) => Assert.Equal(40, o.VariantCallingParameters.MaximumVariantQScore));
            ExecuteParsingOnlyTest("-sbfilter 0.7", true, (o) => Assert.Equal(0.7f, o.VariantCallingParameters.StrandBiasAcceptanceCriteria));
            ExecuteParsingOnlyTest("-t 40", true, (o) => Assert.Equal(40, o.MaxNumThreads));
            ExecuteParsingOnlyTest("-ReportNoCalls true", true, (o) => Assert.True(o.VcfWritingParameters.ReportNoCalls));
            ExecuteParsingOnlyTest(@"-OutFolder C:\out", true, (o) => Assert.Equal(@"C:\out", o.OutputDirectory));
            ExecuteParsingOnlyTest(@"-bampaths C:\bamfolder", true, (o) => Assert.Equal(@"C:\bamfolder", o.BAMPaths[0]));
            ExecuteParsingOnlyTest(@"-vffilter 20.1", true, (o) => Assert.Equal(20.1f, o.VariantCallingParameters.MinimumFrequencyFilter));
            ExecuteParsingOnlyTest(@"-ploidy diploid", true, (o) => Assert.Equal(PloidyModel.DiploidByThresholding, o.VariantCallingParameters.PloidyModel));
            ExecuteParsingOnlyTest(@"-ploidy diploidByadaptiveGT", true, (o) => Assert.Equal(PloidyModel.DiploidByAdaptiveGT, o.VariantCallingParameters.PloidyModel));
            ExecuteParsingOnlyTest(@"-repeatfilter_ToBeRetired 5", true, (o) => Assert.Equal(5, o.VariantCallingParameters.IndelRepeatFilter));
            ExecuteParsingOnlyTest(@"-DuplicateReadFilter false", true, (o) => Assert.Equal(false, o.BamFilterParameters.RemoveDuplicates));
            ExecuteParsingOnlyTest(@"-mindpfilter 3", true, (o) => Assert.Equal(3, o.VariantCallingParameters.LowDepthFilter));
            ExecuteParsingOnlyTest(@"-TargetLODFrequency 0.45", true, (o) => Assert.Equal(0.45f, o.VariantCallingParameters.TargetLODFrequency));
            ExecuteParsingOnlyTest(@"-Collapse true", true, (o) => Assert.True(o.Collapse));
            ExecuteParsingOnlyTest(@"-Collapse false", true, (o) => Assert.False(o.Collapse));


            ExecuteParsingOnlyTest(@"  -PriorsPath C:\path", true, (o) => Assert.Equal(@"C:\path", o.PriorsPath));
            ExecuteParsingOnlyTest(@"  -DiploidSNVGenotypeParameters 0.10,0.20,0.78", true, (o) =>
                    Assert.True(
                        (0.10f == o.VariantCallingParameters.DiploidSNVThresholdingParameters.MinorVF) &&
                        (0.20f == o.VariantCallingParameters.DiploidSNVThresholdingParameters.MajorVF) &&
                        (0.78f == o.VariantCallingParameters.DiploidSNVThresholdingParameters.SumVFforMultiAllelicSite)));
            ExecuteParsingOnlyTest(@"-RMxNFilter false", true, (o) => Assert.True(
                        (null == o.VariantCallingParameters.RMxNFilterMaxLengthRepeat) &&
                        (null == o.VariantCallingParameters.RMxNFilterMinRepetitions)));
            ExecuteParsingOnlyTest(@"-RMxNFilter true", true, (o) => Assert.True(
                        (5 == o.VariantCallingParameters.RMxNFilterMaxLengthRepeat) &&
                        (9 == o.VariantCallingParameters.RMxNFilterMinRepetitions)));
            ExecuteParsingOnlyTest(@"-RMxNFilter 11,3", true, (o) => Assert.True(
                        (11 == o.VariantCallingParameters.RMxNFilterMaxLengthRepeat) &&
                        (3 == o.VariantCallingParameters.RMxNFilterMinRepetitions)));
            ExecuteParsingOnlyTest(@"-RMxNFilter 11,3,0.30", true, (o) => Assert.True(
                (11 == o.VariantCallingParameters.RMxNFilterMaxLengthRepeat) &&
                (3 == o.VariantCallingParameters.RMxNFilterMinRepetitions) &&
                (0.3f == o.VariantCallingParameters.RMxNFilterFrequencyLimit)));
            ExecuteParsingOnlyTest(@"-RMxNFilter 11,3,5,0.20", false);
            ExecuteParsingOnlyTest(@"-RMxNFilter 5", false);
            ExecuteParsingOnlyTest(@"-RMxNFilter yourmom", false);
            ExecuteParsingOnlyTest(@"-ThreadByChr true", true, (o) => Assert.True(o.ThreadByChr));
            ExecuteParsingOnlyTest(@"-ThreadByChr boo", false);
            ExecuteParsingOnlyTest(@"-diploidbinomialmodel boo", false);
            ExecuteParsingOnlyTest(@"-SkipNonIntervalAlignments meh", false);  //<- change after argument refactor. We are no longer being kind to unsupported arguments.

            ExecuteParsingOnlyTest(@"-ncfilter 0.4", true, (o) => Assert.True(0.4f == o.VariantCallingParameters.NoCallFilterThreshold));
            ExecuteParsingOnlyTest(@"-ncfilter true", false);
        }

        [Fact]
        [Trait("ReqID", "SDS-3")]
        public void CommandLineParsing_Errors()
        {
            // focus on mal-formed arguments

            // argument mismatches
            ExecuteParsingOnlyTest("-rmxnfilter 4,5,6,7", false);
            ExecuteParsingOnlyTest("-mingq *", false);
            ExecuteParsingOnlyTest("-pp 20", false);
            ExecuteParsingOnlyTest("-maxvq 40.1", false);

            // enum values
            ExecuteParsingOnlyTest("-ploidy help", false);
            ExecuteParsingOnlyTest("-SBModel bogus", false);
        }

        [Fact]
        [Trait("ReqID", "SDS-4")]
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

                for (int i = 0; i < 4000; i++)
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
        public void PopulateBAMPaths()
        {
            var BAMFolder = TestPaths.SharedBamDirectory;
            //Happy Path
            var options_1 = new PiscesApplicationOptions()
            {
                BAMPaths = BamProcessorParsingUtils.UpdateBamPathsWithBamsFromFolder(BAMFolder),
                GenomePaths = new[] { _existingGenome },
                IntervalPaths = new[] { _existingInterval }
            };

            Assert.NotNull(options_1.BAMPaths);
            Assert.True(options_1.BAMPaths.Length > 0);

            //no bam files found
            var options_3 = new PiscesApplicationOptions()
            {
                GenomePaths = new[] { _existingGenome },
            };

            var parser = new PiscesOptionsParser() { Options = options_3 };
            Assert.Null(options_3.BAMPaths);

            Assert.Throws<ArgumentException>(() => parser.ValidateAndSetDerivedValues());
        }

        [Fact]
        public void MaxNumberThreads()
        {
            string bamFolder = TestPaths.SharedBamDirectory;
            var commandLine1 = string.Format("-minvq 40 -minbq 40 -b {0} -t 1000 -g {1} -vqfilter 40", bamFolder, _existingGenome);
            ExecuteParsingAndSetDerivedParams(commandLine1, (o) => Assert.Equal(Environment.ProcessorCount, o.MaxNumThreads));

            var commandLine2 = string.Format("-minvq 40 -minbq 40 -b {0} -g {1} -vqfilter 40", bamFolder, _existingGenome);
            ExecuteParsingAndSetDerivedParams(commandLine2, (o) => Assert.Equal(Environment.ProcessorCount, o.MaxNumThreads));

            var commandLine3 = string.Format("-minvq 40 -minbq 40 -b {0} -g {1} -vqfilter 40 -t 1", bamFolder, _existingGenome);
            ExecuteParsingAndSetDerivedParams(commandLine3, (o) => Assert.Equal(1, o.MaxNumThreads));
        }



        [Fact]
        [Trait("ReqID", "SDS-3")]
        [Trait("ReqID", "SDS-56")]
        public void Validate()
        {
            // ---------------------
            // verify default should be valid
            // ---------------------
            var option = GetBasicOptions();
            var parser = new PiscesOptionsParser() { Options = option };
            parser.ValidateAndSetDerivedValues();

            // ---------------------
            // verify log folder
            // ---------------------
            Assert.Equal(Path.Combine(TestPaths.SharedBamDirectory, "PiscesLogs"), option.LogFolder);

            // ---------------------------------------------------
            // BAMPath(s) should be specified.
            // ---------------------------------------------------
            ExecuteValidationTest((o) => { o.BAMPaths = new[] { _existingBamPath }; }, true);
            ExecuteValidationTest((o) => { o.BAMPaths = new[] { "bampath1.bam" }; }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = null; }, false);


            // ---------------------------------------------------
            // BAMFolder
            // Folder should exist (given as the first string in BAMPaths)
            // 1 Genome Path should be specified when BAMFolder is specified.
            // Atmost 1 Interval Path should be specified when BAMFolder is specified.
            // Threading by chromosome is not supported when BAMFolder is specified.
            // ---------------------------------------------------
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = new string[] { @"C:\NonexistantBAMFolder" };
                o.GenomePaths = new[] { _existingGenome };
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = new string[] { TestPaths.SharedBamDirectory };
                o.GenomePaths = new[] { "folder1", "folder2" };
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = new string[] { TestPaths.SharedBamDirectory };
                o.GenomePaths = new[] { _existingGenome };
                o.IntervalPaths = new[] { "file1.intervals", "file2.intervals" };
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = BamProcessorParsingUtils.UpdateBamPathsWithBamsFromFolder(TestPaths.SharedBamDirectory);
                o.GenomePaths = new[] { _existingGenome };
                o.IntervalPaths = new[] { _existingInterval };
            }, true);

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

            // intervals
            ExecuteValidationTest((o) => { o.IntervalPaths = new[] { _existingInterval, _existingInterval }; }, false);
            ExecuteValidationTest((o) => { o.IntervalPaths = new[] { "nonexistant.picard" }; }, false);
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new[] { _existingBamPath, _existingBamPath2 };
                o.IntervalPaths = new[] { _existingInterval, _existingInterval };
            }, true);  // dup intervals ok
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new[] { _existingBamPath, _existingBamPath2 };
                o.IntervalPaths = new[] { _existingInterval, _existingInterval, _existingInterval };
            }, false);

            // ---------------------
            // verify parameters
            // ---------------------
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumVariantQScore = 0; }, true);
            ExecuteValidationTest((o) =>
            {
                o.VariantCallingParameters.MinimumVariantQScore = 100;
                o.VariantCallingParameters.MinimumVariantQScoreFilter = 100;
            }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumVariantQScore = -1; }, false);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumVariantQScore = 101; }, false);

            ExecuteValidationTest((o) =>
            {
                o.VariantCallingParameters.MaximumVariantQScore = 0;
                o.VariantCallingParameters.MinimumVariantQScore = 0;
                o.VariantCallingParameters.MinimumVariantQScoreFilter = 0;
            }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MaximumVariantQScore = 100; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MaximumVariantQScore = -1; }, false);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MaximumVariantQScore = 101; }, true);

            ExecuteValidationTest((o) =>
            {
                o.VariantCallingParameters.MinimumVariantQScore = 50;
                o.VariantCallingParameters.MaximumVariantQScore = 49;
            }, false);

            ExecuteValidationTest((o) =>
            {
                o.VariantCallingParameters.MinimumVariantQScore = 50;
                o.VariantCallingParameters.MaximumVariantQScore = 50;
                o.VariantCallingParameters.MinimumVariantQScoreFilter = 50;
            }, true);

            ExecuteValidationTest((o) => { o.BamFilterParameters.MinimumBaseCallQuality = 0; }, true);
            ExecuteValidationTest((o) => { o.BamFilterParameters.MinimumBaseCallQuality = -1; }, false);

            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumFrequency = 0f; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumFrequency = 1f; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumFrequency = -0.99f; }, false);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumFrequency = 1.01f; }, false);

            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumVariantQScoreFilter = o.VariantCallingParameters.MinimumVariantQScore; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumVariantQScoreFilter = o.VariantCallingParameters.MaximumVariantQScore; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumVariantQScoreFilter = o.VariantCallingParameters.MinimumVariantQScore - 1; }, false);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumVariantQScoreFilter = o.VariantCallingParameters.MaximumVariantQScore + 1; }, false);

            ExecuteValidationTest((o) => { o.VariantCallingParameters.ForcedNoiseLevel = 0; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.ForcedNoiseLevel = -50; }, false);

            ExecuteValidationTest((o) => { o.MaxSizeMNV = 0; }, true);  // ok if not calling mnv
            ExecuteValidationTest((o) => { o.MaxGapBetweenMNV = -1; }, true);  // ok if not calling mnv
            ExecuteValidationTest((o) => { o.OutputDirectory = TestPaths.LocalTestDataDirectory; }, true); // True for valid path
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxSizeMNV = 1;
            }, true);
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxSizeMNV = 0;
            }, false);
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxSizeMNV = GlobalConstants.RegionSize;
            }, true);
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxSizeMNV = GlobalConstants.RegionSize + 1;
            }, false);

            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxGapBetweenMNV = 0;
            }, true);
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxGapBetweenMNV = -1;
            }, false);

            ExecuteValidationTest((o) => { o.BamFilterParameters.MinimumMapQuality = 0; }, true);
            ExecuteValidationTest((o) => { o.BamFilterParameters.MinimumMapQuality = -1; }, false);

            ExecuteValidationTest((o) => { o.VariantCallingParameters.StrandBiasAcceptanceCriteria = 0f; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.StrandBiasAcceptanceCriteria = -0.01f; }, false);

            ExecuteValidationTest((o) => { o.MaxNumThreads = 1; }, true);
            ExecuteValidationTest((o) => { o.MaxNumThreads = 0; }, false);

            //FilteredVariantFrequency Scenarios
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumFrequencyFilter = 0; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumFrequencyFilter = 1f; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumFrequencyFilter = -1; o.VariantCallingParameters.MinimumFrequency = -1; }, false);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.MinimumFrequencyFilter = 1.1f; }, false);


            //FilteredLowGenomeQuality Scenarios
            ExecuteValidationTest((o) =>
            {
                o.VariantCallingParameters.PloidyModel = PloidyModel.DiploidByThresholding;
                o.VariantCallingParameters.LowGenotypeQualityFilter = 0;
            }, true);
            ExecuteValidationTest((o) =>
            {
                o.VariantCallingParameters.PloidyModel = PloidyModel.DiploidByThresholding;
                o.VariantCallingParameters.LowGenotypeQualityFilter = 100;
            }, true);
            ExecuteValidationTest((o) =>
            {
                o.VariantCallingParameters.PloidyModel = PloidyModel.DiploidByThresholding;
                o.VariantCallingParameters.LowGenotypeQualityFilter = -1;
            }, false);
            ExecuteValidationTest((o) =>
            {
                o.VariantCallingParameters.PloidyModel = PloidyModel.DiploidByThresholding;
                o.VariantCallingParameters.LowGenotypeQualityFilter = 101;
            }, true);


            ExecuteValidationTest((o) => { o.VariantCallingParameters.LowGenotypeQualityFilter = 0; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.LowGenotypeQualityFilter = 100; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.LowGenotypeQualityFilter = -1; }, false);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.LowGenotypeQualityFilter = 101; }, true);

            //FilteredIndelRepeats Scenarios
            ExecuteValidationTest((o) => { o.VariantCallingParameters.IndelRepeatFilter = 0; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.IndelRepeatFilter = 10; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.IndelRepeatFilter = -1; }, false);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.IndelRepeatFilter = 11; }, false);

            //FilteredLowDepth Scenarios
            ExecuteValidationTest(o =>
            {
                o.VariantCallingParameters.LowDepthFilter = 0;
                o.VariantCallingParameters.MinimumCoverage = 0;
            }, true);
            ExecuteValidationTest(o =>
            {
                o.VariantCallingParameters.LowDepthFilter = 1;
                o.VariantCallingParameters.MinimumCoverage = 0;
            }, true);
            ExecuteValidationTest(o =>
            {
                o.VariantCallingParameters.LowDepthFilter = -1;
                o.VariantCallingParameters.MinimumCoverage = 0;
            }, false);

            //Priors path
            ExecuteValidationTest((o) => { o.PriorsPath = _existingBamPath; }, true);
            ExecuteValidationTest((o) => { o.PriorsPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Nonexistant.txt"); }, false);

            // Thread by chr
            ExecuteValidationTest(o =>
            {
                o.ThreadByChr = true;
                o.ChromosomeFilter = "chr1";
            }, false);

            // Validate ncfilter
            ExecuteValidationTest((o) => { o.VariantCallingParameters.NoCallFilterThreshold = 0f; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.NoCallFilterThreshold = 1f; }, true);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.NoCallFilterThreshold = -1f; }, false);
            ExecuteValidationTest((o) => { o.VariantCallingParameters.NoCallFilterThreshold = 2f; }, false);
        }

        [Fact]
        public void SaveApplicationLegacyOptions()
        {
            var parsingResult = new CommandLineParseResult();

            var bamFolder = TestPaths.SharedBamDirectory;
            var applicationOptionsFile = Path.Combine(TestPaths.LocalScratchDirectory, "SomaticVariantCallerOptions1.used.json");
            if (File.Exists(applicationOptionsFile))
                File.Delete(applicationOptionsFile);

            var commandLine1 = string.Format("-minvq 40 -minbq 40 -BAMpaths {0} -t 1000 -g {1} -vqfilter 40", bamFolder, _existingGenome);
            var optionsParser1 = GetParsedAndValidatedApplicationOptions(commandLine1);

            optionsParser1.Options.Save(applicationOptionsFile);
            Assert.True(File.Exists(applicationOptionsFile));
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


        // If the -OutFolder option uses an invalid folder, execution must end and inform the user of the failure.
        // This could come into play due to permissions issues. Test moved over from FactoryTests
        [Fact]
        [Trait("ReqID", "SDS-26")]
        public void InvalidVcfOutputFolder()
        {

            Assert.False(Directory.Exists("56:\\Illumina\\OutputFolder"));
            var outputFolder = Path.Combine("56:\\Illumina\\OutputFolder");

            string bamChr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr19.bam");
            string bamChr17Chr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam");
            string bamChr17Chr19Dup = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19_removedSQlines.bam");

            string intervalsChr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr19.picard");

            string intervalsChr17 = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17only.picard");

            string genomeChr19 = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");


            var appOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamChr19, bamChr17Chr19, bamChr17Chr19Dup },
                IntervalPaths = new[] { intervalsChr17, intervalsChr19, null },
                GenomePaths = new[] { genomeChr19 },
                OutputDirectory = outputFolder
            };

            var parser = new PiscesOptionsParser() { Options = appOptions };
            Assert.Throws<ArgumentException>(() => parser.ValidateAndSetDerivedValues());

        }

        [Fact]
        public void SaveApplicationOptions()
        {


            var bamFolder = TestPaths.SharedBamDirectory;
            var applicationOptionsFile = Path.Combine(TestPaths.LocalScratchDirectory, "SomaticVariantCallerOptions2.used.json");
            if (File.Exists(applicationOptionsFile))
                File.Delete(applicationOptionsFile);

            var parsingResult = new CommandLineParseResult();
            var commandLine1 = string.Format("-MinVariantQScore 40 -MinBaseCallQuality 40 -BAMPaths {0} -MaxNumThreads 1000 -GenomePaths {1} -VariantQualityFilter 40", bamFolder, _existingGenome);

            var options = GetParsedOptions(commandLine1);

            options.Save(applicationOptionsFile);

            Assert.True(File.Exists(applicationOptionsFile));
        }

        private void ExecuteParsingAndSetDerivedParams(string arguments, Action<PiscesApplicationOptions> assertions = null)
        {
            var parser = GetParsedAndValidatedApplicationOptions(arguments);
            assertions(parser.PiscesOptions);
        }



        private void ExecuteParsingOnlyTest(string arguments, bool shouldPass, Action<PiscesApplicationOptions> assertions = null)
        {
            var parser = GetParsedApplicationOptions(arguments);

            if (shouldPass)
            {
                assertions(parser.PiscesOptions);
            }
            else
            {
                Assert.NotEqual(0, parser.ParsingResult.ExitCode);
            }
        }

        private void ExecuteParsingTest(string arguments, Action<PiscesApplicationOptions> assertions = null)
        {
            var options = GetParsedOptions(arguments);
            assertions(options);
        }

        private PiscesOptionsParser GetParsedApplicationOptions(string arguments)
        {
            var parsedOptions = new PiscesOptionsParser();
            parsedOptions.ParseArgs(arguments.Split(' '), false);
            return parsedOptions;
        }

        private PiscesOptionsParser GetParsedAndValidatedApplicationOptions(string arguments)
        {
            var parsedOptions = new PiscesOptionsParser();
            parsedOptions.ParseArgs(arguments.Split(' '), true);
            return parsedOptions;
        }

        private PiscesApplicationOptions GetParsedOptions(string arguments)
        {
            var parsedOptions = GetParsedApplicationOptions(arguments);
            return parsedOptions.PiscesOptions;
        }


        private void ExecuteValidationTest(Action<PiscesApplicationOptions> testSetup, bool shouldPass)
        {
            var options = GetBasicOptions();
            testSetup(options);
            var parser = new PiscesOptionsParser() { Options = options };

            if (shouldPass)
                parser.ValidateAndSetDerivedValues();
            else
            {
                Assert.Throws<ArgumentException>(() => parser.ValidateAndSetDerivedValues());
            }
        }

        private PiscesApplicationOptions GetBasicOptions()
        {
            var options = new PiscesApplicationOptions()
            {
                BAMPaths = new[] { _existingBamPath },
                GenomePaths = new[] { _existingGenome }
            };

            options.SetIODirectories("Pisces");

            return (options);
        }


        [Fact]
        public void AmpliconBiasFilterParsingTest()
        {
            //happy path. should easily parse.
            var result1 = VariantCallingOptionsParserUtils.ParseAmpliconBiasFilter("0.20", 0.05F);
            Assert.Equal(0.2F, result1);

            //should revert to the default.
            var result2 = VariantCallingOptionsParserUtils.ParseAmpliconBiasFilter("TRUE", 0.01F);
            Assert.Equal(0.01F, result2);

            //should turn off (nullify)
            var result3 = VariantCallingOptionsParserUtils.ParseAmpliconBiasFilter("FALSE", 0.01F);
            Assert.Null(result3);

            //should throw
            Assert.Throws<ArgumentException>(() => (VariantCallingOptionsParserUtils.ParseAmpliconBiasFilter("WontWork!!", 0.01F)));

            //now test it all the way through the parser...

            //happy path
            var parser = GetParsedApplicationOptions("-abfilter 0.99");
            var result4 = ((PiscesApplicationOptions)parser.Options).VariantCallingParameters.AmpliconBiasFilterThreshold;
            Assert.Equal(0.99F, result4);

            //unset (default is now OFF)
            parser = GetParsedApplicationOptions("");
            var result5 = ((PiscesApplicationOptions)parser.Options).VariantCallingParameters.AmpliconBiasFilterThreshold;
            Assert.Null(result5);

            //turned off
            parser = GetParsedApplicationOptions("-abfilter FALSE");
            var result6 = ((PiscesApplicationOptions)parser.Options).VariantCallingParameters.AmpliconBiasFilterThreshold;
            Assert.Null(result6);

            //turned on, + checking cap. invariant . Will turn ON and use default.
            parser = GetParsedApplicationOptions("-aBfilTER true");
            var result7 = ((PiscesApplicationOptions)parser.Options).VariantCallingParameters.AmpliconBiasFilterThreshold;
            Assert.Equal(0.01F, result7);


            //pathological. Make sure the error message involves the argument and the value that cause the problem
            parser = GetParsedApplicationOptions("-abfilter blah");
            var parserResult = parser.ParsingResult;
            Assert.Equal((int)ExitCodeType.BadArguments, parserResult.ExitCode);
            Assert.True(parserResult.Exception.Message.Contains("AmpliconBias"));
            Assert.True(parserResult.Exception.Message.Contains("blah"));
        }

    }
}
