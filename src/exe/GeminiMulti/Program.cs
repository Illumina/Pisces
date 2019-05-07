using System.Collections.Generic;
using System.Linq;
using Alignment.IO.Sequencing;
using CommandLine.Application;
using CommandLine.Util;
using CommandLine.VersionProvider;
using Gemini.Utility;
using Pisces.Processing.Utility;

namespace GeminiMulti
{
    public class Program : BaseApplication<GeminiMultiApplicationOptions>
    {
        static string _commandlineExample = " --bam <bam path> --genome <genome path> --samtools <samtools path> --outFolder <output path> --numProcesses 20 --exePath <path to gemini subprocess>";
        static string _programDescription = "GeminiMulti: pair-aware indel realigner and read stitcher";
        static string _programName = "GeminiMulti";


        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
            IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {
            _options = new GeminiMultiApplicationOptions();
            _appOptionParser = new GeminiMultiApplicationOptionsParser();
        }


        public static int Main(string[] args)
        {
            Program prog = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            prog.DoParsing(args);
            prog.Execute();
                           
            return prog.ExitCode;
        }


        //wrapper should now handle all throwing and catching..
        protected override void ProgramExecution()
        {
            var optionsUsed = _appOptionParser.ParsingResult.OptionsUsed;

            var doNotPassToSubprocess = new List<string>() { "outFolder", "numProcesses", "exePath", "intermediateDir", "multiprocess", "chromosomes" };

            var cmdLineList = MultiProcessHelpers.GetCommandLineWithoutIgnoredArguments(optionsUsed, doNotPassToSubprocess);


            var refNameMapping = new Dictionary<string, int>();
            using (var bamReader = new BamReader(_options.InputBam))
            {
                var chroms = bamReader.GetReferenceNames();
                foreach (var referenceName in chroms)
                {
                    if (_options.Chromosomes != null && !_options.Chromosomes.ToList().Contains(referenceName))
                    {
                        continue;
                    }
                    refNameMapping.Add(referenceName, bamReader.GetReferenceIndex(referenceName));
                }
            }
            
            var taskManager = new CliTaskManager(_options.NumProcesses);
            var geminiProcessor = new GeminiMultiProcessor(_options, new CliTaskCreator());

            var samtoolsWrapper = new SamtoolsWrapper(_options.GeminiOptions.SamtoolsPath, _options.GeminiOptions.IsWeirdSamtools);

            geminiProcessor.Execute(taskManager, refNameMapping, cmdLineList, samtoolsWrapper);

        }






    }
}
