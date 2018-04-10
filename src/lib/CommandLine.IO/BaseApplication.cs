using System;
using System.IO;
using CommandLine.IO.Utilities;
using CommandLine.VersionProvider;

namespace CommandLine.IO
{

    public abstract class BaseApplication
    {
        #region members

        protected bool _disableOutput;
        private readonly string _commandLineExample;
        private readonly string _programDescription;
        private readonly string _programAuthors;      
        private readonly IVersionProvider _versionProvider;
        private long _peakMemoryUsageBytes;
        private TimeSpan _wallTimeSpan;

         private BaseOptionParser _applicationCommandParser;

        public CommandLineParseResult ParsingResult { get => _applicationCommandParser.ParsingResult; }
        public BaseOptionParser ApplicationOptionParser { get => _applicationCommandParser; set => _applicationCommandParser = value; }
        protected int ExitCode { get => _applicationCommandParser.ParsingResult.ExitCode; }

        #endregion


        /// <summary>
        /// constructor
        /// </summary>
        protected BaseApplication(string programDescription, string commandLineExample, string programAuthors, IVersionProvider versionProvider = null)
        {
            _programDescription = programDescription;
            _programAuthors = programAuthors;
            _commandLineExample = commandLineExample;
          
            if (versionProvider == null) versionProvider = new DefaultVersionProvider();
            _versionProvider = versionProvider;
        }



        /// <summary>
        /// executes the program (overriden by child classes)
        /// </summary>
        protected abstract void ProgramExecution();

        protected abstract void Init();

        protected abstract void Close();

        /// <summary>
        /// executes the command-line workflow
        /// </summary>
        public void Execute()
        {
            var bench = new Benchmark();

            try
            {
                //List<string> unsupportedOps = null;
               //// ExitCode = ParsingResult.ExitCode;
               // unsupportedOps = ParsingResult.UnsupportedOps;

                if (ParsingResult.ShowVersion)
                {
                    Console.WriteLine("{0}", _versionProvider.GetProgramVersion());
                    ParsingResult.UpdateExitCode(ExitCodeType.Success);
                }
                else
                {
                    if (ParsingResult.ShowHelpMenu)
                    {
                        CommandLineUtilities.DisplayBanner(_programAuthors);
                        ShowHelp();

                        CommandLineUtilities.ShowUnsupportedOptions(ParsingResult.UnsupportedOps);

                        Console.WriteLine();
                        Console.WriteLine(_versionProvider.GetDataVersion());
                        Console.WriteLine();

                        // print the errors if any were found
                        if (FoundParsingErrors()) return;
                    }
                    else
                    {

                        // print the errors if any were found
                        if (FoundParsingErrors()) return;

                        if (!_disableOutput) CommandLineUtilities.DisplayBanner(_programAuthors);

                        Init();

                        ProgramExecution();
                    }
                }
            }
            catch (Exception e)
            {
                ParsingResult.ExitCode = ExitCodeUtilities.ShowExceptionAndUpdateExitCode(e);
            }

            _peakMemoryUsageBytes = MemoryUtilities.GetPeakMemoryUsage();
            _wallTimeSpan = bench.GetElapsedTime();

            Close();

            if (!ParsingResult.ShowVersion && !ParsingResult.ShowHelpMenu && !_disableOutput)
            {
                Console.WriteLine();
                if (_peakMemoryUsageBytes > 0) Console.WriteLine("Peak memory usage: {0}", MemoryUtilities.ToHumanReadable(_peakMemoryUsageBytes));
                Console.WriteLine("Time: {0}", Benchmark.ToHumanReadable(_wallTimeSpan));
            }
        }

        private void ShowHelp()
        {
            Help.Show(ApplicationOptionParser.OptionSetDics, _commandLineExample, _programDescription);
        }


        /// <summary>
        /// returns true if command-line parsing errors were found
        /// </summary>
        private bool FoundParsingErrors()
        {
            // print the errors if any were found
            if (ExitCode == (int)ExitCodeType.Success) return false;

            Console.WriteLine("Some problems were encountered when parsing the command line options:");
            Console.WriteLine("{0}", ParsingResult.ErrorBuilder);
            Console.WriteLine("For a complete list of command line options, type \"dotnet {0} -h\"", Path.GetFileName(Environment.GetCommandLineArgs()[0]));

            return true;
        }

    }  
}
