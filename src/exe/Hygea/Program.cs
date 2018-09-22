using System.IO;
using RealignIndels.Logic;
using RealignIndels.Logic.Processing;
using System.Linq;
using Common.IO.Utility;
using CommandLine.Application;
using CommandLine.VersionProvider;
using CommandLine.Util;


namespace RealignIndels
{
    public class Program : BaseApplication<HygeaOptions>
    {
        static string _commandlineExample = "-bam <bam path> -g <genome path>";
        static string _programDescription = "Hygea: indel realigner";
        static string _programName = "Hygea";

        public Program(string programDescription, string commandLineExample, string programAuthors, string programName, IVersionProvider versionProvider = null) : 
            base(programDescription, commandLineExample, programAuthors, programName, versionProvider =null)
        {
            _options = new HygeaOptions();
            _appOptionParser = new HygeaOptionParser();
        }
     
        public static int Main(string[] args)
        {
            
            Program hygea = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            hygea.DoParsing(args);
            hygea.Execute();

            return hygea.ExitCode;
        }

        protected override void ProgramExecution()
        {

            var factory = new Factory(_options);
            var distinctGenomeDirectories = _options.GenomePaths.Distinct();

            foreach (var genomeDirectory in distinctGenomeDirectories)
            {
                var genome = factory.GetReferenceGenome(genomeDirectory);

                var processor = new GenomeProcessor(factory, genome, _options.ChromosomeFilter);

                processor.Execute(_options.MaxNumThreads);
                if (!_options.InsideSubProcess)
                    ConcatenateLogs();
            }
        }

        
        private void ConcatenateLogs()
        {
            Logger.WriteToLog("Concatenating log files");
            var files = Directory.EnumerateFiles(_options.LogFolder, "*_" + _options.LogFileNameBase).ToArray();
            foreach (var file in files)
            {
                foreach (var line in File.ReadLines(file))
                    Logger.AppendRaw(line);

                File.Delete(file);
            }

        }

    }
}
