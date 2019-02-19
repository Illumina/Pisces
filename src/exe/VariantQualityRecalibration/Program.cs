using System;
using Common.IO.Utility;
using CommandLine.VersionProvider;
using CommandLine.Application;
using CommandLine.Util;

namespace VariantQualityRecalibration
{
    public class Program : BaseApplication<VQROptions>
    {
       
        static string _commandlineExample = "--vcf <vcf path>";
        static string _programDescription = "VQR: variant quality recalibrator";
        static string _programName = "VQR";
        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
          IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {
            _options = new VQROptions();
            _appOptionParser = new VQROptionsParser();
        }


        public static int Main(string[] args)
        {

            Program vqr = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            vqr.DoParsing(args);
            vqr.Execute();

            return vqr.ExitCode;
        }

      
        protected override void ProgramExecution()
        {
           

            Logger.WriteToLog("Generating counts files");
            SignatureSorterResultFiles results = SignatureSorter.StrainVcf(_options);
             
            Logger.WriteToLog("Starting Recalibration");

            try
            {
                QualityRecalibration.Recalibrate(results, _options);
            }
            catch (Exception e)
            {
                Logger.WriteToLog("*** Error encountered: {0}", e);
                throw e;
            }

            Logger.WriteToLog("Work complete.");
        }

    }
}
