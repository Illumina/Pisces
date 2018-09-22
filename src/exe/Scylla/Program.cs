using System;
using System.IO;
using System.Collections.Generic;
using VariantPhasing;
using VariantPhasing.Logic;
using Pisces.IO.Sequencing;
using Common.IO.Utility;
using CommandLine.Options;
using CommandLine.VersionProvider;
using CommandLine.Util;
using CommandLine.Application;

namespace Scylla
{
    public class Program : BaseApplication<ScyllaApplicationOptions>
    {
        static string _commandlineExample = "--bam <bam path> --vcf <vcf path>";
        static string _programDescription = "Scylla: variant phaser";
        static string _programName = "Scylla";


        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
            IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {
            _options = new ScyllaApplicationOptions();
            _appOptionParser = new ScyllaOptionsParser();
        }


        public static int Main(string[] args)
        {

            Program scylla = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            scylla.DoParsing(args);
            scylla.Execute();

            return scylla.ExitCode;
        }

        protected override void ProgramExecution()
        {
            AdjustOptions(ref _options);
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

        
        private void AdjustOptions(ref ScyllaApplicationOptions scyllaOptions)
        {
           
            List<string> vcfHeaderLines;
            using (var reader = new VcfReader(scyllaOptions.VcfPath))
            {
                vcfHeaderLines = reader.HeaderLines;
            }


            //where to find the Pisces options used to make the original vcf
            var piscesLogDirectory = Path.Combine(Path.GetDirectoryName(scyllaOptions.VcfPath), "PiscesLogs");
            if (!Directory.Exists(piscesLogDirectory))
                piscesLogDirectory = Path.GetDirectoryName(scyllaOptions.VcfPath);

            
            //figure out the original settings used, use those as the defaults.
            VcfConsumerAppParsingUtils.TryToUpdateWithOriginalOptions(scyllaOptions, vcfHeaderLines, piscesLogDirectory);

            //let anything input on the command line take precedence
            ApplicationOptionParser.ParseArgs(scyllaOptions.CommandLineArguments);
         

            _options.Save(Path.Combine(scyllaOptions.LogFolder, "ScyllaOptions.used.json"));
        }

    }
}