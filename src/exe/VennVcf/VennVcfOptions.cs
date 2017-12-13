using System;
using Pisces.IO.Sequencing;
using Pisces.Domain.Options;

namespace VennVcf
{
    public class VennVcfOptions : VcfProcessorOptions
    {
        #region Members
        public string[] InputFiles;
        public string OutputDirectory = "";
        public string ConsensusFileName = "";
        public SampleAggregationParameters SampleAggregationParameters = new SampleAggregationParameters();
        public string CommandLine;
        public bool DebugMode;

        #endregion

        public static void PrintUsageInfo()
        {
            Console.WriteLine("Example use:");
            Console.WriteLine("VennVcf.exe -if [A.genome.vcf,B.genome.vcf] -o outdir -consensus myConsensus2.gvcf -Mfirst true");
        }

        public override bool ParseCommandLine(string[] arguments)
        {
            CommandLine = "##VennVcf_cmdline=\"" + string.Join(" ", arguments) + "\"";

            var usedOptions = BamFilterParams.Parse(arguments);
            usedOptions.AddRange(VariantCallingParams.Parse(arguments));
            usedOptions.AddRange(VcfWritingParams.Parse(arguments));


            int argumentIndex = 0;
            while (argumentIndex < arguments.Length)
            {
                if (arguments[argumentIndex] == null || arguments[argumentIndex].Length == 0)
                {
                    argumentIndex++;
                    continue;
                }
                string Value = null;
                if (argumentIndex < arguments.Length - 1) Value = arguments[argumentIndex + 1];
                switch (arguments[argumentIndex])
                {
                    case "-if":
                        InputFiles = OptionHelpers.ListOfParamsToStringArray(Value);
                        if (InputFiles.Length < 1)
                        {
                            Console.WriteLine("Error: Need at least one input vcf '{0}'", Value);
                            return false;
                        }
                        argumentIndex += 2;
                        break;

                    case "-out":
                    case "-o":
                        OutputDirectory = Value;
                        argumentIndex += 2;
                        break;
                    case "-consensus":
                        ConsensusFileName = Value;
                        argumentIndex += 2;
                        break;
                    case "-Mfirst":
                        VcfWritingParams.MitochondrialChrComesFirst = bool.Parse(Value);
                        argumentIndex += 2;
                        break;
                    case "-debug":
                    case "-Debug":
                        DebugMode = bool.Parse(Value);
                        argumentIndex += 2;
                        break;
                    default:
                        if (!(usedOptions.Contains(arguments[argumentIndex])))
                        {
                            Console.WriteLine(string.Format("Unknown argument '{0}'. Continuing without it.", arguments[argumentIndex]));
                            return false;
                        }
                        argumentIndex += 2;
                        break;
                }
            }

            if (InputFiles == null)
            {
                Console.WriteLine("Error: no input vcf files or dirs");
                return false;
            }

            return true;
        }
    }
}
