using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CallSomaticVariants.Logic.Processing;
using CallSomaticVariants.Models;
using CallSomaticVariants.Utility;
using SequencingFiles;

namespace CallSomaticVariants
{
    public class Program
    {
        private ApplicationOptions _options;

        private readonly object _sync;

        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ApplicationOptions.PrintUsageInfo();
                return;
            }

            try
            {
                var application = new Program(args);
                application.Execute();
            }
            catch (Exception ex)
            {
                var wrappedException = new Exception("Unable to process: " + ex.Message, ex);
                Logger.WriteExceptionToLog(wrappedException);

                throw wrappedException;
            }
            finally
            {
                Logger.TryCloseLog();
            }
        }

        public Program(string[] args)
        {
            _options = ApplicationOptions.ParseCommandLine(args);
            Init();
        }

        public Program(ApplicationOptions options)
        {
            _options = options;
            Init();
        }

        private void Init()
        {
            Logger.TryOpenLog(_options.LogFolder, ApplicationOptions.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(_options.CommandLineArguments);

            if (_options.DebugMode)
                _options.Save(Path.Combine(_options.LogFolder, "SomaticVariantCallerOptions.used.xml"));
        }

        public void Execute()
        {
            var factory = new Factory(_options);

            if (!_options.ThreadByChr)
            {
                var distinctGenomeDirectories = _options.GenomePaths.Distinct();

                foreach (var genomeDirectory in distinctGenomeDirectories)
                {
                    var genome = factory.GetReferenceGenome(genomeDirectory);
                    var genomeProcessor = new GenomeProcessor(factory, genome);
                    genomeProcessor.Execute(_options.MaxNumThreads);
                }
            }
            else
            {
                var workRequest = factory.WorkRequests.First();

                var genome = factory.GetReferenceGenome(workRequest.GenomeDirectory);
                var bamProcessor = new BamProcessor(factory, genome);
                bamProcessor.Execute(_options.MaxNumThreads);
            }
        }
    }
}
