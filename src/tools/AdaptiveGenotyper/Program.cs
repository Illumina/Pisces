using System;
using System.Collections.Generic;
using Common.IO.Utility;
using CommandLine.VersionProvider;
using CommandLine.Application;
using CommandLine.Util;

namespace AdaptiveGenotyper
{
    public class Program : BaseApplication<GenotyperOptions>
    {

        static string _commandlineExample = "--vcf <vcf path>";
        static string _programDescription = "GQR: genotype quality recalibrator";
        static string _programName = "GQR";
        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
          IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {
            _options = new GenotyperOptions();
            _appOptionParser = new GenotyperOptionsParser();
        }


        public static int Main(string[] args)
        {

            Program gqr = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            gqr.DoParsing(args);
            gqr.Execute();

            return gqr.ExitCode;
        }


        protected override void ProgramExecution()
        {

            Logger.WriteToLog("Starting Recalibration");

            try
            {
                Recalibration recalibration = new Recalibration();
                recalibration.Recalibrate(_options.InputVcf, _options.OutputDirectory, _options.ModelFile,
                    _options.QuotedCommandLineArgumentsString);
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
