using System;
using Common.IO.Utility;
using CommandLine.Util;
using CommandLine.Application;
using CommandLine.VersionProvider;

namespace Stitcher
{
    public class Program : BaseApplication<StitcherApplicationOptions>
    {
        static string _commandlineExample = "--bam <bam path> ";
        static string _programDescription = "Stitcher: read stitcher";
        static string _programName = "Stitcher";

        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
           IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {
            _options = new StitcherApplicationOptions();
            _appOptionParser = new StitcherApplicationOptionsParser();
        }


        public static int Main(string[] args)
        {

            Program stitcher = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            stitcher.DoParsing(args);
            stitcher.Execute();

            return stitcher.ExitCode;
        }


        protected override void ProgramExecution()
        {
           
            try
            {
                var processor = _options.StitcherOptions.ThreadByChromosome ? (IStitcherProcessor)new GenomeProcessor(_options.InputBam) : new BamProcessor();
                processor.Process(_options.InputBam, _options.OutputDirectory, _options.StitcherOptions);
            }
            catch (Exception ex)
            {
                var wrappedException = new Exception("Unable to process: " + ex.Message, ex);
                Logger.WriteExceptionToLog(wrappedException);

                throw wrappedException;
            }
        }

    }
}
