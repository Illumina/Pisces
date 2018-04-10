using System;
using System.IO;
using System.Collections.Generic;
using VariantPhasing;
using VariantPhasing.Logic;
using Pisces.IO.Sequencing;
using Common.IO.Utility;
using CommandLine.Options;
using CommandLine.VersionProvider;
using CommandLine.IO.Utilities;
using CommandLine.IO;


namespace Scylla
{
    public class Program : BaseApplication
    {
        private ScyllaApplicationOptions _options;
        static string _commandlineExample = "--bam <bam path> --vcf <vcf path>";
        static string _programDescription = "Scylla: variant phaser";

        public Program(string programDescription, string commandLineExample, string programAuthors, IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, versionProvider = null) { }


        public static int Main(string[] args)
        {

            Program scylla = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite());
            scylla.DoParsing(args);
            scylla.Execute();

            return scylla.ExitCode;
        }

        public void DoParsing(string[] args)
        {
            ApplicationOptionParser = new ScyllaOptionsParser();
            ApplicationOptionParser.ParseArgs(args);
            _options = ((ScyllaOptionsParser)ApplicationOptionParser).ScyllaOptions;

            //Scylla is going to need this to write the output vcf.
            //We could tuck this line into the OptionsParser() constructor if we had a base options class. TODO, maybe.
            _options.CommandLineArguments = ApplicationOptionParser.CommandLineArguments;

        }
        protected override void ProgramExecution()
        {
            try
            {

                var factory = new Factory(_options);
                var variantPhaser = new VariantPhaser(factory);
                variantPhaser.Execute(_options.NumThreads);
            }
            catch (Exception ex)
            {
                var wrappedException = new Exception("Unable to process: " + ex.Message, ex);
                Logger.WriteExceptionToLog(wrappedException);

                throw wrappedException;
            }
        }


        protected override void Init()
        {
            Logger.OpenLog(_options.LogFolder, _options.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(_options.QuotedCommandLineArgumentsString);

            List<string> vcfHeaderLines;
            using (var reader = new VcfReader(_options.VcfPath))
            {
                vcfHeaderLines = reader.HeaderLines;
            }


            //where to find the Pisces options used to make the original vcf
            var piscesLogDirectory = Path.Combine(Path.GetDirectoryName(_options.VcfPath), "PiscesLogs");
            if (!Directory.Exists(piscesLogDirectory))
                piscesLogDirectory = Path.GetDirectoryName(_options.VcfPath);

            
            //figure out the original settings used, use those as the defaults.
            VcfConsumerAppParsingUtils.TryToUpdateWithOriginalOptions(_options, vcfHeaderLines, piscesLogDirectory);

            //let anything input on the command line take precedence
            ApplicationOptionParser.ParseArgs(_options.CommandLineArguments);
         

            _options.Save(Path.Combine(_options.LogFolder, "ScyllaOptions.used.json"));
        }

        protected override void Close()
        {
            Logger.CloseLog();
        }

    }
}