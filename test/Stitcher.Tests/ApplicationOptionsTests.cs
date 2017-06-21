using System.Collections.Generic;
using System.Linq;
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

            ValidateOptionParsing(argsDict);

        }

        private void ValidateOptionParsing(Dictionary<string, string> argsDict)
        {
            var args = argsDict.SelectMany(x => new[] { "-" + x.Key, x.Value }).ToArray();

            var options = new ApplicationOptions(args);

            ValidateOption(argsDict, "Bam", options.InputBam.ToString());
            ValidateOption(argsDict, "OutFolder", options.OutFolder.ToString());
            ValidateOption(argsDict, "MinBaseCallQuality", options.StitcherOptions.MinBaseCallQuality.ToString());
            ValidateOption(argsDict, "FilterMinMapQuality", options.StitcherOptions.FilterMinMapQuality.ToString());
            ValidateOption(argsDict, "FilterDuplicates", options.StitcherOptions.FilterDuplicates.ToString());
            ValidateOption(argsDict, "FilterForProperPairs", options.StitcherOptions.FilterForProperPairs.ToString());
            ValidateOption(argsDict, "FilterUnstitchablePairs", options.StitcherOptions.FilterUnstitchablePairs.ToString());
            ValidateOption(argsDict, "StitchGappedPairs", options.StitcherOptions.StitchGappedPairs.ToString());
            ValidateOption(argsDict, "UseSoftClippedBases", options.StitcherOptions.UseSoftClippedBases.ToString());
            ValidateOption(argsDict, "NifyUnstitchablePairs", options.StitcherOptions.NifyUnstitchablePairs.ToString());
            ValidateOption(argsDict, "Debug", options.StitcherOptions.Debug.ToString());
            ValidateOption(argsDict, "LogFileName", options.StitcherOptions.LogFileName.ToString());
            ValidateOption(argsDict, "ThreadByChr", options.StitcherOptions.ThreadByChromosome.ToString());
            ValidateOption(argsDict, "DebugSummary", options.StitcherOptions.DebugSummary.ToString());
            ValidateOption(argsDict, "StitchProbeSoftclips", options.StitcherOptions.StitchProbeSoftclips.ToString());
            ValidateOption(argsDict, "NumThreads", options.StitcherOptions.NumThreads.ToString());
            ValidateOption(argsDict, "MemoryGB", options.StitcherOptions.SortMemoryGB.ToString());
            ValidateOption(argsDict, "MaxReadLength", options.StitcherOptions.MaxReadLength.ToString());
            Assert.Equal(argsDict.ContainsKey("ver"), options.ShowVersion);
        }

        private void ValidateOption(Dictionary<string, string> argsDict, string optionName, string option)
        {
            if (argsDict.ContainsKey(optionName))
            {
                Assert.Equal(argsDict[optionName], option);
            }
        }

    }
}