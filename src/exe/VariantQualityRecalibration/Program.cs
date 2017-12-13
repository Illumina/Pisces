using System;
using System.IO;
using Common.IO.Utility;

namespace VariantQualityRecalibration
{
    //example cmd line
    // -vcf pisces\TestData\ByFeature\QScoreRecalibration_PICS-5\L11_S1.genome.vcf -log hi.txt
    public class Program
    {
        static int Main(string[] arguments)
        {


	        if (arguments.Length == 0)
	        {
				ApplicationOptions.PrintUsageInfo();
				return 1;
			}

            var options = ApplicationOptions.ParseCommandLine(arguments);
           
            if (options == null)
            {
                ApplicationOptions.PrintUsageInfo();
                return 1;
            }
            try
            {
                Init(options);
                if (options.InputVcf.ToLower().EndsWith(".vcf"))
                {
                    if (!(options.InputVcf).ToLower().EndsWith(".genome.vcf"))
                    {
                        // if its a vcf and not a gvcf, they better have supplied a loci count.

                        if (options.LociCount > 0)
                        {
                            Logger.WriteToLog("Recalibration algorithm processing .vcf, with loci count " + options.LociCount + ".");
                        }
                        else
                        {
                            Logger.WriteWarningToLog("Recalibration algorithm needs a loci count when processing a .vcf .");
                            ApplicationOptions.PrintUsageInfo();
                            return 1;
                        }
                    }
                }
                else
                {
                    Logger.WriteToLog("Warning. File passed to Recalibration algorithm should be .vcf or .genome.vcf .");
                }


                Logger.WriteToLog("Generating counts file");
                string countsFile = Counts.WriteCountsFile(options.InputVcf, options.OutputDirectory, options.LociCount);

                Logger.WriteToLog("Starting Recalibration");

                QualityRecalibration.Recalibrate(options.InputVcf, countsFile, options.OutputDirectory, options.BaseQNoise, 
                    options.ZFactor, options.MaxQScore, options.FilterQScore, string.Join(" ", arguments));        
        
            }
            catch (Exception e)
            {
                Logger.WriteToLog("*** Error encountered: {0}", e);
            }
            Logger.WriteToLog("Work complete.");
            Logger.CloseLog();
            return 0;
        }

        public static void Init(ApplicationOptions options)
        {
            Logger.OpenLog(options.OutputDirectory, options.LogFileName);
			options.Save(Path.Combine(options.OutputDirectory, "VariantQualityRecalibrationOptions.used.json"));

		}
    }
}
