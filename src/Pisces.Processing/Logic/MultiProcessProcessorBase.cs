using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.Domain.Interfaces;
using Pisces.Processing.Utility;

namespace Pisces.Processing.Logic
{
    public class MultiProcessProcessorBase : BaseProcessor
    {
        protected IJobManager JobManager;
        protected readonly string OutputFolder;
        private readonly string[] _baseArgs;
        private readonly string _exePath;
        private readonly string _logFolder;
        private readonly string _monoPath;
        private readonly string[] _inputFiles;

        public MultiProcessProcessorBase(IGenome genome, IJobManager jobManager, IEnumerable<string> inputFiles,
            string[] commandLineArgs, string outputFolder, string logFolder, string monoPath = null, string exePath = null)
        {
            Genome = genome;
            JobManager = jobManager;

            var pairs = commandLineArgs.Where((x,i) => i % 2 == 0)
                                        .Zip(commandLineArgs.Where((x, i) => i % 2 != 0), (x, y) => new[] { x, y });

            _baseArgs = pairs
                .Where(x => x[0].ToLowerInvariant() != "-bampaths")
                .Where(x => x[0].ToLowerInvariant() != "-bamfolder")
                .Where(x => x[0].ToLowerInvariant() != "-outfolder")
                .Where(x => x[0].ToLowerInvariant() != "-maxthreads")
                .Where(x => x[0].ToLowerInvariant() != "-maxnumthreads")
                .Where(x => x[0].ToLowerInvariant() != "-t")
                .SelectMany(x => x)
                .ToArray();
            OutputFolder = outputFolder;
            _logFolder = logFolder;
            _monoPath = monoPath;

            _exePath = exePath ?? System.Reflection.Assembly.GetEntryAssembly().Location;
            _inputFiles = inputFiles.ToArray();
        }

        public override void InternalExecute(int maxThreads)
        {
            try
            {
                Logger.WriteToLog("Processing genome '{0}' with {1} threads", Genome.Directory, maxThreads);

                var jobs = new List<IJob>();

                foreach (var chrName in Genome.ChromosomesToProcess)
                {
                    foreach (var inputFile in _inputFiles)
                    {
                        var job = new ExternalProcessJob(chrName)
                        {
                            ExecutablePath = _exePath,
                            CommandLineArguments = GetCommandLineArgs(chrName, inputFile)
                        };

                        if (Utilities.IsThisMono())
                        {

                            string discoveredMono = Utilities.GetMonoPath();
                            job.CommandLineArguments = job.ExecutablePath + " " + job.CommandLineArguments;
                            job.ExecutablePath = _monoPath ?? discoveredMono;

                            Logger.WriteToLog(string.Format("Mono path from command line is {0}", _monoPath ?? "empty"));
                            Logger.WriteToLog(string.Format("Mono path discovered is {0}", discoveredMono ?? "empty"));


                            if ((string.IsNullOrEmpty(_monoPath)) && (string.IsNullOrEmpty(discoveredMono)))
                            {
                                Logger.WriteToLog(string.Format("Warning: Unable to determine mono path."));
                            }

                            if (!(string.IsNullOrEmpty(_monoPath)) && !(string.IsNullOrEmpty(discoveredMono)))
                            {
                                if (_monoPath != discoveredMono)
                                {
                                    Logger.WriteToLog(
                                        string.Format(
                                            "Warning: Mono path from command line does not match mono version discovered"));
                                }
                            }

                            Logger.WriteToLog(string.Format("Mono path {0} will be used.", job.ExecutablePath));
                        }

                        Logger.WriteToLog(string.Format("Launching process: {0} {1}", job.ExecutablePath,
                            job.CommandLineArguments));

                        jobs.Add(job);
                    }
                }

                JobManager.Process(jobs); // process all jobs
            }
            finally
            {
                Finish();
            }
        }

        private string GetCommandLineArgs(string chrName, string inputFile)
        {
            var args = _baseArgs.ToList();
            args.Add("-OutFolder");
            args.Add(Path.Combine(OutputFolder, chrName));
            args.Add("-bampaths");
            args.Add(inputFile);
            args.Add("-chrfilter");
            args.Add(chrName);
            args.Add("-InsideSubProcess");
            args.Add("true");
            args.Add("-MaxNumThreads");
            args.Add("1");

            return string.Join(" ", args);
        }

        protected virtual void Finish()
        {
            if (!Directory.Exists(_logFolder))
                Directory.CreateDirectory(_logFolder);

            // clean up directories
            try
            {
                foreach (var chrName in Genome.ChromosomesToProcess)
                {
                    var chrDirectory = Path.Combine(OutputFolder, chrName);

                    // move any aux files (logs, bias output) to main output
                    foreach (var file in Directory.GetFiles(chrDirectory))
                    {
                        var destFile = Path.Combine(_logFolder, chrName + "_" + Path.GetFileName(file));
                        File.Delete(destFile);
                        File.Move(file, destFile);
                    }

                    Directory.Delete(chrDirectory);
                }
            }
            catch (Exception ex)
            {
                // make best effort here, if there's an error move on
                Logger.WriteExceptionToLog(new Exception("Warning: unable to clean up directories", ex));
            }
        }
    }
}
