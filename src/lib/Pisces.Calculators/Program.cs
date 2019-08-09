using System.IO;
using CommandLine.Application;
using CommandLine.Util;
using CommandLine.VersionProvider;
using Gemini.IO;
using Gemini.Utility;

namespace Gemini
{
    public class Program : BaseApplication<GeminiApplicationOptions>
    {
        static string _commandlineExample = " --bam <bam path> --genome <genome path> --samtools <samtools path> --outFolder <output path> --numThreads 4";
        static string _programDescription = "Gemini: pair-aware indel realigner and read stitcher (subprocess)";
        static string _programName = "Gemini";


        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
            IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors,
            programName, versionProvider = null)
        {
            _options = new GeminiApplicationOptions();
            _appOptionParser = new GeminiApplicationOptionsParser();
        }


        public static int Main(string[] args)
        {
            Program prog = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(),
                _programName);
            prog.DoParsing(args);
            prog.Execute();

            return prog.ExitCode;
        }


        //wrapper should now handle all throwing and catching.. 
        protected override void ProgramExecution()
        {
            _options.GeminiSampleOptions.InputBam = _options.InputBam;
            _options.GeminiSampleOptions.OutputFolder = _options.OutputDirectory;
            _options.GeminiSampleOptions.OutputBam = Path.Combine(_options.OutputDirectory, "out.bam");
            _options.GeminiOptions.Debug = _options.StitcherOptions.Debug;

            // Gemini defaults different than stitcher defaults
            _options.StitcherOptions.NifyUnstitchablePairs = false;

            // Set stitcher pair-filter-level duplicate filtering if skip and remove dups, to save time
            _options.StitcherOptions.FilterDuplicates = _options.GeminiOptions.SkipAndRemoveDups;

            var dataSourceFactory = new GeminiDataSourceFactory(_options.StitcherOptions, _options.GeminiOptions.GenomePath,
                _options.GeminiOptions.SkipAndRemoveDups, _options.GeminiSampleOptions.RefId, Path.Combine(_options.OutputDirectory, "Regions.txt"), debug: _options.GeminiOptions.Debug);
            var dataOutputFactory = new GeminiDataOutputFactory(_options.StitcherOptions.NumThreads);

            var samtoolsWrapper = new SamtoolsWrapper(_options.GeminiOptions.SamtoolsPath, _options.GeminiOptions.IsWeirdSamtools);

            var geminiWorkflow = new GeminiWorkflow(dataSourceFactory, dataOutputFactory, _options.GeminiOptions, 
                _options.GeminiSampleOptions, _options.RealignmentOptions, _options.StitcherOptions, _options.OutputDirectory, _options.RealignmentAssessmentOptions, _options.IndelFilteringOptions, samtoolsWrapper);
            geminiWorkflow.Execute();

        }

    }
}
