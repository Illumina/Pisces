using System;
using System.Collections.Generic;
using Common.IO.Utility;
using CommandLine.VersionProvider;
using CommandLine.Application;
using CommandLine.Util;

namespace AdaptiveGenotyper
{
    public class Program : BaseApplication<AdaptiveGtOptions>
    {

        static string _commandlineExample = "--vcf <vcf path>";
        static string _programDescription = "Adaptive Genotyper: A binomial mixture model genotyper for germline variant calling.";
        static string _programName = "AdaptiveGT";
        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
          IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {
            _options = new AdaptiveGtOptions();
            _appOptionParser = new AdaptiveGtOptionsParser();
        }


        public static int Main(string[] args)
        {

            Program adaptiveGT = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            adaptiveGT.DoParsing(args);
            adaptiveGT.Execute();

            return adaptiveGT.ExitCode;
        }


        protected override void ProgramExecution()
        {

            Logger.WriteToLog("Starting Recalibration");

            try
            {
                Recalibration recalibration = new Recalibration(_options);
                recalibration.Recalibrate();
            }
            catch (Exception e)
            {
                Logger.WriteToLog("*** Error encountered: {0}", e);
                throw;
            }

            Logger.WriteToLog("Work complete.");
        }

    }
}
