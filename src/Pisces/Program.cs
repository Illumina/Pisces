using System;
using System.IO;
using System.Linq;
using CallVariants.Logic.Processing;
using Pisces.Domain.Utility;
using Pisces.Logic.Processing;
using Pisces.Processing.Logic;
using Pisces.Processing.Utility;

namespace Pisces
{
    public class Program
    {
        private ApplicationOptions _options;

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
			if(_options == null) return;     
            Init();
        }

        private void Init()
        {
            Logger.TryOpenLog(_options.LogFolder, _options.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(string.Join(" ", _options.CommandLineArguments));

            _options.Save(Path.Combine(_options.LogFolder, "PiscesOptions.used.xml"));
        }

        public void Execute()
        {
			if(_options == null) return;
			var factory = new Factory(_options);

            var distinctGenomeDirectories = _options.GenomePaths.Distinct();

            foreach (var genomeDirectory in distinctGenomeDirectories)
            {
                var genome = factory.GetReferenceGenome(genomeDirectory);
                var processor = (_options.ThreadByChr && _options.MultiProcess && !_options.InsideSubProcess)
                    ? new MultiProcessProcessor(factory, genome, new JobManager(_options.MaxNumThreads), _options.CommandLineArguments, _options.OutputFolder, _options.LogFolder, _options.MonoPath)
                    : (BaseProcessor)new GenomeProcessor(factory, genome, !_options.ThreadByChr || _options.InsideSubProcess, !_options.InsideSubProcess);
                processor.Execute(_options.MaxNumThreads);
            }
        }
    }
}
