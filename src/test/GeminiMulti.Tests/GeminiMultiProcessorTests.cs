using System;
using System.Collections.Generic;
using CommandLine.Util;
using Gemini.Interfaces;
using Gemini.Types;
using Pisces.Processing.Utility;
using Moq;
using Xunit;

namespace GeminiMulti.Tests
{
    public class GeminiMultiApplicationOptionsTests
    {

        [Fact]
        public void PrintOptionsTest()
        {
            // "help|h" should disply help. At least check it doesnt crash.

            try
            {
                Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-h" }));
                Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "--h" }));
                Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-Help" }));
                Assert.True(true);
            }
            catch
            {
                Assert.True(false);
            }
        }

        [Fact]
        public void SetOptionsTest()
        {
            //test with lower case arguments
            var optionExpectations = GetLowerCaseOptionsExpectations();
            Action<GeminiMultiApplicationOptions> expectations = null;
            foreach (var option in optionExpectations.Values)
            {
                expectations += option;
            }

            ExecuteParsingTest(string.Join(" ", optionExpectations.Keys), true, expectations);

            //test with upper case arguments
            optionExpectations = GetUpperCaseOptionsExpectations();
            expectations = null;
            foreach (var option in optionExpectations.Values)
            {
                expectations += option;
            }

            ExecuteParsingTest(string.Join(" ", optionExpectations.Keys), true, expectations);

            //test with "--" hyphen case
            optionExpectations = GetExtraHyphenExpectations();
            expectations = null;
            foreach (var option in optionExpectations.Values)
            {
                expectations += option;
            }

            ExecuteParsingTest(string.Join(" ", optionExpectations.Keys), true, expectations);

        }

        [Fact]
        public void FailParsingTest()
        {
            ExecuteParsingTest("-balh ", false);
            ExecuteParsingTest("", false);
            ExecuteParsingTest("help me", false);
        }

        private Dictionary<string, Action<GeminiMultiApplicationOptions>> GetLowerCaseOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<GeminiMultiApplicationOptions>>();

            optionsExpectationsDict.Add("-bam input.bam", (o) => Assert.Equal("input.bam", o.InputBam));
            optionsExpectationsDict.Add("-genome mygenome", (o) => Assert.Equal("mygenome", o.GeminiOptions.GenomePath));
            optionsExpectationsDict.Add("-samtools samtools.exe", (o) => Assert.Equal("samtools.exe", o.GeminiOptions.SamtoolsPath));
            optionsExpectationsDict.Add("-outFolder my/out", (o) => Assert.Equal("my/out", o.OutputDirectory));

            optionsExpectationsDict.Add("-minBaseCallQuality 1", (o) => Assert.Equal(1, o.StitcherOptions.MinBaseCallQuality));
            //optionsExpectationsDict.Add("-filterPairUnmapped true", (o) => Assert.True(o.StitcherOptions.FilterPairUnmapped));
            optionsExpectationsDict.Add("-filterForProperPairs true", (o) => Assert.True(o.StitcherOptions.FilterForProperPairs));
            optionsExpectationsDict.Add("-nifyDisagreement true", (o) => Assert.Equal(true, o.StitcherOptions.NifyDisagreements));
            optionsExpectationsDict.Add("-maxReadLength 8024", (o) => Assert.Equal(8024, o.StitcherOptions.MaxReadLength));
            optionsExpectationsDict.Add("-dontStitchRepeatOverlap false", (o) => Assert.False(o.StitcherOptions.DontStitchHomopolymerBridge));
            optionsExpectationsDict.Add("-ignoreReadsAboveMaxLength true", (o) => Assert.True(o.StitcherOptions.IgnoreReadsAboveMaxLength));

            //optionsExpectationsDict.Add("-maxIndelSize 200", (o) => Assert.Equal(200, o.RealignmentOptions.MaxIndelSize)); // No longer exposing on command line (TODO maybe add to Gemini)
            //optionsExpectationsDict.Add("-allowRescoringOrigZero true", (o) => Assert.True(o.RealignmentOptions.AllowRescoringOrigZero));
            optionsExpectationsDict.Add("-maskPartialInsertion true", (o) => Assert.True(o.RealignmentOptions.MaskPartialInsertion));
            optionsExpectationsDict.Add("-minimumUnanchoredInsertionLength 20", (o) => Assert.Equal(20, o.RealignmentOptions.MinimumUnanchoredInsertionLength));

            optionsExpectationsDict.Add("-minPreferredSupport 20", (o) => Assert.Equal(20, o.IndelFilteringOptions.FoundThreshold));
            optionsExpectationsDict.Add("-minPreferredAnchor 2", (o) => Assert.Equal((int)2, o.IndelFilteringOptions.MinAnchor));
            optionsExpectationsDict.Add("-minRequiredIndelSupport 3", (o) => Assert.Equal(3, o.IndelFilteringOptions.StrictFoundThreshold));
            optionsExpectationsDict.Add("-minRequiredAnchor 1", (o) => Assert.Equal(1, o.IndelFilteringOptions.StrictAnchorThreshold));
            optionsExpectationsDict.Add("-maxMessThreshold 30", (o) => Assert.Equal(30, o.IndelFilteringOptions.MaxMess));
            optionsExpectationsDict.Add("-binSize 5", (o) => Assert.Equal(5, o.IndelFilteringOptions.BinSize));

            optionsExpectationsDict.Add("-keepBothSideSoftclips true", (o) => Assert.True(o.GeminiOptions.KeepBothSideSoftclips));
            optionsExpectationsDict.Add("-keepProbe true", (o) => Assert.True(o.GeminiOptions.KeepProbeSoftclip));
            optionsExpectationsDict.Add("-trustSoftclips true", (o) => Assert.True(o.GeminiOptions.TrustSoftclips));

            optionsExpectationsDict.Add("-checkSoftclipsForMismatches true", (o) => Assert.True(o.RealignmentAssessmentOptions.CheckSoftclipsForMismatches));
            optionsExpectationsDict.Add("-trackMismatches true", (o) => Assert.True(o.RealignmentAssessmentOptions.TrackActualMismatches));

            optionsExpectationsDict.Add("-categoriesToRealign Disagree,UnstitchIndel,ImperfectStitched", (o) => Assert.Equal(new List<PairClassification>() { PairClassification.Disagree, PairClassification.UnstitchIndel, PairClassification.ImperfectStitched }, o.RealignmentOptions.CategoriesForRealignment));
            optionsExpectationsDict.Add("-categoriesToSnowball Disagree,UnstitchIndel", (o) => Assert.Equal(new List<PairClassification>() { PairClassification.Disagree, PairClassification.UnstitchIndel }, o.RealignmentOptions.CategoriesForSnowballing));
            optionsExpectationsDict.Add("-pairAwareEverything true", (o) => Assert.True(o.RealignmentOptions.PairAwareEverything));

            optionsExpectationsDict.Add("-samtoolsOldStyle true", (o) => Assert.True(o.GeminiOptions.IsWeirdSamtools));

            optionsExpectationsDict.Add("-logregionsandrealignments true", (o) => Assert.True(o.GeminiOptions.LogRegionsAndRealignments));
            optionsExpectationsDict.Add("-silencesuspiciousmdreads true", (o) => Assert.True(o.GeminiOptions.SilenceSuspiciousMdReads));
            optionsExpectationsDict.Add("-silencedirectionalmessreads true", (o) => Assert.True(o.GeminiOptions.SilenceDirectionalMessReads));
            optionsExpectationsDict.Add("-silencemessymapmessreads true", (o) => Assert.True(o.GeminiOptions.SilenceMessyMapMessReads));
            optionsExpectationsDict.Add("-recalculateusablesitesaftersnowball false", (o) => Assert.False(o.GeminiOptions.RecalculateUsableSitesAfterSnowball));

            optionsExpectationsDict.Add("-messysitewidth 200", (o) => Assert.Equal(200, o.GeminiOptions.MessySiteWidth));
            optionsExpectationsDict.Add("-messysitethreshold 2", (o) => Assert.Equal(2, o.GeminiOptions.MessySiteThreshold));
            optionsExpectationsDict.Add("-messymapq 40", (o) => Assert.Equal(40, o.GeminiOptions.MessyMapq));
            optionsExpectationsDict.Add("-numsoftclipstobeconsideredmessy 4", (o) => Assert.Equal(4, o.GeminiOptions.NumSoftclipsToBeConsideredMessy));
            optionsExpectationsDict.Add("-nummismatchestobeconsideredmessy 6", (o) => Assert.Equal(6, o.GeminiOptions.NumMismatchesToBeConsideredMessy));
            optionsExpectationsDict.Add("-imperfectfreqthreshold 0.2", (o) => Assert.Equal(0.2f, o.GeminiOptions.ImperfectFreqThreshold));
            optionsExpectationsDict.Add("-indelregionfreqthreshold 0.1", (o) => Assert.Equal(0.1f, o.GeminiOptions.IndelRegionFreqThreshold));
            optionsExpectationsDict.Add("-regiondepththreshold 10", (o) => Assert.Equal(10, o.GeminiOptions.RegionDepthThreshold));


            // TODO add test for defaults?

            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<GeminiMultiApplicationOptions>> GetExtraHyphenExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<GeminiMultiApplicationOptions>>();

            optionsExpectationsDict.Add("--bam input.bam", (o) => Assert.Equal("input.bam", o.InputBam));
            optionsExpectationsDict.Add("--genome mygenome", (o) => Assert.Equal("mygenome", o.GeminiOptions.GenomePath));
            optionsExpectationsDict.Add("--samtools samtools.exe", (o) => Assert.Equal("samtools.exe", o.GeminiOptions.SamtoolsPath));
            optionsExpectationsDict.Add("--outFolder my/out", (o) => Assert.Equal("my/out", o.OutputDirectory));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<GeminiMultiApplicationOptions>> GetUpperCaseOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<GeminiMultiApplicationOptions>>();

            optionsExpectationsDict.Add("-BAM input.bam", (o) => Assert.Equal("input.bam", o.InputBam));
            optionsExpectationsDict.Add("-Genome mygenome", (o) => Assert.Equal("mygenome", o.GeminiOptions.GenomePath));
            optionsExpectationsDict.Add("-SamTools samtools.exe", (o) => Assert.Equal("samtools.exe", o.GeminiOptions.SamtoolsPath));
            optionsExpectationsDict.Add("-OutFolder my/out", (o) => Assert.Equal("my/out", o.OutputDirectory));
            return optionsExpectationsDict;
        }




        private static void ExecuteParsingTest(string arguments, bool shouldPass, Action<GeminiMultiApplicationOptions> assertions = null)
        {
            var ApplicationOptionParser = new GeminiMultiApplicationOptionsParser();
            ApplicationOptionParser.ParseArgs(arguments.Split(' '));
            var Options = (ApplicationOptionParser).ProgramOptions;

            if (shouldPass)
            {
                assertions(Options);
            }
            else //TODO - it would be nice to specify the actual error codes from the parsing result
            {

                int ErrorCode = ApplicationOptionParser.ParsingResult.ExitCode;
                Assert.NotEqual(0, ApplicationOptionParser.ParsingResult.ExitCode);
                Assert.NotNull(ApplicationOptionParser.ParsingResult.Exception);
            }
        }
    }

    public class GeminiMultiProcessorTests
    {
        [Fact]
        public void Execute()
        {
            var options = new GeminiMultiApplicationOptions(){OutputDirectory = "Output", ExePath = "myexe"};

            var mockSamtools = new Mock<ISamtoolsWrapper>();
            var mockCliTaskManager = new Mock<ICliTaskManager>();
            var executedTasks = new List<ICliTask>();
            var cliTaskCreator = new Mock<CliTaskCreator>();

            var mockTask1 = new Mock<ICliTask>();
            var mockTask2 = new Mock<ICliTask>();
            mockTask1.SetupGet(x => x.ExitCode).Returns(0);
            mockTask2.SetupGet(x => x.ExitCode).Returns(0);

            var tasks = new Dictionary<int,ICliTask>() {{1,mockTask1.Object}, {3,mockTask2.Object}};
            var commandsPassed = new List<IEnumerable<string>>();

            cliTaskCreator.Setup(x => x.GetCliTask(It.IsAny<string[]>(),
              It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
              .Returns<string[], string, string, string, int>((args, chrom, e, outDir, chrRefId) => tasks[chrRefId]).Callback<string[], string, string, string, int>(
                  (args, chrom, e, outDir, chrRefId) => { commandsPassed.Add(args); });



            mockCliTaskManager.Setup(x => x.Process(It.IsAny<List<ICliTask>>()))
                .Callback<List<ICliTask>>(x =>
                {
                    foreach (var task in x)
                    {
                        executedTasks.Add(task);
                    }
                });

            var chroms = new Dictionary<string, int>()
            {
                {"chr1", 1},
                {"chr3", 3}
            };

            var processor = new GeminiMultiProcessor(options, cliTaskCreator.Object);

            var cmdLineList = new List<string>() { "--command", "line", "-options", "tada" };
            processor.Execute(mockCliTaskManager.Object, chroms, cmdLineList, mockSamtools.Object);
            Assert.Equal(2, executedTasks.Count);
        }
    }
}
