using System;
using System.IO;
using Pisces.Domain.Utility;
using Common.IO;
using Common.IO.Utility;

namespace VariantQualityRecalibration
{
    public class ApplicationOptions
    {
    
        #region Members
        public string InputVcf;
        public string OutputDirectory = "";
        public string LogFileName = "VariantQualityRecalibrationLog.txt";

        //+
        //calibration parameters
        public int BaseQNoise = 20;
        public int FilterQScore = 30;
        public int MaxQScore = 100;
        public double ZFactor = 2;
        //-
        #endregion
        public static void PrintVersionToConsole()
        {
            var currentDllName = FileUtilities.LocalAssemblyDll<ApplicationOptions>().Replace(".dll","") ;
            var currentVersion = FileUtilities.LocalAssemblyVersion<ApplicationOptions>();
            Console.WriteLine("Version:\t" + currentVersion);
            Console.WriteLine(currentDllName + " " + currentVersion);
            Console.WriteLine(UsageInfoHelper.GetWebsite());
            Console.WriteLine();
        }

        public static void PrintUsageInfo()
        {
            PrintVersionToConsole();

            Console.WriteLine("Required arguments:");
            Console.WriteLine("-vcf imput file name ");
            Console.WriteLine("Optional arguments:");
            Console.WriteLine("-o output directory");
            Console.WriteLine("-log log file name");
            Console.WriteLine("-b baseline noise level, default 20. (The new noise level is never recalibrated to lower than this.)");
            Console.WriteLine("-z thresholding parameter, default 2 (How many std devs above averge observed noise will the algorithm tolerate, before deciding a mutation type is likely to be artifact ) ");
            Console.WriteLine("-f filter Q score, default 30 (if a variant gets recalibrated, when we apply the \"LowQ\" filter)");
            Console.WriteLine("-Q max Q score, default 100 (if a variant gets recalibrated, when we cap the new Q score");
        }

        public static ApplicationOptions ParseCommandLine(string[] Arguments)
        {
            var options = new ApplicationOptions();
            var argumentIndex = 0;
            while (argumentIndex < Arguments.Length)
            {
                if (Arguments[argumentIndex] == null || Arguments[argumentIndex].Length == 0)
                {
                    argumentIndex++;
                    continue;
                }
                string value = null;
                if (argumentIndex < Arguments.Length - 1) value = Arguments[argumentIndex + 1];
                switch (Arguments[argumentIndex].ToLower())
                {
                    case "-vcf":
                        options.InputVcf = value;
                        break;                   
                    case "-o":
                        options.OutputDirectory = value;
                        break;
                    case "-b":
                        options.BaseQNoise = int.Parse(value);
                        break;
                    case "-f":
                        options.FilterQScore = int.Parse(value);
                       break;
                    case "-z":
                        options.ZFactor = double.Parse(value);
                        break;
                    case "-q":
                        options.MaxQScore = int.Parse(value);
                        break;
                    case "-log":
                        options.LogFileName = value;
                        break;
                    default:
                        Console.WriteLine("Error: Unknown argument '{0}'", Arguments[argumentIndex]);
                        return null;
                }
                argumentIndex += 2;
                        
            }

            if (string.IsNullOrEmpty(options.InputVcf)) 
            {
                Console.WriteLine("Error: no input vcf file");

                return null; // basic validation! 
            }

            if (string.IsNullOrEmpty(options.OutputDirectory))
            {
                options.OutputDirectory = Path.GetDirectoryName(options.InputVcf);
            }

         
            return options;
        }
        public void Save(string filepath)
        {
            JsonUtil.Save(filepath, this);
        }
    }
}
