using System;
using System.Collections.Generic;
using CommandLine.NDesk.Options;

namespace CommandLine.Util
{
    public static class Help
    {
        public static void Show(Dictionary<string, OptionSet> opstionSetList, string commonOptions, string description)
        {
            OutputHelper.WriteLabel("USAGE: ");
            Console.WriteLine("dotnet {0} {1}", OutputHelper.GetExecutableName(), commonOptions);
            Console.WriteLine("{0}\n", description);

            foreach (var ops in opstionSetList)
            {
                OutputHelper.WriteLabel(ops.Key+":");
                Console.WriteLine();
                ops.Value.WriteOptionDescriptions(Console.Out);
                Console.WriteLine();
                Console.WriteLine();
            }
            
        }
    }
}
