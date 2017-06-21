using System;
using System.Collections.Generic;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class ApplicationOptionsTests
    {
        [Fact]
        public void PrintOptionsTest()
        {
            try
            {
                ApplicationOptions.PrintUsageInfo();
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
            var optionExpectations = GetLowerCaseOptionsExpectations();
            Action<ApplicationOptions> expectations = null;
            foreach (var option in optionExpectations.Values)
            {
                expectations += option;
            }

            ExecuteParsingTest(string.Join(" ", optionExpectations.Keys), true, expectations);

            optionExpectations = GetUpperCaseOptionsExpectations();
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

        private Dictionary<string, Action<ApplicationOptions>> GetLowerCaseOptionsExpectations()
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

            var optionsExpectationsDict = new Dictionary<string, Action<ApplicationOptions>>();

            optionsExpectationsDict.Add("-b 35", (o) => Assert.Equal(35, o.BaseQNoise));
            optionsExpectationsDict.Add("-f 22", (o) => Assert.Equal(22, o.FilterQScore));
            optionsExpectationsDict.Add("-vcf test.vcf", (o) => Assert.Equal("test.vcf", o.InputVcf));
            optionsExpectationsDict.Add("-log mylog.txt", (o) => Assert.Equal("mylog.txt", o.LogFileName));
            optionsExpectationsDict.Add("-q 1000", (o) => Assert.Equal(1000, o.MaxQScore));
            optionsExpectationsDict.Add("-o myoutdir", (o) => Assert.Equal("myoutdir", o.OutputDirectory));
            optionsExpectationsDict.Add("-z 42", (o) => Assert.Equal(42, o.ZFactor));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<ApplicationOptions>> GetUpperCaseOptionsExpectations()
        {
           
            var optionsExpectationsDict = new Dictionary<string, Action<ApplicationOptions>>();

            optionsExpectationsDict.Add("-B 350", (o) => Assert.Equal(350, o.BaseQNoise));
            optionsExpectationsDict.Add("-F 220", (o) => Assert.Equal(220, o.FilterQScore));
            optionsExpectationsDict.Add("-VCF test2.vcf", (o) => Assert.Equal("test2.vcf", o.InputVcf));
            optionsExpectationsDict.Add("-LOG myloG.txt", (o) => Assert.Equal("myloG.txt", o.LogFileName));
            optionsExpectationsDict.Add("-Q 2000", (o) => Assert.Equal(2000, o.MaxQScore));
            optionsExpectationsDict.Add("-O myoutDir", (o) => Assert.Equal("myoutDir", o.OutputDirectory));
            optionsExpectationsDict.Add("-Z 43", (o) => Assert.Equal(43, o.ZFactor));
            return optionsExpectationsDict;
        }


        private void ExecuteParsingTest(string arguments, bool shouldPass, Action<ApplicationOptions> assertions = null)
        {
            var options = ApplicationOptions.ParseCommandLine(arguments.Split(' '));

            if (shouldPass)
            {               
                if (assertions != null)
                    assertions(options);
            }
            else
            {
                Assert.Null(options);
            }
        }
    }
}
