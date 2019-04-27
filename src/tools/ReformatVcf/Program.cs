
using System;
using System.IO;
using System.Collections.Generic;
using Common.IO.Utility;
using CommandLine.VersionProvider;
using CommandLine.Application;
using CommandLine.Options;
using CommandLine.Util;
using Pisces.IO.Sequencing;
namespace ReformatVcf
{
    class Program : BaseApplication<ReformatOptions>
    {
        static string _commandlineExample = "--vcf <vcf path>";
        static string _programDescription = "ReformatVcf: reformat a vcf from uncrushed to crushed";
        static string _programName = "ReformatVcf";


        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
     IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {
            _options = new ReformatOptions();
            _appOptionParser = new ReformatOptionsParser();
        }

        public static int Main(string[] args)
        {

            Program reformat = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            reformat.DoParsing(args);
            reformat.Execute();

            return reformat.ExitCode;
        }

        protected override void ProgramExecution()
        {
            Logger.WriteToLog("Starting Reformating");

            try
            {
                Reformat.DoReformating(_options);
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

