using System;
using System.IO;
using System.Linq;
using CallVariants.Logic.Processing;
using Common.IO.Utility;
using Pisces.Domain.Options;
using Pisces.Processing;
using Pisces.Processing.Logic;
using Pisces.Processing.Utility;

namespace Pisces
{
    public class Program
    {
        private PiscesApplicationOptions _options;

        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PiscesApplicationOptions.PrintUsageInfo();
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
                Logger.CloseLog();
            }
        }

        public Program(string[] args)
        {
            _options = PiscesApplicationOptions.ParseCommandLine(args);   
			if(_options == null) return;     
            Init();
        }

        private void Init()
        {
            Logger.OpenLog(_options.LogFolder, _options.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(string.Join(" ", _options.CommandLineArguments));

            _options.Save(Path.Combine(_options.LogFolder, "PiscesOptions.used.json"));
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
                    ? new GenomeProcessor(factory, genome, !_options.ThreadByChr,true)
                    : new GenomeProcessor(factory, genome, !_options.ThreadByChr || _options.InsideSubProcess, !_options.InsideSubProcess);

                processor.Execute(_options.MaxNumThreads);
            }
        }
    }
}
