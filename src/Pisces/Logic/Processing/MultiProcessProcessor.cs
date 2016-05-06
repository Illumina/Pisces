using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.IO;
using Pisces.Processing.Logic;
using Pisces.Processing.Utility;

namespace Pisces.Logic.Processing
{
    public class MultiProcessProcessor : BaseProcessor
    {
        protected readonly List<BamWorkRequest> WorkRequests;
        protected IJobManager JobManager;
        private readonly string _commandLineArgs;
        private readonly string _outputFolder;
        private string _exePath;
        private Factory _factory;
        private readonly string _logFolder;
        private readonly string _monoPath;

        public MultiProcessProcessor(Factory factory, IGenome genome,
            string commandLineArgs, string outputFolder, string logFolder, string monoPath = null, string exePath = null)
        {
            Genome = genome;
            WorkRequests = factory.WorkRequests;

            _commandLineArgs = commandLineArgs;
            _outputFolder = outputFolder ?? Path.GetDirectoryName(WorkRequests[0].OutputFilePath);
            _logFolder = logFolder;
            _factory = factory;
            _monoPath = monoPath;

            _exePath = exePath ?? Assembly.GetExecutingAssembly().Location;
        }

        public override void InternalExecute(int maxThreads)
        {
            try
            {
                Logger.WriteToLog("Processing genome '{0}' with {1} threads", Genome.Directory, maxThreads);

                JobManager = new JobManager(maxThreads);
                var jobs = new List<IJob>();

                foreach (var chrName in Genome.ChromosomesToProcess)
                {
                    var job = new ExternalProcessJob(chrName)
                    {
                        ExecutablePath = _exePath,
                        CommandLineArguments = GetCommandLineArgs(chrName)
                    };

                    if (Utilities.IsThisMono())
                    {
                        job.CommandLineArguments = job.ExecutablePath + " " + job.CommandLineArguments;
                        job.ExecutablePath = _monoPath ?? Utilities.GetMonoPath();
                    }

                    Logger.WriteToLog(string.Format("Launching process: {0} {1}", job.ExecutablePath, job.CommandLineArguments));

                    jobs.Add(job);
                }

                JobManager.Process(jobs); // process all jobs
            }
            finally
            {
                Finish();
            }
        }

        private string GetCommandLineArgs(string chrName)
        {
            var args = _commandLineArgs;
            var chrOutputFolder = Path.Combine(_outputFolder, chrName);

            if (_commandLineArgs.ToLower().Contains("-outfolder"))
                args = _commandLineArgs.Replace(_outputFolder, chrOutputFolder);
            else
            {
                args += " -OutFolder " + chrOutputFolder;
            }

            return string.Format("{0} -ChrFilter {1} -InsideSubProcess true", args, chrName);
        }

        protected void Finish()
        {
            foreach (var workRequest in WorkRequests)
            {
                using (var vcfWriter = _factory.CreateVcfWriter(workRequest.OutputFilePath, new VcfWriterInputContext
                {
                    ReferenceName = Genome.Directory,
                    CommandLine = _factory.GetCommandLine(),
                    SampleName = Path.GetFileName(workRequest.BamFilePath),
                    ContigsByChr = Genome.ChromosomeLengths
                }))
                {
                    vcfWriter.WriteHeader();
                }

                foreach (var chrName in Genome.ChromosomesToProcess)
                {
                    var chrOutput = Path.Combine(_outputFolder, chrName, Path.GetFileName(workRequest.OutputFilePath));

                    if (File.Exists(chrOutput))
                    {
                        using (Stream input = File.OpenRead(chrOutput))
                        using (Stream output = new FileStream(workRequest.OutputFilePath, FileMode.Append,
                            FileAccess.Write, FileShare.None))
                        {
                            input.CopyTo(output); // Using .NET 4
                        }

                        File.Delete(chrOutput);
                    }
                }
            }

            if (!Directory.Exists(_logFolder))
                Directory.CreateDirectory(_logFolder);

            // clean up directories
            try
            {
                foreach (var chrName in Genome.ChromosomesToProcess)
                {
                    var chrDirectory = Path.Combine(_outputFolder, chrName);

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
