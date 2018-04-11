using System;
using System.IO;
using Common.IO.Utility;
using CommandLine.IO.Utilities;
using CommandLine.IO;
using CommandLine.VersionProvider;

namespace Stitcher
{
    public class Program : BaseApplication
    {
        private ApplicationOptions _programOptions;
        static string _commandlineExample = "--bam <bam path> ";
        static string _programDescription = "Stitcher: read stitcher";

        public Program(string programDescription, string commandLineExample, string programAuthors, IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, versionProvider = null) { }


        public static int Main(string[] args)
        {

            Program stitcher = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite());
            stitcher.DoParsing(args);
            stitcher.Execute();

            return stitcher.ExitCode;
        }

        public void DoParsing(string[] args)
        {
            ApplicationOptionParser = new StitcherApplicationOptionsParser();
            ApplicationOptionParser.ParseArgs(args);
            _programOptions = ((StitcherApplicationOptionsParser)ApplicationOptionParser).ProgramOptions;
            _programOptions.CommandLineArguments = ApplicationOptionParser.CommandLineArguments;
        }
        protected override void ProgramExecution()
        {
           
            try
            {
                var processor = _programOptions.StitcherOptions.ThreadByChromosome ? (IStitcherProcessor)new GenomeProcessor(_programOptions.InputBam) : new BamProcessor();
                processor.Process(_programOptions.InputBam, _programOptions.OutFolder, _programOptions.StitcherOptions);
            }
            catch (Exception ex)
            {
                var wrappedException = new Exception("Unable to process: " + ex.Message, ex);
                Logger.WriteExceptionToLog(wrappedException);

                throw wrappedException;
            }
        }

        protected override void Close()
        {
            Logger.CloseLog();
        }

        protected override void Init()
        {

            Logger.OpenLog(_programOptions.LogFolder, _programOptions.StitcherOptions.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(_programOptions.QuotedCommandLineArgumentsString);

            _programOptions.Save(Path.Combine(_programOptions.LogFolder, "StitcherOptions.used.json"));

        }
    }
}
