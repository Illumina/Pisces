using System.Collections.Generic;
using System.Linq;
using CommandLine.Util;
using Xunit;

namespace Stitcher.Tests
{
    public class ApplicationOptionsTests
    {
        [Fact]
        public void ApplicationOptions_HappyPath()
        {
            var argsDict = new Dictionary<string, string>();

            argsDict["Bam"] = "X";
            argsDict["OutFolder"] = "TestOut";
            argsDict["MinBaseCallQuality"] = "20";
            argsDict["FilterMinMapQuality"] = "1";
            argsDict["FilterDuplicates"] = "True";
            argsDict["FilterForProperPairs"] = "False";
            argsDict["FilterUnstitchablePairs"] = "False";
            argsDict["StitchGappedPairs"] = "False";
            argsDict["UseSoftClippedBases"] = "True";
            argsDict["NifyUnstitchablePairs"] = "True";
            argsDict["IdentifyDuplicates"] = "True";
            argsDict["NifyDisagreement"] = "True";
            argsDict["Debug"] = "True";
            argsDict["LogFileName"] = "True";
            argsDict["ThreadByChr"] = "True";
            argsDict["DebugSummary"] = "True";
            argsDict["StitchProbeSoftclips"] = "True";
            argsDict["NumThreads"] = "5";
            argsDict["SortMemoryGB"] = "18";
            argsDict["MaxReadLength"] = "100";
            argsDict["IgnoreReadsAboveMaxLength"] = "True";

            ValidateOptionParsing(argsDict);


            argsDict["Bam"] = "Z";
            argsDict["OutFolder"] = "TestOut2";
            argsDict["MinBaseCallQuality"] = "200";
            argsDict["FilterMinMapQuality"] = "100";
            argsDict["FilterDuplicates"] = "False";
            argsDict["FilterForProperPairs"] = "True";
            argsDict["FilterUnstitchablePairs"] = "True";
            argsDict["StitchGappedPairs"] = "True";
            argsDict["UseSoftClippedBases"] = "False";
            argsDict["NifyUnstitchablePairs"] = "False";
            argsDict["Debug"] = "False";
            argsDict["LogFileName"] = "False";
            argsDict["ThreadByChr"] = "False";
            argsDict["DebugSummary"] = "False";
            argsDict["StitchProbeSoftclips"] = "False";
            argsDict["NumThreads"] = "55";
            argsDict["SortMemoryGB"] = "180";
            argsDict["MaxReadLength"] = "1000";
            argsDict["ver"] = "";
            argsDict["IgnoreReadsAboveMaxLength"] = "False";

            ValidateOptionParsing(argsDict);

        }

        private void ValidateOptionParsing(Dictionary<string, string> argsDict)
        {
            var args = argsDict.SelectMany(x => new[] { "-" + x.Key, x.Value }).ToArray();
            var parser = new StitcherApplicationOptionsParser();
            parser.ParseArgs(args);

            var options = parser.ProgramOptions;

            ValidateOption(argsDict, "Bam", options.InputBam.ToString());
            ValidateOption(argsDict, "OutFolder", options.OutputDirectory.ToString());
            ValidateOption(argsDict, "MinBaseCallQuality", options.StitcherOptions.MinBaseCallQuality.ToString());
            ValidateOption(argsDict, "MinMapQuality", options.StitcherOptions.FilterMinMapQuality.ToString());
            ValidateOption(argsDict, "FilterPairLowMapQ", options.StitcherOptions.FilterPairLowMapQ.ToString());
            ValidateOption(argsDict, "FilterPairUnmapped", options.StitcherOptions.FilterPairUnmapped.ToString());
            ValidateOption(argsDict, "FilterDuplicates", options.StitcherOptions.FilterDuplicates.ToString());
            ValidateOption(argsDict, "FilterForProperPairs", options.StitcherOptions.FilterForProperPairs.ToString());
            ValidateOption(argsDict, "FilterUnstitchablePairs", options.StitcherOptions.FilterUnstitchablePairs.ToString());
            ValidateOption(argsDict, "StitchGappedPairs", options.StitcherOptions.StitchGappedPairs.ToString());
            ValidateOption(argsDict, "UseSoftClippedBases", options.StitcherOptions.UseSoftClippedBases.ToString());
            ValidateOption(argsDict, "NifyUnstitchablePairs", options.StitcherOptions.NifyUnstitchablePairs.ToString());
            ValidateOption(argsDict, "Debug", options.StitcherOptions.Debug.ToString());
            ValidateOption(argsDict, "LogFileName", options.LogFileNameBase.ToString());
            ValidateOption(argsDict, "ThreadByChr", options.StitcherOptions.ThreadByChromosome.ToString());
            ValidateOption(argsDict, "DebugSummary", options.StitcherOptions.DebugSummary.ToString());
            ValidateOption(argsDict, "StitchProbeSoftclips", options.StitcherOptions.StitchProbeSoftclips.ToString());
            ValidateOption(argsDict, "NumThreads", options.StitcherOptions.NumThreads.ToString());
            ValidateOption(argsDict, "MemoryGB", options.StitcherOptions.SortMemoryGB.ToString());
            ValidateOption(argsDict, "MaxReadLength", options.StitcherOptions.MaxReadLength.ToString());
            ValidateOption(argsDict, "IgnoreReadsAboveMaxLength", options.StitcherOptions.IgnoreReadsAboveMaxLength.ToString());
   
        }

        private void ValidateOption(Dictionary<string, string> argsDict, string optionName, string option)
        {
            if (argsDict.ContainsKey(optionName))
            {
                Assert.Equal(argsDict[optionName], option);
            }
        }


        /// <summary>
        ///The following tests check the new argument handling takes care of the following cases:
        ///(1) No arguments given
        ///(2) Version num requested 
        ///(3) unknown arguments given
        ///(4) missing required input (no vcf given)
        /// </summary>
        [Fact]
        public void CheckCommandLineArgumentHandling_noArguments()
        {
            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { }));

            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-v" }));

            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "--v" }));

            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-vcf", "foo.genome.vcf", "-blah", "won't work" }));

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
                Assert.True(true);
            }
            catch
            {
                Assert.True(false);
            }
        }

    }
}