using System;
using System.IO;
using Common.IO.Utility;
using CommandLine.VersionProvider;
using CommandLine.IO;
using CommandLine.IO.Utilities;

namespace VariantQualityRecalibration
{
    public class Program : BaseApplication
    {
       
        private VQROptions _options;
        static string _commandlineExample = "--vcf <vcf path>";
        static string _programDescription = "VQR: variant quality recalibrator";

        public Program(string programDescription, string commandLineExample, string programAuthors, IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, versionProvider = null) { }


        public static int Main(string[] args)
        {

            Program vqr = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite());
            vqr.DoParsing(args);
            vqr.Execute();

            return vqr.ExitCode;
        }

        public void DoParsing(string[] args)
        {
            ApplicationOptionParser = new VQROptionsParser();
            ApplicationOptionParser.ParseArgs(args);
            _options = ((VQROptionsParser)ApplicationOptionParser).Options;

            //We could tuck this line into the OptionsParser() constructor if we had a base options class.
            _options.CommandLineArguments = ApplicationOptionParser.CommandLineArguments;
        }

        protected override void Init()
        {
            Logger.OpenLog(_options.OutputDirectory, _options.LogFileName);
            Logger.WriteToLog("Command-line arguments: " + _options.QuotedCommandLineArgumentsString);
            _options.Save(Path.Combine(_options.OutputDirectory, "VariantQualityRecalibrationOptions.used.json"));

        }

        protected override void Close()
        {
            Logger.CloseLog();
        }

        protected override void ProgramExecution()
        {
            Logger.WriteToLog("Generating counts file");
            string countsFile = Counts.WriteCountsFile(_options.InputVcf, _options.OutputDirectory, _options.LociCount);

            Logger.WriteToLog("Starting Recalibration");

            try
            {
                QualityRecalibration.Recalibrate(_options.InputVcf, countsFile, _options.OutputDirectory, _options.BaseQNoise,
                    _options.ZFactor, _options.MaxQScore, _options.FilterQScore, _options.QuotedCommandLineArgumentsString);
            }
            catch (Exception e)
            {
                Logger.WriteToLog("*** Error encountered: {0}", e);
                throw e;
            }

            Logger.WriteToLog("Work complete.");
        }

        
        public void RegularResetMain()
        {
            _options = new VQROptions();
            Logger.CloseLog();
        }
    }
}
