using System;
using System.Collections.Generic;
using CommandLine.Util;
using CommandLine.Options;
using Xunit;
using AdaptiveGenotyper;

namespace AdaptiveGenotyper.Tests
{
    public class ApplicationOptionsTests
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
            Action<AdaptiveGtOptions> expectations = null;
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

        private Dictionary<string, Action<AdaptiveGtOptions>> GetLowerCaseOptionsExpectations()
        {
            
            var optionsExpectationsDict = new Dictionary<string, Action<AdaptiveGtOptions>>();

            optionsExpectationsDict.Add("-models my.model", (o) => Assert.Equal("my.model", o.ModelFile));
            optionsExpectationsDict.Add("-vcf test.vcf", (o) => Assert.Equal("test.vcf", o.VcfPath));
            optionsExpectationsDict.Add("-log mylog.txt", (o) => Assert.Equal("mylog.txt", o.LogFileName));
            optionsExpectationsDict.Add("-o myoutdir", (o) => Assert.Equal("myoutdir", o.OutputDirectory));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<AdaptiveGtOptions>> GetExtraHyphenExpectations()
        {

            var optionsExpectationsDict = new Dictionary<string, Action<AdaptiveGtOptions>>();

            optionsExpectationsDict.Add("--models my.model", (o) => Assert.Equal("my.model", o.ModelFile));
            optionsExpectationsDict.Add("--vcf tesT.vcf", (o) => Assert.Equal("tesT.vcf", o.VcfPath));
            optionsExpectationsDict.Add("--log myloG.txt", (o) => Assert.Equal("myloG.txt", o.LogFileName));
            optionsExpectationsDict.Add("--o myoutdir", (o) => Assert.Equal("myoutdir", o.OutputDirectory));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<AdaptiveGtOptions>> GetUpperCaseOptionsExpectations()
        {

            var optionsExpectationsDict = new Dictionary<string, Action<AdaptiveGtOptions>>();

            optionsExpectationsDict.Add("-MODEL my.model", (o) => Assert.Equal("my.model", o.ModelFile));
            optionsExpectationsDict.Add("-VCF teSt2.vcf", (o) => Assert.Equal("teSt2.vcf", o.VcfPath));
            optionsExpectationsDict.Add("-LOG myloG.txt", (o) => Assert.Equal("myloG.txt", o.LogFileName));
            optionsExpectationsDict.Add("-O myoutDir", (o) => Assert.Equal("myoutDir", o.OutputDirectory));
            return optionsExpectationsDict;
        }




        private void ExecuteParsingTest(string arguments, bool shouldPass, Action<AdaptiveGtOptions> assertions = null)
        {
            var parser = new AdaptiveGtOptionsParser();
            parser.ParseArgs(arguments.Split());

            if (shouldPass)
            {
                assertions(parser.AdaptiveGtOptions);
            }
            else //TODO - it would be nice to specify the actual error codes from the parsing result
            {
                Assert.True(parser.ParsingFailed);
                Assert.NotNull(parser.ParsingResult.Exception);
            }
        }


    }
}
