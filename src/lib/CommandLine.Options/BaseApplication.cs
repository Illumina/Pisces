using System;
using System.IO;
using CommandLine.Util;
using CommandLine.VersionProvider;
using CommandLine.Options;
using Common.IO.Utility;

namespace CommandLine.Application
{

    public abstract class BaseApplication<T>
    {
        #region members

        protected bool _disableOutput;
        //protected IApplicationOptions _options;
        protected T _options;
        protected IOptionParser _appOptionParser;

        private readonly string _programName;
        private readonly string _commandLineExample;
        private readonly string _programDescription;
        private readonly string _programAuthors;
   
        private readonly IVersionProvider _versionProvider;
        private long _peakMemoryUsageBytes;
        private TimeSpan _wallTimeSpan;

       
        public CommandLineParseResult ParsingResult { get => _appOptionParser.ParsingResult; }
        public IOptionParser ApplicationOptionParser { get => _appOptionParser; }

        public T Options{   get => _options; }
        protected int ExitCode { get => _appOptionParser.ParsingResult.ExitCode; }

        #endregion


        /// <summary>
        /// constructor
        /// </summary>
        protected BaseApplication(string programDescription, string commandLineExample, string programAuthors,
            string programName, IVersionProvider versionProvider = null)
        {
            _programDescription = programDescription;
            _programAuthors = programAuthors;
            _commandLineExample = commandLineExample;
            _programName = programName;

            if (versionProvider == null) versionProvider = new DefaultVersionProvider();
            _versionProvider = versionProvider;
        }



        /// <summary>
        /// executes the program (overriden by child classes)
        /// </summary>
        protected abstract void ProgramExecution();

        public void DoParsing(string[] args)
        {
            ApplicationOptionParser.Options = (IApplicationOptions) _options;
            ApplicationOptionParser.ParseArgs(args);            
        }

        public void Init()
        {
            var options = (IApplicationOptions)_options;
            options.SetIODirectories(_programName);
            Logger.OpenLog(options.LogFolder, options.LogFileNameBase);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(options.QuotedCommandLineArgumentsString);        
            options.Save(Path.Combine(options.LogFolder, _programName + "Options.used.json"));
        }

        public void Close()
        {
            Logger.CloseLog();
        }


        /// <summary>
        /// executes the command-line workflow
        /// </summary>
        public void Execute()
        {
            var bench = new Benchmark();

            try
            {
                
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

                        try
                        {
                            ProgramExecution();
                        }
                        catch (Exception e)
                        {
                            Logger.WriteExceptionToLog(e);
                            throw;
                        }
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
