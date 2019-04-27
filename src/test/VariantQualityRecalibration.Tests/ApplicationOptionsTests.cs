using System;
using System.Collections.Generic;
using CommandLine.Util;
using CommandLine.Options;
using Xunit;

namespace VariantQualityRecalibration.Tests
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
            Action<VQROptions> expectations = null;
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

        private Dictionary<string, Action<VQROptions>> GetLowerCaseOptionsExpectations()
        {
            /*
            Console.WriteLine("Required arguments:");
            Console.WriteLine("-vcf imput file name ");
            Console.WriteLine("Optional arguments:");
            Console.WriteLine("-o output directory");
            Console.WriteLine("-log log file name");
            Console.WriteLine("-b baseline noise level, default 20. (The new noise level is never recalibrated to lower than this.)");
            Console.WriteLine("-z thresholding parameter, default 2 (How many std devs above averge observed noise will the algorithm tolerate, before deciding a mutation type is likely to be artifact ) ");
            Console.WriteLine("-f filter Q score, default 30 (if a variant gets recalibrated, when we apply the \"LowQ\" filter)");
            Console.WriteLine("-Q max Q score, default 100 (if a variant gets recalibrated, when we cap the new Q score");
            */

            var optionsExpectationsDict = new Dictionary<string, Action<VQROptions>>();

            optionsExpectationsDict.Add("-b 35", (o) => Assert.Equal(35, o.BamFilterParams.MinimumBaseCallQuality));
            optionsExpectationsDict.Add("-f 22", (o) => Assert.Equal(22, o.VariantCallingParams.MinimumVariantQScoreFilter));
            optionsExpectationsDict.Add("-vcf test.vcf", (o) => Assert.Equal("test.vcf", o.VcfPath));
            optionsExpectationsDict.Add("-log mylog.txt", (o) => Assert.Equal("mylog.txt", o.LogFileNameBase));
            optionsExpectationsDict.Add("-q 1000", (o) => Assert.Equal(1000, o.MaxQScore));
            optionsExpectationsDict.Add("-o myoutdir", (o) => Assert.Equal("myoutdir", o.OutputDirectory));
            optionsExpectationsDict.Add("-z 42", (o) => Assert.Equal(42, o.ZFactor));
            optionsExpectationsDict.Add("-locicount 4200", (o) => Assert.Equal(4200, o.LociCount));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<VQROptions>> GetExtraHyphenExpectations()
        {
           
            var optionsExpectationsDict = new Dictionary<string, Action<VQROptions>>();

            optionsExpectationsDict.Add("--b 31", (o) => Assert.Equal(31, o.BamFilterParams.MinimumBaseCallQuality));
            optionsExpectationsDict.Add("--f 29", (o) => Assert.Equal(29, o.VariantCallingParams.MinimumVariantQScoreFilter));
            optionsExpectationsDict.Add("--vcf tesT.vcf", (o) => Assert.Equal("tesT.vcf", o.VcfPath));
            optionsExpectationsDict.Add("--log myloG.txt", (o) => Assert.Equal("myloG.txt", o.LogFileNameBase));
            optionsExpectationsDict.Add("--q 1003", (o) => Assert.Equal(1003, o.MaxQScore));
            optionsExpectationsDict.Add("--o myoutdir", (o) => Assert.Equal("myoutdir", o.OutputDirectory));
            optionsExpectationsDict.Add("--z 47", (o) => Assert.Equal(47, o.ZFactor));
            optionsExpectationsDict.Add("--locicount 4200", (o) => Assert.Equal(4200, o.LociCount));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<VQROptions>> GetUpperCaseOptionsExpectations()
        {
           
            var optionsExpectationsDict = new Dictionary<string, Action<VQROptions>>();

            optionsExpectationsDict.Add("-B 350", (o) => Assert.Equal(350, o.BamFilterParams.MinimumBaseCallQuality));
            optionsExpectationsDict.Add("-F 220", (o) => Assert.Equal(220, o.VariantCallingParams.MinimumVariantQScoreFilter));
            optionsExpectationsDict.Add("-VCF teSt2.vcf", (o) => Assert.Equal("teSt2.vcf", o.VcfPath));
            optionsExpectationsDict.Add("-LOG myloG.txt", (o) => Assert.Equal("myloG.txt", o.LogFileNameBase));
            optionsExpectationsDict.Add("-Q 2000", (o) => Assert.Equal(2000, o.MaxQScore));
            optionsExpectationsDict.Add("-O myoutDir", (o) => Assert.Equal("myoutDir", o.OutputDirectory));
            optionsExpectationsDict.Add("-Z 43", (o) => Assert.Equal(43, o.ZFactor));
            optionsExpectationsDict.Add("-lociCount 4203", (o) => Assert.Equal(4203, o.LociCount));
            return optionsExpectationsDict;
        }




        private void ExecuteParsingTest(string arguments, bool shouldPass, Action<VQROptions> assertions = null)
        {
           var parser = new VQROptionsParser();
            parser.ParseArgs(arguments.Split());
        
            if (shouldPass)
            {
                assertions(parser.VQROptions);
            }
            else //TODO - it would be nice to specify the actual error codes from the parsing result
            {
                Assert.True(parser.ParsingFailed);
                Assert.NotNull(parser.ParsingResult.Exception);
            }
        }

       
    }
}
