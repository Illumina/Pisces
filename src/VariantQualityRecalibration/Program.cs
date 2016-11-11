using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Pisces.Processing.Utility;

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
                    Logger.WriteToLog(">>> **Warning**: VCF supplied to Recalibration algorithms should be genome VCF. Was this the intent?");
                    Logger.WriteToLog(">>> continuing...");
                }

                Logger.WriteToLog(">>> generating counts file");
                string countsFile = Counts.WriteCountsFile(options.InputVcf, options.OutputDirectory);

                Logger.WriteToLog(">>> starting Recalibration");
                QualityRecalibration.Recalibrate(options.InputVcf, countsFile, options.BaseQNoise, 
                    options.ZFactor, options.MaxQScore, options.FilterQScore);        
        
            }
            catch (Exception e)
            {
                Logger.WriteToLog("*** Error encountered: {0}", e);
            }
            Logger.WriteToLog(">>> Work complete.");
            Logger.TryCloseLog();
            return 0;
        }

        public static void Init(ApplicationOptions options)
        {
            Logger.TryOpenLog(options.OutputDirectory, options.LogFileName);
			options.Save(Path.Combine(options.OutputDirectory, "VariantQualityRecalibrationOptions.used.xml"));

		}
    }
}
