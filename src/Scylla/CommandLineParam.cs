using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Pisces.Domain.Options;
using Pisces.Domain.Utility;
using Pisces.Domain.Types;
using Common.IO.Utility;
using VariantPhasing;

namespace Scylla
{
    public class CommandLineParameters 
    {
       
        public static void PrintVersionToConsole()
        {
            var currentAssemblyName = Common.IO.FileUtilities.LocalAssemblyName<CommandLineParameters>();
            var currentAssemblyVersion  = Common.IO.FileUtilities.LocalAssemblyVersion<CommandLineParameters>();
            Console.WriteLine(currentAssemblyName + " " + currentAssemblyVersion);
            Console.WriteLine(UsageInfoHelper.GetWebsite());
            Console.WriteLine();
        }

        public static void PrintUsageInfo()
        {
            PrintVersionToConsole();

            Console.WriteLine("-ver/-v : Print version.");
            Console.WriteLine("-help : Print help.");
            Console.WriteLine();
            Console.WriteLine("Required arguments:");
            Console.WriteLine("-vcf : Path to input vcf file.");
            Console.WriteLine("-bam : Path to bam file.");
            Console.WriteLine();
            Console.WriteLine("Optional arguments:");
            Console.WriteLine("-Dist : How close variants need to be to chain together. Should be less than read length.");
            Console.WriteLine("-Out : Output directory. Default is to return results to vcf path.");
            Console.WriteLine("-PassingVariantsOnly : Whether only passing variants should be allowed to phase, 'true' or 'false'. Default, true");
            Console.WriteLine("-ClusterConstraint : Constrain the number of clusters to this number, if possible. Analogous to forced ploidy.");
            Console.WriteLine("-AllowClusterMerging : Whether clusters should be allowed to merge, 'true' or 'false'. Default, true");
            Console.WriteLine("-AllowWorstFitRemoval : Whether a cluster should try to remove and reassign its worst fit, 'true' or 'false'. Default, true");
            Console.WriteLine("-Chr : Array indicating which chromosomes to process (ie, [chr1,chr9]). If empty, all chromosomes will be processed. Default, empty (all)");
            Console.WriteLine("-MaxNbhdsToProcess : A debug option, an integer cap on the number of neighborhoods to process. If -1, all neighborhoods will be processed. Default, -1 (all)");
            Console.WriteLine("-Debug : Run the program in debug mode (additional logging).");
            Console.WriteLine("-t : Number of threads to use. Default, 10");
            Console.WriteLine("-b : Minimum base call quality. Cigar operations with quality below the minimum will be treated as 'N's. Default, 20");
            Console.WriteLine("-m : Minimum map quality to consider a read. Default, 1");
        }

        public static bool ParseAndValidateCommandLine(string[] arguments, PhasingApplicationOptions options)
        {

            if ((arguments == null) || (arguments.Length == 0))
            {
                PrintUsageInfo();
                return false;
            }

            if (arguments.Contains("-help") || (arguments.Contains("-h")))
            {
                PrintUsageInfo();
                return false;
            }



            try
            {
                options.ParseCommandLine(arguments);
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
