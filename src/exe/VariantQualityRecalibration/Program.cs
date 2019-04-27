using System;
using System.IO;
using System.Collections.Generic;
using Common.IO.Utility;
using CommandLine.VersionProvider;
using CommandLine.Application;
using CommandLine.Options;
using CommandLine.Util;
using Pisces.IO;

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

            AdjustOptions(ref _options);

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

        
        private void AdjustOptions(ref VQROptions vqrOptions)
        {

            List<string> vcfHeaderLines = AlleleReader.GetAllHeaderLines(vqrOptions.VcfPath);
          
            //where to find the Pisces options used to make the original vcf
            var piscesLogDirectory = Path.Combine(Path.GetDirectoryName(vqrOptions.VcfPath), "PiscesLogs");
            if (!Directory.Exists(piscesLogDirectory))
                piscesLogDirectory = Path.GetDirectoryName(vqrOptions.VcfPath);


            //figure out the original settings used, use those as the defaults.
            VcfConsumerAppParsingUtils.TryToUpdateWithOriginalOptions(vqrOptions, vcfHeaderLines, piscesLogDirectory);

            //let anything input on the command line take precedence
            ApplicationOptionParser.ParseArgs(vqrOptions.CommandLineArguments);


            _options.Save(Path.Combine(vqrOptions.LogFolder, _programName + "Options.used.json"));
        }

    }
}
