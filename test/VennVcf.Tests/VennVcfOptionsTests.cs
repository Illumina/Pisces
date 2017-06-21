using System;
using System.Collections.Generic;
using Xunit;

namespace VennVcf.Tests
{
    public class VennVcfOptionsTests
    {
  
        [Fact]
        public void PrintOptionsTest()
        {
            try
            {
                VennVcfOptions.PrintUsageInfo();
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
            var optionExpectations = GetOptionsExpectations();
            Action<VennVcfOptions> expectations = null;
            foreach (var option in optionExpectations.Values)
            {
                expectations += option;
            }

            ExecuteParsingTest(string.Join(" ", optionExpectations.Keys), true, expectations);

         
        }

     

        private Dictionary<string, Action<VennVcfOptions>> GetOptionsExpectations()
        {
            
            var optionsExpectationsDict = new Dictionary<string, Action<VennVcfOptions>>();

            optionsExpectationsDict.Add("-if [vcfA,vcfB]", (o) => Assert.Equal(new string[] { "vcfA", "vcfB" }, o.InputFiles));
            optionsExpectationsDict.Add("-out myOutDir", (o) => Assert.Equal("myOutDir", o.OutputDirectory));
            optionsExpectationsDict.Add("-debug true", (o) => Assert.Equal(true, o.DebugMode));
            optionsExpectationsDict.Add("-minbq 10", (o) => Assert.Equal(10, o.BamFilterParams.MinimumBaseCallQuality));
            return optionsExpectationsDict;
        }

        private void ExecuteParsingTest(string arguments, bool shouldPass, Action<VennVcfOptions> assertions = null)
        {
            var options = new VennVcfOptions();
            options.ParseCommandLine(arguments.Split(' '));

            if (shouldPass)
            {
                assertions(options);
            }
            else
            {
                Assert.Null(options);
            }
        }
    }
}