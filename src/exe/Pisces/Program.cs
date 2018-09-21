using System.Linq;
using CallVariants.Logic.Processing;
using Pisces.Domain.Options;
using CommandLine.VersionProvider;
using CommandLine.Options;
using CommandLine.Application;
using CommandLine.Util;

namespace Pisces
{
    public class Program : BaseApplication<PiscesApplicationOptions> 
    {
        static string _commandlineExample = "-bam <bam path> -g <genome path>";
        static string _programDescription = "Pisces: variant caller";
        static string _programName = "Pisces";

        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
            IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, 
                programAuthors, programName, versionProvider = null)
        {
            _options = new PiscesApplicationOptions();
            _appOptionParser = new PiscesOptionsParser();
        }

        public static int Main(string[] args)
        {

            Program pisces = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            pisces.DoParsing(args);
            pisces.Execute();

            return pisces.ExitCode;
        }

        protected override void ProgramExecution()
        {
            var piscesOptions = (PiscesApplicationOptions) Options;
            var factory = new Factory(piscesOptions);

            var distinctGenomeDirectories = piscesOptions.GenomePaths.Distinct();

            foreach (var genomeDirectory in distinctGenomeDirectories)
            {
                var genome = factory.GetReferenceGenome(genomeDirectory);

                var processor = (piscesOptions.ThreadByChr && piscesOptions.MultiProcess && !piscesOptions.InsideSubProcess)
                    ? new GenomeProcessor(factory, genome, !piscesOptions.ThreadByChr,true)
                    : new GenomeProcessor(factory, genome, !piscesOptions.ThreadByChr || piscesOptions.InsideSubProcess, !piscesOptions.InsideSubProcess);

                processor.Execute(piscesOptions.MaxNumThreads);
            }
        }
    }
}
