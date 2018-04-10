using System.IO;
using RealignIndels.Logic;
using RealignIndels.Logic.Processing;
using System.Linq;
using Common.IO.Utility;
using CommandLine.VersionProvider;
using CommandLine.IO;
using CommandLine.IO.Utilities;


namespace RealignIndels
{
    public class Program : BaseApplication
    {
        private HygeaOptions _options;
        static string _commandlineExample = "-bam <bam path> -g <genome path>";
        static string _programDescription = "Hygea: indel realigner";        
        public Program(string programDescription, string commandLineExample, string programAuthors, IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, versionProvider=null) {}
     
        public static int Main(string[] args)
        {
            
            Program hygea = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite());
            hygea.DoParsing(args);
            hygea.Execute();

            return hygea.ExitCode;
        }

        public void DoParsing(string[] args)
        {
            ApplicationOptionParser = new HygeaOptionParser();
            ApplicationOptionParser.ParseArgs(args);
            _options =  ((HygeaOptionParser) ApplicationOptionParser).HygeaOptions;
            _options.CommandLineArguments = ApplicationOptionParser.CommandLineArguments;
        }

        protected override void Init()
        {
            Logger.OpenLog(_options.LogFolder, _options.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(string.Join(" ", ApplicationOptionParser.CommandLineArguments));

            _options.Save(Path.Combine(_options.LogFolder, "HygeaOptions.used.json"));
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

                var processor = new GenomeProcessor(factory, genome, _options.ChromosomeFilter);

                processor.Execute(_options.MaxNumThreads);
                if (!_options.InsideSubProcess)
                    ConcatenateLogs();
            }
        }

        
        private void ConcatenateLogs()
        {
            Logger.WriteToLog("Concatenating log files");
            var files = Directory.EnumerateFiles(_options.LogFolder, "*_" + HygeaOptions.LogFileNameBase).ToArray();
            foreach (var file in files)
            {
                foreach (var line in File.ReadLines(file))
                    Logger.AppendRaw(line);
                File.Delete(file);
            }

        }

    }
}
