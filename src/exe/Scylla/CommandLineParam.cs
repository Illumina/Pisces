using System;
using System.Linq;
using System.IO;
using Pisces.Domain.Utility;
using VariantPhasing;

namespace Scylla
{
    public class CommandLineParameters 
    {
        private const int SpaceForOption = 25;
        public static void PrintVersionToConsole()
        {
            var currentAssemblyName = Common.IO.FileUtilities.LocalAssemblyName<CommandLineParameters>();
            var currentAssemblyVersion  = Common.IO.FileUtilities.LocalAssemblyVersion<CommandLineParameters>();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(currentAssemblyName + " " + currentAssemblyVersion);
            Console.WriteLine(UsageInfoHelper.GetWebsite());
            Console.WriteLine();
            Console.ResetColor();
        }

        public static void PrintUsageInfo()
        {
            PrintVersionToConsole();


            PrintOption("-ver/-v","Print version.");
            PrintOption("-help","Print help.");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Required arguments:");
            Console.ResetColor();

            PrintOption("-vcf","Path to input vcf file.");
            PrintOption("-bam","Path to bam file.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Optional arguments:");
            Console.ResetColor();

            PrintOption("-Dist","How close variants need to be to chain together. Should be less than read length.");
            PrintOption("-Out","Output directory. Default is to return results to vcf path.");
            PrintOption("-PassingVariantsOnly","Whether only passing variants should be allowed to phase, 'true' or 'false'. Default, true");
            PrintOption("-ClusterConstraint","Constrain the number of clusters to this number, if possible. Analogous to forced ploidy.");
            PrintOption("-AllowClusterMerging","Whether clusters should be allowed to merge, 'true' or 'false'. Default, true");
            PrintOption("-AllowWorstFitRemoval","Whether a cluster should try to remove and reassign its worst fit, 'true' or 'false'. Default, true");
            PrintOption("-Chr","Array indicating which chromosomes to process (ie, [chr1,chr9]). If empty, all chromosomes will be processed. Default, empty (all)");
            PrintOption("-MaxNbhdsToProcess","A debug option, an integer cap on the number of neighborhoods to process. If -1, all neighborhoods will be processed. Default, -1 (all)");
            PrintOption("-Debug","Run the program in debug mode (additional logging).");
            PrintOption("-t","Number of threads to use. Default, 10");
            PrintOption("-b","Minimum base call quality. Cigar operations with quality below the minimum will be treated as 'N's. Default, 20");
            PrintOption("-m","Minimum map quality to consider a read. Default, 1");

            Console.WriteLine();

        }

        private static void PrintOption(string option, string description)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            var fillerCount = SpaceForOption - option.Length;
            var filler = new string(' ', fillerCount);
            Console.Write(option + filler);
            Console.ResetColor();
            Console.WriteLine(description);
        }

        public static bool ParseAndValidateCommandLine(string[] arguments, PhasingApplicationOptions options)
        {

            if ((arguments == null) || (arguments.Length == 0))
            {
                PrintUsageInfo();
                return false;
            }

            if (arguments.Contains("-help") || arguments.Contains("-h"))
            {
                PrintUsageInfo();
                return false;
            }



            try
            {
                if (!options.ParseCommandLine(arguments))
                    return false;
            }
            catch (Exception)
            {
                PrintUsageInfo();
                throw;
            }

            if (arguments.Length != 0 && options != null)
            {
                options.SetDerivedvalues();
                Validate(options);
            }

            return true;

        }

    

        public static void Validate(PhasingApplicationOptions options)
        {
            // Check for required fields.
            if (string.IsNullOrEmpty(options.BamPath) || string.IsNullOrEmpty(options.VcfPath))
            {
                throw new ArgumentNullException("Missing required parameters. " +
                                                "Minimal required parameters are: BAM path, VCF path.");
            }

            if (!File.Exists(options.BamPath))
                throw new ArgumentException(string.Format("BAM path '{0}' does not exist.", options.BamPath));

            if (!File.Exists(options.VcfPath))
                throw new ArgumentException(string.Format("VCF path '{0}' does not exist.", options.VcfPath));

            if (string.IsNullOrEmpty(options.OutFolder))
            {
                options.OutFolder = Path.GetDirectoryName(options.VcfPath);

                if (string.IsNullOrEmpty(options.OutFolder))
                {
                    options.OutFolder = Directory.GetCurrentDirectory();//some sensible default
                }
            }
            options.Validate();

        }

    }

}
