using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.Types;
using TestUtilities;
using Pisces.Calculators;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Tests.ApplicationOptionsTests
{
    public class ApplicationOptionsTests
    {
        private string _existingBamPath = Path.Combine(UnitTestPaths.TestDataDirectory, "Chr17Chr19.bam");
        private string _existingBamPath2 = Path.Combine(UnitTestPaths.TestDataDirectory, "Chr17Chr19_removedSQlines.bam");
        private string _existingGenome = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr19");
        private string _existingInterval = Path.Combine(UnitTestPaths.TestDataDirectory, "chr17only.picard");


        private Dictionary<string, Action<ApplicationOptions>> GetOriginalOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<ApplicationOptions>>();

            optionsExpectationsDict.Add("-minvq 40", (o) => Assert.Equal(40, o.MinimumVariantQScore));
            optionsExpectationsDict.Add("-minbq 41", (o) => Assert.Equal(41, o.MinimumBaseCallQuality));
            optionsExpectationsDict.Add(@"-B C:\test.bam,C:\test2.bam", (o) => Assert.Equal(2, o.BAMPaths.Length));
            optionsExpectationsDict.Add("-c 4", (o) => Assert.Equal(4, o.MinimumDepth));
            optionsExpectationsDict.Add("-d true", (o) => Assert.True(o.DebugMode));
            optionsExpectationsDict.Add("-debug true", (o) => Assert.True(o.DebugMode));
            optionsExpectationsDict.Add("-minvf 0.555", (o) => Assert.Equal(0.555f, o.MinimumFrequency));
            optionsExpectationsDict.Add("-vqfilter 45", (o) => Assert.Equal(45, o.FilteredVariantQScore));
            optionsExpectationsDict.Add("-ssfilter true", (o) => Assert.True(o.FilterOutVariantsPresentOnlyOneStrand));
            optionsExpectationsDict.Add(@"-g C:\genome,C:\genome2", (o) => Assert.Equal(2, o.GenomePaths.Length));
            optionsExpectationsDict.Add("-NL 40", (o) => Assert.Equal(40, o.AppliedNoiseLevel));
            optionsExpectationsDict.Add("-gVCF true", (o) => Assert.True(o.OutputgVCFFiles));
            optionsExpectationsDict.Add("-CallMNVs true", (o) => Assert.True(o.CallMNVs));
            optionsExpectationsDict.Add("-MaxMNVLength 40", (o) => Assert.Equal(40, o.MaxSizeMNV));
            optionsExpectationsDict.Add("-MaxGapBetweenMNV 40", (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            optionsExpectationsDict.Add(@"-i C:\blah,C:\blah2", (o) => Assert.Equal(2, o.IntervalPaths.Length));
            optionsExpectationsDict.Add("-minmq 40", (o) => Assert.Equal(40, o.MinimumMapQuality));
            optionsExpectationsDict.Add("-SBModel poisson", (o) => Assert.Equal(StrandBiasModel.Poisson, o.StrandBiasModel));
            optionsExpectationsDict.Add("-outputsbfiles true", (o) => Assert.True(o.OutputBiasFiles));
            optionsExpectationsDict.Add("-pp true", (o) => Assert.True(o.OnlyUseProperPairs));
            optionsExpectationsDict.Add("-maxvq 400", (o) => Assert.Equal(400, o.MaximumVariantQScore));
            optionsExpectationsDict.Add("-sbfilter 0.7", (o) => Assert.Equal(0.7f, o.StrandBiasAcceptanceCriteria));
            optionsExpectationsDict.Add("-t 40", (o) => Assert.Equal(40, o.MaxNumThreads));
            optionsExpectationsDict.Add("-ReportNoCalls true", (o) => Assert.True(o.ReportNoCalls));
            optionsExpectationsDict.Add(@"-OutFolder C:\out", (o) => Assert.Equal(@"C:\out", o.OutputFolder));
            optionsExpectationsDict.Add("-ThreadByChr true", (o) => Assert.Equal(true, o.ThreadByChr));
            optionsExpectationsDict.Add("-Mono bleh", (o) => Assert.Equal("bleh", o.MonoPath));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<ApplicationOptions>> GetLongHandOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<ApplicationOptions>>();

            optionsExpectationsDict.Add("-MinVariantQScore 40", (o) => Assert.Equal(40, o.MinimumVariantQScore));
            optionsExpectationsDict.Add("-MinBaseCallQuality 40", (o) => Assert.Equal(40, o.MinimumBaseCallQuality));
            optionsExpectationsDict.Add(@"-BamPaths C:\test.bam,C:\test2.bam", (o) => Assert.Equal(2, o.BAMPaths.Length));
            optionsExpectationsDict.Add("-MinDp 40", (o) => Assert.Equal(40, o.MinimumDepth));
            optionsExpectationsDict.Add("-debug true", (o) => Assert.True(o.DebugMode));
            optionsExpectationsDict.Add("-MinimumFrequency 0.555", (o) => Assert.Equal(0.555f, o.MinimumFrequency));
            optionsExpectationsDict.Add("-VariantQualityFilter 40", (o) => Assert.Equal(40, o.FilteredVariantQScore));
            optionsExpectationsDict.Add("-EnableSingleStrandFilter true", (o) => Assert.True(o.FilterOutVariantsPresentOnlyOneStrand));
            optionsExpectationsDict.Add(@"-GenomePaths C:\genome,C:\genome2", (o) => Assert.Equal(2, o.GenomePaths.Length));
            optionsExpectationsDict.Add("-NoiseLevelForQModel 40", (o) => Assert.Equal(40, o.AppliedNoiseLevel));
            optionsExpectationsDict.Add("-gVCF true", (o) => Assert.True(o.OutputgVCFFiles));
            optionsExpectationsDict.Add("-CallMNVs true", (o) => Assert.True(o.CallMNVs));
            optionsExpectationsDict.Add("-MaxMNVLength 40", (o) => Assert.Equal(40, o.MaxSizeMNV));
            optionsExpectationsDict.Add("-MaxGapBetweenMNV 40", (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            optionsExpectationsDict.Add(@"-IntervalPaths C:\blah,C:\blah2", (o) => Assert.Equal(2, o.IntervalPaths.Length));
            optionsExpectationsDict.Add("-MinMapQuality 40", (o) => Assert.Equal(40, o.MinimumMapQuality));
            optionsExpectationsDict.Add("-SBModel poisson", (o) => Assert.Equal(StrandBiasModel.Poisson, o.StrandBiasModel));
            optionsExpectationsDict.Add("-OutputSBFiles true", (o) => Assert.True(o.OutputBiasFiles));
            optionsExpectationsDict.Add("-OnlyUseProperPairs true", (o) => Assert.True(o.OnlyUseProperPairs));
            optionsExpectationsDict.Add("-MaxVariantQScore 1000", (o) => Assert.Equal(1000, o.MaximumVariantQScore));
            optionsExpectationsDict.Add("-MaxAcceptableStrandBiasFilter 0.7", (o) => Assert.Equal(0.7f, o.StrandBiasAcceptanceCriteria));
            optionsExpectationsDict.Add("-MaxNumThreads 10", (o) => Assert.Equal(10, o.MaxNumThreads));
            optionsExpectationsDict.Add("-ReportNoCalls true", (o) => Assert.True(o.ReportNoCalls));
            optionsExpectationsDict.Add(@"-OutFolder C:\out", (o) => Assert.Equal(@"C:\out", o.OutputFolder));
            optionsExpectationsDict.Add("-ThreadByChr true", (o) => Assert.Equal(true, o.ThreadByChr));
            optionsExpectationsDict.Add("-InsideSubProcess true", (o) => Assert.Equal(true, o.InsideSubProcess));
            optionsExpectationsDict.Add("-Mono bleh", (o) => Assert.Equal("bleh", o.MonoPath));
            return optionsExpectationsDict;
        }


        [Fact]
        [Trait("ReqID","SDS-1")]
        public void CommandLineWhitespaceParse()
        {
            var shortHandOptionsExpectations = GetOriginalOptionsExpectations();
            Action<ApplicationOptions> shortHandExpectations = null;
            foreach (var option in shortHandOptionsExpectations.Values)
            {
                shortHandExpectations += option;
            }

            var longHandOptionsExpectations = GetLongHandOptionsExpectations();
            Action<ApplicationOptions> longHandExpectations = null;
            foreach (var option in longHandOptionsExpectations.Values)
            {
                longHandExpectations += option;
            }

            //Test with multiple options strung together by spaces.
            ExecuteParsingTest(string.Join(" ", longHandOptionsExpectations.Keys), true, longHandExpectations);
            ExecuteParsingTest(string.Join(" ", shortHandOptionsExpectations.Keys), true, shortHandExpectations);
           
            //Different separator shouldn't work
            ExecuteParsingTest(string.Join(";", longHandOptionsExpectations.Keys), false, longHandExpectations);
           
            //Order shouldn't matter
            ExecuteParsingTest(string.Join(" ", longHandOptionsExpectations.Keys.OrderByDescending(o=>o)), true, longHandExpectations);
            ExecuteParsingTest(string.Join(" ", longHandOptionsExpectations.Keys.OrderBy(o => o)), true, longHandExpectations);

        }


        [Fact]
        [Trait("ReqID","SDS-2")]
        public void CommandLineParsing()
        {

            //TODO ensure that everything here is in line item in SDS-2 and vice versa
            // make sure arguments get mapped to the right fields
            ExecuteParsingTest("-minvq 40", true, (o) => Assert.Equal(40, o.MinimumVariantQScore));
            ExecuteParsingTest("-minbq 40", true, (o) => Assert.Equal(40, o.MinimumBaseCallQuality));
            ExecuteParsingTest(@"-B C:\test.bam,C:\test2.bam", true, (o) => Assert.Equal(2, o.BAMPaths.Length));
            ExecuteParsingTest("-mindp 40", true, (o) => Assert.Equal(40, o.MinimumDepth));
            ExecuteParsingTest("-d true", true, (o) => Assert.True(o.DebugMode));
            ExecuteParsingTest("-debug true", true, (o) => Assert.True(o.DebugMode));
            ExecuteParsingTest("-minvf 0.555", true, (o) => Assert.Equal(0.555f, o.MinimumFrequency));
            ExecuteParsingTest("-vqfilter 40", true, (o) => Assert.Equal(40, o.FilteredVariantQScore));
            ExecuteParsingTest("-ssfilter true", true, (o) => Assert.True(o.FilterOutVariantsPresentOnlyOneStrand));
            ExecuteParsingTest(@"-g C:\genome,C:\genome2", true, (o) => Assert.Equal(2, o.GenomePaths.Length));
            ExecuteParsingTest("-NL 40", true, (o) => Assert.Equal(40, o.AppliedNoiseLevel));
            ExecuteParsingTest("-gVCF true", true, (o) => Assert.True(o.OutputgVCFFiles));
            ExecuteParsingTest("-CallMNVs true", true, (o) => Assert.True(o.CallMNVs));          
            ExecuteParsingTest("-MaxMNVLength 40", true, (o) => Assert.Equal(40, o.MaxSizeMNV));
            ExecuteParsingTest("-MaxGapBetweenMNV 40", true, (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            ExecuteParsingTest(@"-i C:\blah,C:\blah2", true, (o) => Assert.Equal(2, o.IntervalPaths.Length));
            ExecuteParsingTest("-minmq 40", true, (o) => Assert.Equal(40, o.MinimumMapQuality));
            ExecuteParsingTest("-SBModel poisson", true, (o) => Assert.Equal(StrandBiasModel.Poisson, o.StrandBiasModel));
            ExecuteParsingTest("-SBModel extended", true, (o) => Assert.Equal(StrandBiasModel.Extended, o.StrandBiasModel));
            ExecuteParsingTest("-SBModel random", false);
            ExecuteParsingTest("-outputsbfiles true", true, (o) => Assert.True(o.OutputBiasFiles));
            ExecuteParsingTest("-pp true", true, (o) => Assert.True(o.OnlyUseProperPairs));
            ExecuteParsingTest("-maxvq 40", true, (o) => Assert.Equal(40, o.MaximumVariantQScore));
            ExecuteParsingTest("-sbfilter 0.7", true, (o) => Assert.Equal(0.7f, o.StrandBiasAcceptanceCriteria));
            ExecuteParsingTest("-t 40", true, (o) => Assert.Equal(40, o.MaxNumThreads));
            ExecuteParsingTest("-ReportNoCalls true", true, (o) => Assert.True(o.ReportNoCalls));
            ExecuteParsingTest(@"-OutFolder C:\out", true, (o) => Assert.Equal(@"C:\out", o.OutputFolder));
            ExecuteParsingTest(@"-BAMFolder C:\bamfolder", true, (o) => Assert.Equal(@"C:\bamfolder", o.BAMFolder));
            ExecuteParsingTest(@"-vffilter 20.1", true, (o) => Assert.Equal(20.1f, o.FilteredVariantFrequency));
            ExecuteParsingTest(@"-ploidy diploid", true, (o) => Assert.Equal(PloidyModel.Diploid, o.PloidyModel));
            ExecuteParsingTest(@"-RepeatFilter 5", true, (o) => Assert.Equal(5, o.IndelRepeatFilter));
            ExecuteParsingTest(@"-mindpfilter 3", true, (o) => Assert.Equal(3, o.LowDepthFilter));
            ExecuteParsingTest(@"-Collapse true", true, (o) => Assert.True(o.Collapse));
            ExecuteParsingTest(@"-Collapse false", true, (o) => Assert.False(o.Collapse));
            ExecuteParsingTest(@"  -PriorsPath C:\path", true, (o) => Assert.Equal(@"C:\path", o.PriorsPath));
            ExecuteParsingTest(@"  -DiploidGenotypeParameters 0.10,0.20,0.78", true, (o) => 
                    Assert.True(
                        (0.10f == o.DiploidThresholdingParameters.MinorVF) &&
                        (0.20f == o.DiploidThresholdingParameters.MajorVF) &&
                        (0.78f == o.DiploidThresholdingParameters.SumVFforMultiAllelicSite)));
            ExecuteParsingTest(@"-RMxNFilter false", true, (o) => Assert.True(
                        (null == o.RMxNFilterMaxLengthRepeat) &&
                        (null == o.RMxNFilterMinRepetitions)));
            ExecuteParsingTest(@"-RMxNFilter true", true, (o) => Assert.True(
                        (5 == o.RMxNFilterMaxLengthRepeat) &&
                        (9 == o.RMxNFilterMinRepetitions)));
            ExecuteParsingTest(@"-RMxNFilter 11,3", true, (o) => Assert.True(
                        (11 == o.RMxNFilterMaxLengthRepeat) &&
                        (3 == o.RMxNFilterMinRepetitions)));
            ExecuteParsingTest(@"-RMxNFilter 11,3,0.30", true, (o) => Assert.True(
                (11 == o.RMxNFilterMaxLengthRepeat) &&
                (3 == o.RMxNFilterMinRepetitions) &&
                (0.3f == o.RMxNFilterFrequencyLimit)));
            ExecuteParsingTest(@"-RMxNFilter 11,3,5,0.20", false);
            ExecuteParsingTest(@"-RMxNFilter 5", false);
            ExecuteParsingTest(@"-RMxNFilter yourmom", false);
            ExecuteParsingTest(@"-ThreadByChr true", true, (o) => Assert.True(o.ThreadByChr));
            ExecuteParsingTest(@"-ThreadByChr boo", false);
            ExecuteParsingTest(@"-Mono boo", true, (o) => Assert.Equal(@"boo", o.MonoPath));
            ExecuteParsingTest(@"-mono boo2", true, (o) => Assert.Equal(@"boo2", o.MonoPath));
            ExecuteParsingTest(@"-SkipNonIntervalAlignments meh", false);
        }

        [Fact]
        [Trait("ReqID", "SDS-3")]
        public void CommandLineParsing_Errors()
        {
            // focus on mal-formed arguments

            // argument mismatches
            ExecuteParsingTest("-a", false);
            ExecuteParsingTest("-a 20 -b", false);
            ExecuteParsingTest("a 20", false);
            ExecuteParsingTest("-unknown", false);  

            // enum values
            ExecuteParsingTest("-ploid help", false);
            ExecuteParsingTest("-SBModel bogus", false);  
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
                ExecuteValidationTest((o) => { o.BAMPaths = new string[] {}; }, false);
                //1 bams - should be ok
                ExecuteValidationTest((o) => { o.BAMPaths = new string[] {_existingBamPath}; }, true);

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

                //97 bams - should error
                ExecuteValidationTest((o) => { o.BAMPaths = bams.ToArray(); }, false);

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
        [Trait("ReqID","SDS-3")]
        [Trait("ReqID", "SDS-56")]
        public void Validate()
        {
            // ---------------------
            // verify default should be valid
            // ---------------------
            var option = GetBasicOptions();
            option.Validate();
            
            // ---------------------
            // verify log folder
            // ---------------------
            Assert.Equal(Path.Combine(UnitTestPaths.TestDataDirectory, "PiscesLogs"), option.LogFolder);
            Assert.Throws<Exception>(() => new ApplicationOptions().LogFolder);

            // ---------------------------------------------------
            // Either BAMPath(s) or BAMFolder should be specified.
            // ---------------------------------------------------
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new []{"bampath1.bam"};
                o.BAMFolder = @"C:\BAMFolder";
            }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = null; }, false);

            // ---------------------------------------------------
            // BAMFolder
            // Folder should exist
            // 1 Genome Path should be specified when BAMFolder is specified.
            // Atmost 1 Interval Path should be specified when BAMFolder is specified.
            // Threading by chromosome is not supported when BAMFolder is specified.
            // ---------------------------------------------------
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = null;
                o.BAMFolder = @"C:\NonexistantBAMFolder";
                o.GenomePaths = new[] {_existingGenome};
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = null;
                o.BAMFolder = UnitTestPaths.TestDataDirectory;
                o.GenomePaths = new[] { "folder1","folder2" };
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = null;
                o.BAMFolder = UnitTestPaths.TestDataDirectory;
                o.GenomePaths = new[] { _existingGenome };
                o.IntervalPaths = new[] {"file1.intervals", "file2.intervals"};
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = null;
                o.BAMFolder = UnitTestPaths.TestDataDirectory;
                o.GenomePaths = new[] { _existingGenome };
                o.IntervalPaths = new[] { _existingInterval };
            }, true);

            // ---------------------
            // BAM Paths
            // Duplicate BAMPaths detected.
            // BAM Path does not exist.
            // ---------------------
            ExecuteValidationTest((o) => { o.BAMPaths = new string[0]; }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = new[] {_existingBamPath, _existingBamPath}; }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = new[] { "nonexistant.bam" }; }, false);

            // genome paths
            ExecuteValidationTest((o) => { o.GenomePaths = null; }, false);
            ExecuteValidationTest((o) => { o.GenomePaths = new string[0]; }, false);
            ExecuteValidationTest((o) => { o.GenomePaths = new[] { _existingGenome, _existingGenome }; }, false);
            ExecuteValidationTest((o) => { o.GenomePaths = new[] { "nonexistant" }; }, false);
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new[] {_existingBamPath, _existingBamPath2};
                o.GenomePaths = new[] {_existingGenome, _existingGenome};
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
            ExecuteValidationTest((o) => { o.MinimumVariantQScore = 0; }, true);
            ExecuteValidationTest((o) =>
            {
                o.MinimumVariantQScore = 100;
                o.FilteredVariantQScore = 100;
            }, true);
            ExecuteValidationTest((o) => { o.MinimumVariantQScore = -1; }, false);
            ExecuteValidationTest((o) => { o.MinimumVariantQScore = 101; }, false);

            ExecuteValidationTest((o) => { o.MaximumVariantQScore = 0;
                                             o.MinimumVariantQScore = 0;
                                             o.FilteredVariantQScore = 0;
            }, true);
            ExecuteValidationTest((o) => { o.MaximumVariantQScore = 100; }, true);
            ExecuteValidationTest((o) => { o.MaximumVariantQScore = -1; }, false);
            ExecuteValidationTest((o) => { o.MaximumVariantQScore = 101; }, true);

            ExecuteValidationTest((o) =>
            {
                o.MinimumVariantQScore = 50;
                o.MaximumVariantQScore = 49;
            }, false);

            ExecuteValidationTest((o) =>
            {
                o.MinimumVariantQScore = 50;
                o.MaximumVariantQScore = 50;
                o.FilteredVariantQScore = 50;
            }, true);

            ExecuteValidationTest((o) => { o.MinimumBaseCallQuality = 0; }, true);
            ExecuteValidationTest((o) => { o.MinimumBaseCallQuality = -1; }, false);

            ExecuteValidationTest((o) => { o.MinimumFrequency = 0f; }, true);
            ExecuteValidationTest((o) => { o.MinimumFrequency = 1f; }, true);
            ExecuteValidationTest((o) => { o.MinimumFrequency = -0.99f; }, false);
            ExecuteValidationTest((o) => { o.MinimumFrequency = 1.01f; }, false);

            ExecuteValidationTest((o) => { o.FilteredVariantQScore = o.MinimumVariantQScore; }, true);
            ExecuteValidationTest((o) => { o.FilteredVariantQScore = o.MaximumVariantQScore; }, true);
            ExecuteValidationTest((o) => { o.FilteredVariantQScore = o.MinimumVariantQScore - 1; }, false);
            ExecuteValidationTest((o) => { o.FilteredVariantQScore = o.MaximumVariantQScore + 1; }, false);

            ExecuteValidationTest((o) => { o.AppliedNoiseLevel = 0; }, true);
            ExecuteValidationTest((o) => { o.AppliedNoiseLevel = -50; }, false);

            ExecuteValidationTest((o) => { o.MaxSizeMNV = 0; }, true);  // ok if not calling mnv
            ExecuteValidationTest((o) => { o.MaxGapBetweenMNV = -1; }, true);  // ok if not calling mnv
            ExecuteValidationTest((o) => { o.OutputFolder = UnitTestPaths.TestDataDirectory; }, true); // True for valid path
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

            ExecuteValidationTest((o) => { o.MinimumMapQuality = 0; }, true);
            ExecuteValidationTest((o) => { o.MinimumMapQuality = -1; }, false);

            ExecuteValidationTest((o) => { o.StrandBiasAcceptanceCriteria = 0f; }, true);
            ExecuteValidationTest((o) => { o.StrandBiasAcceptanceCriteria = -0.01f; }, false);

            ExecuteValidationTest((o) => { o.MaxNumThreads = 1; }, true);
            ExecuteValidationTest((o) => { o.MaxNumThreads =  0; }, false);

            //FilteredVariantFrequency Scenarios
            ExecuteValidationTest((o) => { o.FilteredVariantFrequency = 0; }, true);
            ExecuteValidationTest((o) => { o.FilteredVariantFrequency = 1f; }, true);
            ExecuteValidationTest((o) => { o.FilteredVariantFrequency = -1; }, false);
            ExecuteValidationTest((o) => { o.FilteredVariantFrequency = 1.1f; }, false);

          
         //FilteredLowGenomeQuality Scenarios
         ExecuteValidationTest((o) => {
             o.PloidyModel = PloidyModel.Diploid;
             o.LowGenotypeQualityFilter = 0;
         }, true);
         ExecuteValidationTest((o) => {
             o.PloidyModel= PloidyModel.Diploid;
             o.LowGenotypeQualityFilter = 100;
         }, true);
         ExecuteValidationTest((o) => {
             o.PloidyModel = PloidyModel.Diploid;
             o.LowGenotypeQualityFilter = -1;
         }, false);
         ExecuteValidationTest((o) => {
             o.PloidyModel = PloidyModel.Diploid;
             o.LowGenotypeQualityFilter = 101;
         }, true);

        
         ExecuteValidationTest((o) => { o.LowGenotypeQualityFilter = 0; }, true);
         ExecuteValidationTest((o) => { o.LowGenotypeQualityFilter = 100; }, true);
         ExecuteValidationTest((o) => { o.LowGenotypeQualityFilter = -1; }, false);
         ExecuteValidationTest((o) => { o.LowGenotypeQualityFilter = 101; }, true);
        
            //FilteredIndelRepeats Scenarios
            ExecuteValidationTest((o) => { o.IndelRepeatFilter = 0; }, true);
            ExecuteValidationTest((o) => { o.IndelRepeatFilter = 10; }, true);
            ExecuteValidationTest((o) => { o.IndelRepeatFilter = -1; }, false);
            ExecuteValidationTest((o) => { o.IndelRepeatFilter = 11; }, false);

            //FilteredLowDepth Scenarios
            ExecuteValidationTest(o =>
            {
                o.LowDepthFilter = 0;
                o.MinimumDepth = 0;
            }, true);
            ExecuteValidationTest(o =>
            {
                o.LowDepthFilter = 1;
                o.MinimumDepth = 0;
            }, true);
            ExecuteValidationTest(o =>
            {
                o.LowDepthFilter = -1;
                o.MinimumDepth = 0;
            }, false);

            //Priors path
            ExecuteValidationTest((o) => { o.PriorsPath = Path.Combine(UnitTestPaths.TestDataDirectory, "small_s1.bam"); }, true);
            ExecuteValidationTest((o) => { o.PriorsPath = Path.Combine(UnitTestPaths.TestDataDirectory, "Nonexistant.txt"); }, false);

            // Thread by chr
            ExecuteValidationTest(o =>
            {
                o.ThreadByChr = true;
                o.ChromosomeFilter = "chr1";
            }, false);
        }

        [Fact]
        public void PopulateBAMPaths()
        {
            //Happy Path
            var options_1 = new ApplicationOptions()
            {
                BAMFolder = UnitTestPaths.TestDataDirectory,
                GenomePaths = new[] {_existingGenome},
                IntervalPaths = new[] {_existingInterval}
            };
            options_1.PopulateBAMPaths();
            Assert.NotNull(options_1.BAMPaths);
            Assert.True(options_1.BAMPaths.Length > 0);
            
            //no bam files found
            var options_3 = new ApplicationOptions()
            {
                BAMFolder = UnitTestPaths.TestGenomesDirectory,
                GenomePaths = new[] { _existingGenome },
            };
            Assert.Throws<ArgumentException>(() => options_3.PopulateBAMPaths());
        }

        [Fact]
        public void MaxNumberThreads()
        {
            string bamFolder = UnitTestPaths.TestDataDirectory;
            var commandLine1 = string.Format("-minvq 40 -minbq 40 -BAMFolder {0} -t 1000 -g {1} -vqfilter 40", bamFolder, _existingGenome);
            var options1 = ApplicationOptions.ParseCommandLine(commandLine1.Split(' '));
            Assert.Equal(Environment.ProcessorCount, options1.MaxNumThreads);

            var commandLine2 = string.Format("-minvq 40 -minbq 40 -BAMFolder {0} -g {1} -vqfilter 40", bamFolder, _existingGenome);
            var options2 = ApplicationOptions.ParseCommandLine(commandLine2.Split(' '));
            Assert.Equal(Environment.ProcessorCount, options2.MaxNumThreads);

            var commandLine3 = string.Format("-minvq 40 -minbq 40 -BAMFolder {0} -g {1} -vqfilter 40 -t 1", bamFolder, _existingGenome);
            var options3 = ApplicationOptions.ParseCommandLine(commandLine3.Split(' '));
            Assert.Equal(1, options3.MaxNumThreads);
        }

        [Fact]
        public void PrintAndSaveApplicationLegacyOptions()
        {
            ApplicationOptions.PrintUsageInfo();
            var bamFolder = UnitTestPaths.TestDataDirectory;
            var applicationOptionsFile = Path.Combine(UnitTestPaths.TestDataDirectory, "SomaticVariantCallerOptions.used.xml");
            if(File.Exists(applicationOptionsFile))
                File.Delete(applicationOptionsFile);
            var commandLine1 = string.Format("-minvq 40 -minbq 40 -BAMFolder {0} -t 1000 -g {1} -vqfilter 40", bamFolder, _existingGenome);
            var options1 = ApplicationOptions.ParseCommandLine(commandLine1.Split(' '));
            options1.Save(applicationOptionsFile);
            Assert.True(File.Exists(applicationOptionsFile));
        }

        [Fact]
        public void PrintAndSaveApplicationOptions()
        {
            ApplicationOptions.PrintUsageInfo();
            var bamFolder = UnitTestPaths.TestDataDirectory;
            var applicationOptionsFile = Path.Combine(UnitTestPaths.TestDataDirectory, "SomaticVariantCallerOptions.used.xml");
            if (File.Exists(applicationOptionsFile))
                File.Delete(applicationOptionsFile);
            var commandLine1 = string.Format("-MinVariantQScore 40 -MinBaseCallQuality 40 -BAMFolder {0} -MaxNumThreads 1000 -GenomePaths {1} -VariantQualityFilter 40", bamFolder, _existingGenome);
            var options1 = ApplicationOptions.ParseCommandLine(commandLine1.Split(' '));
            options1.Save(applicationOptionsFile);
            Assert.True(File.Exists(applicationOptionsFile));
        }

        private void ExecuteParsingTest(string arguments, bool shouldPass, Action<ApplicationOptions> assertions = null)
        {
            var options = new ApplicationOptions();

            if (shouldPass)
            {
                options.UpdateOptions(arguments.Split(' '));
                if (assertions != null)
                    assertions(options);
            }
            else
            {
                Assert.Throws<Exception>(() => options.UpdateOptions(arguments.Split(' ')));
            }
        }

        private void ExecuteValidationTest(Action<ApplicationOptions> testSetup, bool shouldPass)
        {
            var options = GetBasicOptions();

            testSetup(options);

            if (shouldPass)
                options.Validate();
            else
            {
                Assert.Throws<ArgumentException>(() => options.Validate());
            }
        }

        private ApplicationOptions GetBasicOptions()
        {
            return new ApplicationOptions()
            {
                BAMPaths = new[] {_existingBamPath},
                GenomePaths = new[] {_existingGenome}
            };
        }
    }
}
