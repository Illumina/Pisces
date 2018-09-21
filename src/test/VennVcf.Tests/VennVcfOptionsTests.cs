using System;
using System.Collections.Generic;
using CommandLine.Util;
using Xunit;

namespace VennVcf.Tests
{
    public class VennVcfOptionsTests
    {

        [Fact]
        public void PrintOptionsTest()
        {

            // "help|h" should disply help. At least check it doesnt crash.
            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-h" }));
            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "--h" }));
            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-Help" }));
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

            ExecuteParsingOnlyTest(string.Join(" ", optionExpectations.Keys), true, expectations);

         
        }

        private void ExecuteParsingOnlyTest(string arguments, bool shouldPass, Action<VennVcfOptions> assertions = null)
        {
            var parser = new VennVcfOptionsParser();

            //skip validation, we dont care if the input files are real or not.
            //We are strictly testing parsing.
            parser.ParseArgs(arguments.Split(), false); 

            if (shouldPass)
            {
               assertions(parser.VennOptions);
               Assert.True(parser.HadSuccess);
            }
            else //TODO - it would be nice to specify the actual error codes from the parsing result
            {
                Assert.True(parser.ParsingFailed);
                Assert.NotNull(parser.ParsingResult.Exception);
            }
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

    }
}