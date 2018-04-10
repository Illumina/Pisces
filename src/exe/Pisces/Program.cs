using System.IO;
using System.Linq;
using CallVariants.Logic.Processing;
using Common.IO.Utility;
using Pisces.Domain.Options;
using CommandLine.VersionProvider;
using CommandLine.Options;
using CommandLine.IO;
using CommandLine.IO.Utilities;

namespace Pisces
{
    public class Program : BaseApplication
    {
        private PiscesApplicationOptions _options;
        static string _commandlineExample = "-bam <bam path> -g <genome path>";
        static string _programDescription = "Pisces: variant caller";

        public Program(string programDescription, string commandLineExample, string programAuthors, IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, versionProvider = null) { }

        public static int Main(string[] args)
        {

            Program pisces = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite());
            pisces.DoParsing(args);
            pisces.Execute();

            return pisces.ExitCode;
        }

        public void DoParsing(string[] args)
        {
            ApplicationOptionParser = new PiscesOptionsParser();
            ApplicationOptionParser.ParseArgs(args);
            _options = ((PiscesOptionsParser)ApplicationOptionParser).PiscesOptions;
            _options.CommandLineArguments = ApplicationOptionParser.CommandLineArguments;
        }

        protected override void Init()
        {
            Logger.OpenLog(_options.LogFolder, _options.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(string.Join(" ", ApplicationOptionParser.CommandLineArguments));

            _options.Save(Path.Combine(_options.LogFolder, "PiscesOptions.used.json"));
        }

        protected override void Close()
        {
            Logger.CloseLog();
        }

        protected override void ProgramExecution()
        {
            
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
