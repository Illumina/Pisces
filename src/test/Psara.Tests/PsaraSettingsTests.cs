using System;
using System.IO;
using System.Collections.Generic;
using CommandLine.IO.Utilities;
using Xunit;

namespace Psara.Tests
{
    public class PsaraSettingsTests
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
            Action<PsaraOptions> expectations = null;
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

        private Dictionary<string, Action<PsaraOptions>> GetLowerCaseOptionsExpectations()
        {
            //"vcf=",
            // "o|out|outfolder=",
            //"log=",
            //"ROI=",
            //InclusionModel = value

            var optionsExpectationsDict = new Dictionary<string, Action<PsaraOptions>>();

            optionsExpectationsDict.Add("-vcf My.vcf", (o) => Assert.Equal("My.vcf", o.InputVcf));
            optionsExpectationsDict.Add("-out outDir", (o) => Assert.Equal("outDir", o.OutputDirectory));
            optionsExpectationsDict.Add("-log My.log", (o) => Assert.Equal(Path.Combine("outDir","PsaraLogs","My.log"), o.LogFileName));
            optionsExpectationsDict.Add("-roi myROI.txt", (o) => Assert.Equal("myROI.txt", o.GeometricFilterParameters.RegionOfInterestPath));
            optionsExpectationsDict.Add("-inclusionmodel StaRt", (o) => Assert.Equal(GeometricFilterParameters.InclusionModel.ByStartPosition, o.GeometricFilterParameters.InclusionStrategy));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<PsaraOptions>> GetExtraHyphenExpectations()
        {

            var optionsExpectationsDict = new Dictionary<string, Action<PsaraOptions>>();

            optionsExpectationsDict.Add("--VCF My.vcf", (o) => Assert.Equal("My.vcf", o.InputVcf));
            optionsExpectationsDict.Add("--OutFolder outDir", (o) => Assert.Equal("outDir", o.OutputDirectory));
            optionsExpectationsDict.Add("--Log My.log", (o) => Assert.Equal(Path.Combine("outDir", "PsaraLogs", "My.log"), o.LogFileName));
            optionsExpectationsDict.Add("--ROI myROI.txt", (o) => Assert.Equal("myROI.txt", o.GeometricFilterParameters.RegionOfInterestPath));
            optionsExpectationsDict.Add("--inclusionModel ExpanD", (o) => Assert.Equal(GeometricFilterParameters.InclusionModel.Expanded, o.GeometricFilterParameters.InclusionStrategy));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<PsaraOptions>> GetUpperCaseOptionsExpectations()
        {

            var optionsExpectationsDict = new Dictionary<string, Action<PsaraOptions>>();

            optionsExpectationsDict.Add("-VCF My.vcf", (o) => Assert.Equal("My.vcf", o.InputVcf));
            optionsExpectationsDict.Add("-OutFolder outDir", (o) => Assert.Equal("outDir", o.OutputDirectory));
            optionsExpectationsDict.Add("-Log My.log", (o) => Assert.Equal(Path.Combine("outDir", "PsaraLogs", "My.log"), o.LogFileName));
            optionsExpectationsDict.Add("-ROI myROI.txt", (o) => Assert.Equal("myROI.txt", o.GeometricFilterParameters.RegionOfInterestPath));
            optionsExpectationsDict.Add("-InclusionModel ExpanD", (o) => Assert.Equal(GeometricFilterParameters.InclusionModel.Expanded, o.GeometricFilterParameters.InclusionStrategy));
            return optionsExpectationsDict;
        }




        public static void ExecuteParsingTest(string arguments, bool shouldPass, Action<PsaraOptions> assertions = null)
        {
            var ApplicationOptionParser = new PsaraOptionsParser();
           ApplicationOptionParser.ParseArgs(arguments.Split(' '));
           var Options = (ApplicationOptionParser).PsaraOptions;
            
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
}
