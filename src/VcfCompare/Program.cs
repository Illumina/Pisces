using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SequencingFiles;

namespace VcfCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            var baselineVcfPath = args[0];
            var testVcfPath = args[1];

            var config = new VcfComparisonConfig();

            var region = new VcfRegion();

            if (args.Length > 2)
            {
                config.Exact = args.Contains("-Exact");
                config.CheckGT = args.Contains("-GT");
                config.CheckDP = args.Contains("-DP");
                config.CheckVF = args.Contains("-VF");
                config.CheckSB = args.Contains("-SB");
                config.CheckFilter = args.Contains("-Filter");
                config.CheckQual = args.Contains("-Qual");
                config.PassingOnly = !args.Contains("-AllVars");
                config.ConsiderRefs = args.Contains("-Refs");
                config.CheckAD = args.Contains("-AD");
                config.AllCheck(args.Contains("-AllCheck"));
                config.OutputFile = GetParameterValue(args, "Out");
                config.SummaryFile = GetParameterValue(args, "Summary");
                config.HideSharedDiffs = args.Contains("-HideShared");
            }

            region.Chromosome = GetParameterValue(args, "Chrom");
            region.Start = int.Parse(GetParameterValue(args, "Start") ?? "0");
            region.End = int.Parse(GetParameterValue(args, "End") ?? int.MaxValue.ToString());

            var configMargin = ConfigurationManager.AppSettings["MarginOfError"];
            if (configMargin != "N/A")
            {
                config.MarginOfError = float.Parse(configMargin);
            };
            var variantTypes = ConfigurationManager.AppSettings["VariantTypes"].Split(',');
            var verbose = bool.Parse(ConfigurationManager.AppSettings["Verbose"]);

            config.BlockSize = int.Parse(ConfigurationManager.AppSettings["BlockSize"]);

            config.CheckDeletions = variantTypes.Contains("Del");
            config.CheckInsertions = variantTypes.Contains("Ins");
            config.CheckSnv = variantTypes.Contains("Snv");
            config.CheckMnv = variantTypes.Contains("Mnv");

            Console.WriteLine();
            Console.WriteLine(string.Join(" ",args));
            Console.WriteLine("==============================" + Environment.NewLine);
            Console.WriteLine("Variant Types: "+string.Join(",",variantTypes));

            VcfComparer.BaselineVcfs(baselineVcfPath,testVcfPath, config, region, verbose);
        }

        private static string GetParameterValue(string[] args, string argName)
        {
            if (args.Any(a=>a.StartsWith("--"+argName+"=")))
            {
                return args.First(a => a.StartsWith("--" + argName + "=")).Split('=')[1];
            }
            return null;
        }

        public enum VariantType
        {
            Snv,
            Mnv,
            Insertion,
            Deletion
        }
    }

    public class Logger
    {
        private readonly bool _verbose;

        public Logger(bool verbose)
        {
            _verbose = verbose;
        }

        public void Log(string message)
        {
            if (_verbose) Console.WriteLine(message);
        }
    }
}
