using System;
using System.Collections.Generic;
using System.Linq;

namespace GeminiMulti
{
    public static class MultiProcessHelpers
    {
        public static List<string> GetCommandLineWithoutIgnoredArguments(Dictionary<string, string> optionsUsed, List<string> doNotPassToSubprocess)
        {
            const string argumentPrefixCharacter = "-";
            if (doNotPassToSubprocess.Any(x => x.Contains(argumentPrefixCharacter)))
            {
                throw new ArgumentException("Parameter names should not contain argument prefix characters.");
            }
            var cmdLineList = new List<string>();
            foreach (var kvp in optionsUsed)
            {
                if (!doNotPassToSubprocess.Contains(kvp.Key.Replace(argumentPrefixCharacter, string.Empty), StringComparer.InvariantCultureIgnoreCase))
                {
                    cmdLineList.Add(kvp.Key);
                    cmdLineList.Add($"\"{kvp.Value}\"");
                }
            }

            return cmdLineList;
        }

        public static List<string> GetOrderedChromosomes(Dictionary<string, int> chromRefIds)
        {
            return chromRefIds.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        }
    }
}