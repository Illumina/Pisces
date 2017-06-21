using System;
using System.Collections.Generic;
using System.IO;
using Common.IO.Utility;

namespace VariantQualityRecalibration
{
    //example cmd line
    // -vcf \\ussd-prd-isi04\pisces\TestData\ByFeature\QScoreRecalibration_PICS-5\L11_S1.genome.vcf -log hi.txt
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
            Init(options);

            if (options == null)
            {
                ApplicationOptions.PrintUsageInfo();
                return 1;
            }
            try
            {
                if (!(options.InputVcf).ToLower().EndsWith(".genome.vcf"))
                {
                    Logger.WriteWarningToLog("VCF supplied to Recalibration algorithms should be genome VCF. Was this the intent?");
                    Logger.WriteToLog("Continuing...");
                }

                Logger.WriteToLog("Generating counts file");
                string countsFile = Counts.WriteCountsFile(options.InputVcf, options.OutputDirectory);

                Logger.WriteToLog("Starting Recalibration");
                QualityRecalibration.Recalibrate(options.InputVcf, countsFile, options.BaseQNoise, 
                    options.ZFactor, options.MaxQScore, options.FilterQScore);        
        
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
