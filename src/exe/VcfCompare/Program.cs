using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Pisces.IO.Sequencing;

namespace VcfCompare
{
    public class ProgramConfigSettings
    {
        public double MarginOfError = 0.00001;
        public string VariantTypes = "Snv,Mnv,Del,Ins";
        public bool Verbose = false;
        public int BlockSize = 5000000;
    }

    class Program
    {
        static void Main(string[] args)
        {
            var baselineVcfPath = args[0];
            var testVcfPath = args[1];

            var comparisonConfig = new VcfComparisonConfig();
            var configurationManager = new ProgramConfigSettings();
            var region = new VcfRegion();

            if (args.Length > 2)
            {
                comparisonConfig.Exact = args.Contains("-Exact");
                comparisonConfig.CheckGT = args.Contains("-GT");
                comparisonConfig.CheckDP = args.Contains("-DP");
                comparisonConfig.CheckVF = args.Contains("-VF");
                comparisonConfig.CheckSB = args.Contains("-SB");
                comparisonConfig.CheckFilter = args.Contains("-Filter");
                comparisonConfig.CheckQual = args.Contains("-Qual");
                comparisonConfig.PassingOnly = !args.Contains("-AllVars");
                comparisonConfig.ConsiderRefs = args.Contains("-Refs");
                comparisonConfig.CheckAD = args.Contains("-AD");
                comparisonConfig.AllCheck(args.Contains("-AllCheck"));
                comparisonConfig.OutputFile = GetParameterValue(args, "Out");
                comparisonConfig.SummaryFile = GetParameterValue(args, "Summary");
                comparisonConfig.HideSharedDiffs = args.Contains("-HideShared");
            }

            region.Chromosome = GetParameterValue(args, "Chrom");
            region.Start = int.Parse(GetParameterValue(args, "Start") ?? "0");
            region.End = int.Parse(GetParameterValue(args, "End") ?? int.MaxValue.ToString());

            var configMargin = configurationManager.MarginOfError;
            if (configMargin < 0)
            {
                comparisonConfig.MarginOfError = configMargin;
            };
            var variantTypes = configurationManager.VariantTypes.Split(',');
            var verbose = configurationManager.Verbose;

            comparisonConfig.BlockSize = configurationManager.BlockSize;

            comparisonConfig.CheckDeletions = variantTypes.Contains("Del");
            comparisonConfig.CheckInsertions = variantTypes.Contains("Ins");
            comparisonConfig.CheckSnv = variantTypes.Contains("Snv");
            comparisonConfig.CheckMnv = variantTypes.Contains("Mnv");

            Console.WriteLine();
            Console.WriteLine(string.Join(" ",args));
            Console.WriteLine("==============================" + Environment.NewLine);
            Console.WriteLine("Variant Types: "+string.Join(",",variantTypes));

            VcfComparer.BaselineVcfs(baselineVcfPath,testVcfPath, comparisonConfig, region, verbose);
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
