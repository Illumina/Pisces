using System;
using System.Collections.Generic;
using Xunit;

namespace GeminiMulti.Tests
{
    public class MultiProcessHelpersTests
    {
        [Fact]
        public void GetOrderedChromosomes()
        {
            var refIdLookup = new Dictionary<string, int>(){{"chr1",1},{"chrM",0},{"chr3",3},{"chrY",24}};
            Assert.Equal(new List<string>(){"chrM","chr1","chr3","chrY"}, MultiProcessHelpers.GetOrderedChromosomes(refIdLookup));
        }

        [Fact]
        public void GetCommandLineWithoutIgnoredArguments()
        {
            var optionsUsed = new Dictionary<string, string>() {{"--option1", "blah"}, {"-option2", "blah2"}, { "-option1b", "blah1b" }};
            var filteredCmd =
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed, new List<string>());
            Assert.Equal(6, filteredCmd.Count);

            // Remove option2
            filteredCmd =
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed, 
                    new List<string>(){"option2"});
            Assert.Equal(4, filteredCmd.Count);
            Assert.Equal("--option1 \"blah\" -option1b \"blah1b\"", string.Join(" ", filteredCmd));

            // Remove option2 - demonstrate case insensitivity
            filteredCmd =
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed,
                    new List<string>() { "OPtioN2" });
            Assert.Equal(4, filteredCmd.Count);
            Assert.Equal("--option1 \"blah\" -option1b \"blah1b\"", string.Join(" ", filteredCmd));

            // Remove option1 - demonstrate that it works with single dash and double, and that option1 being substring of option1b is fine
            filteredCmd =
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed,
                    new List<string>() { "option1" });
            Assert.Equal(4, filteredCmd.Count);
            Assert.Equal("-option2 \"blah2\" -option1b \"blah1b\"", string.Join(" ", filteredCmd));

            // Remove option1b - demonstrate that it works with single dash and double, and that option1 being substring of option1b is fine
            filteredCmd =
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed,
                    new List<string>() { "option1b" });
            Assert.Equal(4, filteredCmd.Count);
            Assert.Equal("--option1 \"blah\" -option2 \"blah2\"", string.Join(" ", filteredCmd));

            // Remove multiple
            filteredCmd =
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed,
                    new List<string>() { "option1b", "option2" });
            Assert.Equal(2, filteredCmd.Count);
            Assert.Equal("--option1 \"blah\"", string.Join(" ", filteredCmd));

            // Remove all
            filteredCmd =
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed,
                    new List<string>() { "option1b", "option2", "option1" });
            Assert.Equal(0.0, filteredCmd.Count);

            // Ignore stuff that wasn't there to begin with
            filteredCmd =
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed,
                    new List<string>() { "option1b", "option2", "optionC", "optionD" });
            Assert.Equal(2, filteredCmd.Count);
            Assert.Equal("--option1 \"blah\"", string.Join(" ", filteredCmd));

            // Throw exception if we're giving junk for argument names to ignore
            Assert.Throws<ArgumentException>(()=>
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed,
                    new List<string>() { "option1-notallowed"}));

            var optionsUsedHasSpace = new Dictionary<string, string>() { { "--option1", "blah has space" }, { "-option2", "blah2_nospace" } };
            filteredCmd =
                MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsedHasSpace, new List<string>());
            Assert.Equal(4, filteredCmd.Count);
            Assert.Equal("--option1 \"blah has space\" -option2 \"blah2_nospace\"", String.Join(" ", filteredCmd));

        }
    }
}