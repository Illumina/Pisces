using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BamStitchingLogic;
using Common.IO.Utility;
using Gemini;
using Gemini.Interfaces;
using Gemini.IO;
using Gemini.Utility;
using Pisces.Processing.Utility;

namespace GeminiMulti
{
    public class GeminiMultiProcessor
    {
        private readonly GeminiMultiApplicationOptions _options;
        private readonly CliTaskCreator _taskCreator;

        public GeminiMultiProcessor(GeminiMultiApplicationOptions options, CliTaskCreator taskCreator)
        {
            _options = options;
            _taskCreator = taskCreator;
        }

        public void Execute(ICliTaskManager cliTaskManager, Dictionary<string, int> chromRefIds, List<string> cmdLineList, ISamtoolsWrapper samtoolsWrapper)
        {
            var bamFile = _options.InputBam;
            var outMultiPath = _options.OutputDirectory;
            var exePath = _options.ExePath;

            List<string> paths;

            var taskDirectories = new List<string>();
            var taskLogDir = Path.Combine(outMultiPath, "TaskLogs");

            
            if (_options.MultiProcess)
            {
                paths = ExecuteChromosomeJobs(cliTaskManager, chromRefIds, cmdLineList, outMultiPath, taskLogDir, exePath, taskDirectories);
            }
            else
            {
                paths = ExecuteChromosomeJobs(chromRefIds, outMultiPath,
                    taskDirectories);
            }

            var outBamName = Path.GetFileNameWithoutExtension(bamFile) + ".PairRealigned.bam";

            MergeAndFinalizeBam(paths, samtoolsWrapper, outMultiPath,
                outBamName);

            try
            {
                CleanUp(outMultiPath, taskDirectories, taskLogDir);
            }
            catch (Exception e)
            {
                Logger.WriteExceptionToLog(new Exception($"Error encountered during cleanup step (after analysis completion).",e));
            }
        }

        class GeminiMultiOptions
        {
            public StitcherOptions StitcherOptions;
            public GeminiSampleOptions GeminiSampleOptions;
            public IndelFilteringOptions IndelFilteringOptions;
            public RealignmentAssessmentOptions RealignmentAssessmentOptions;
            public RealignmentOptions RealignmentOptions;
            public GeminiOptions GeminiOptions;

            public string InputBam = null;
            public string OutputDirectory = null;
        }

        private List<string> ExecuteChromosomeJobs(Dictionary<string, int> chromRefIds,
            string outMultiPath, List<string> taskDirectories)
        {
            var orderedChroms = MultiProcessHelpers.GetOrderedChromosomes(chromRefIds).ToList();
            ConcurrentQueue<string> input = new ConcurrentQueue<string>(orderedChroms);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var tasks = new List<Task>();
            var pathsArray = new string[chromRefIds.Max(x=>x.Value) + 1];

            using (var concurrencySemaphore = new SemaphoreSlim(_options.NumProcesses))
            {
                foreach (var chrom in orderedChroms)
                {
                    {
                        var options = new GeminiMultiOptions()
                        {
                            StitcherOptions = (_options.StitcherOptions.DeepCopy()),
                            GeminiSampleOptions = (_options.GeminiSampleOptions.DeepCopy()),
                            IndelFilteringOptions =
                                (_options.IndelFilteringOptions.DeepCopy()),
                            RealignmentAssessmentOptions =
                                (_options.RealignmentAssessmentOptions.DeepCopy()),
                            RealignmentOptions = (_options.RealignmentOptions.DeepCopy()),

                            GeminiOptions = (_options.GeminiOptions.DeepCopy()),
                            InputBam = _options.InputBam,
                            OutputDirectory = _options.OutputDirectory
                        };


                        Console.WriteLine($"Waiting to launch job for chrom {chrom}");
                        Console.WriteLine($"Launching job for chrom {chrom}");
                        concurrencySemaphore.Wait();
                        tasks.Add(Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                pathsArray[chromRefIds[chrom]] = ProcessChromosome(chromRefIds, outMultiPath, taskDirectories, chrom, options);
                            }
                            finally
                            {
                                concurrencySemaphore.Release();
                            }
                        }));

                    }
                }

                Logger.WriteToLog("Launched all tasks.");
                Task.WaitAll(tasks.ToArray());

                return pathsArray.Where(x=> x!=null).ToList();

            }
        }
        
        private static string ProcessChromosome(Dictionary<string, int> chromRefIds, string outMultiPath, List<string> taskDirectories,
            string chrom, GeminiMultiOptions options)
        {
            // TODO either officially deprecate non-multiprocess-processing and remove this, or consolidate this with the Gemini calling code from Gemini/Program.cs

            var outdir = Path.Combine(outMultiPath, chrom);
            var refId = chromRefIds[chrom];
            var intermediate = string.IsNullOrEmpty(options.GeminiSampleOptions.IntermediateDir)
                ? null
                : Path.Combine(options.GeminiSampleOptions.IntermediateDir, chrom);
            var geminiSampleOptions = new GeminiSampleOptions
            {
                InputBam = options.InputBam,
                OutputFolder = outdir,
                OutputBam = Path.Combine(outdir, "out.bam"),
                IntermediateDir = intermediate,
                RefId = refId
            };

            // Gemini defaults different than stitcher defaults
            options.StitcherOptions.NifyUnstitchablePairs = false;

            // Set stitcher pair-filter-level duplicate filtering if skip and remove dups, to save time
            options.StitcherOptions.FilterDuplicates = options.GeminiOptions.SkipAndRemoveDups;

            var dataSourceFactory = new GeminiDataSourceFactory(options.StitcherOptions, options.GeminiOptions.GenomePath,
                options.GeminiOptions.SkipAndRemoveDups, refId,
                Path.Combine(outdir, "Regions.txt"), debug: options.GeminiOptions.Debug);
            var dataOutputFactory = new GeminiDataOutputFactory(options.StitcherOptions.NumThreads);
            var samtoolsWrapper = new SamtoolsWrapper(options.GeminiOptions.SamtoolsPath, options.GeminiOptions.IsWeirdSamtools);

        var geminiWorkflow = new GeminiWorkflow(dataSourceFactory, dataOutputFactory,
                options.GeminiOptions, geminiSampleOptions, options.RealignmentOptions,  options.StitcherOptions, options.OutputDirectory, options.RealignmentAssessmentOptions, options.IndelFilteringOptions, samtoolsWrapper);

            Directory.CreateDirectory(outdir);
            geminiWorkflow.Execute();

            //var logger = new Illumina.CG.Common.Logging.Logger(taskLogDir, $"GeminiTaskLog_{chrom}.txt");
            //var task = _taskCreator.GetCliTask(cmdLineList.ToArray(), chrom, exePath, outdir, chromRefIds[chrom], logger,
            //    string.IsNullOrEmpty(_options.GeminiSampleOptions.IntermediateDir)
            //        ? null
            //        : Path.Combine(_options.GeminiSampleOptions.IntermediateDir, chrom));

            //tasks.Add(task);

            Console.WriteLine($"Completed Gemini Workflow for {chrom}");

            var path =(Path.Combine(outdir, "merged.bam.sorted.bam"));
            taskDirectories.Add(outdir);
            //paths[refId] = path;
            return path;
        }


        private List<string> ExecuteChromosomeJobs(ICliTaskManager cliTaskManager, Dictionary<string, int> chromRefIds, List<string> cmdLineList,
            string outMultiPath, string taskLogDir, string exePath, List<string> taskDirectories)
        {
            var tasks = new List<ICliTask>();
            var paths = new List<string>();
            //var loggers = new List<Illumina.CG.Common.Logging.Logger>();

            foreach (var chrom in MultiProcessHelpers.GetOrderedChromosomes(chromRefIds))
            {
                var outdir = Path.Combine(outMultiPath, chrom);
                //var logger = new Illumina.CG.Common.Logging.Logger(taskLogDir, $"GeminiTaskLog_{chrom}.txt");
                //loggers.Add(logger);
                var task = _taskCreator.GetCliTask(cmdLineList.ToArray(), chrom, exePath, outdir, chromRefIds[chrom]);
                tasks.Add(task);
                paths.Add(Path.Combine(outdir, "merged.bam.sorted.bam"));
                taskDirectories.Add(outdir);
            }

            cliTaskManager.Process(tasks);

            foreach (var task in tasks)
            {
                Logger.WriteToLog($"Completed task {task.Name} with exit code {task.ExitCode}.");
            }

            if (tasks.Any(x => x.ExitCode != 0))
            {
                foreach (var failedTask in tasks.Where(x => x.ExitCode != 0))
                {
                    Logger.WriteWarningToLog($"Processing failed for {failedTask.Name}. See error log for details.");
                }

                throw new Exception($"Application failed: {tasks.Count(x => x.ExitCode != 0)} tasks failed.");
            }

            Logger.WriteToLog($"Completed {tasks.Count} tasks.");
            return paths;
            //try
            //{
            //    foreach (var logger in loggers)
            //    {
            //        logger.Dispose();
            //    }
            //}
            //catch (Exception e)
            //{
            //    Logger.WriteExceptionToLog(
            //        new Exception($"Error encountered during logging cleanup step (after individual analysis completion).", e));
            //}
        }

        private void CleanUp(string outMultiPath, List<string> taskDirectories, string taskLogsDir)
        {
            Logger.WriteToLog("Consolidating log files.");
            var perChromLogsDir = Path.Combine(outMultiPath, "GeminiChromosomeLogs");
            Directory.CreateDirectory(perChromLogsDir);
            var chromTaskLogsDir = Path.Combine(perChromLogsDir, "TaskLogs");
            if (Directory.Exists(chromTaskLogsDir))
            {
                Directory.Move(chromTaskLogsDir, chromTaskLogsDir + "_" + DateTime.Now.Ticks);
            }

            foreach (var taskDirectory in taskDirectories)
            {
                var logFileDir = Path.Combine(taskDirectory, "GeminiLogs");
                var indelsCsv = Path.Combine(taskDirectory, "Indels.csv");
                var conclusionsCsv = Path.Combine(taskDirectory, "ReadConclusions.csv");
                var statusesCsv = Path.Combine(taskDirectory, "StatusCounts.csv");
                var indelStatusesCsv = Path.Combine(taskDirectory, "IndelStatusCounts.csv");
                var indelOutcomesCsv = Path.Combine(taskDirectory, "IndelOutcomes.csv");
                var categoryOutcomesCsv = Path.Combine(taskDirectory, "CategoryOutcomes.csv");
                var finalIndelsCsv = Path.Combine(taskDirectory, "FinalIndels.csv");
                var indelsAfterSnowballCsv = Path.Combine(taskDirectory, "Indels_AfterSnowball.csv");
                var finalIndelsAfterSnowballCsv = Path.Combine(taskDirectory, "FinalIndels_AfterSnowball.csv");

                var summaryFiles = new List<string>()
                {
                    indelsCsv,
                    indelOutcomesCsv,
                    conclusionsCsv,
                    statusesCsv,
                    indelStatusesCsv,
                    finalIndelsCsv,
                    indelsAfterSnowballCsv,
                    finalIndelsAfterSnowballCsv,
                    categoryOutcomesCsv
                };

                var parentName = new DirectoryInfo(logFileDir).Parent.Name;

                if (Directory.Exists(logFileDir))
                {
                    try
                    {
                        foreach (var file in Directory.GetFiles(logFileDir))
                        {
                            File.Copy(file, Path.Combine(perChromLogsDir, parentName + "_" + new FileInfo(file).Name),
                                true);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Issue copying log files: " + e.Message, e);
                    }
                }
                else
                {
                    Console.WriteLine($"Directory does not exist: {logFileDir}.");
                }

                foreach (var summaryFile in summaryFiles)
                {
                    if (File.Exists(summaryFile))
                    {
                        File.Copy(summaryFile, Path.Combine(perChromLogsDir, parentName + "_" + new FileInfo(summaryFile).Name), true);
                    }
                }
            }

            if (!_options.StitcherOptions.Debug && !_options.GeminiOptions.KeepUnmergedBams)
            {
                Logger.WriteToLog("Deleting intermediate files.");
                foreach (var taskDirectory in taskDirectories)
                {
                    Directory.Delete(taskDirectory, true);
                }
            }

            //Directory.Move(taskLogsDir, Path.Combine(perChromLogsDir, "TaskLogs"));

            Logger.WriteToLog("Done cleaning up.");
        }

        private static void MergeAndFinalizeBam(List<string> perChromPaths, ISamtoolsWrapper samtoolsWrapper, string outFolder, string outputBamName)
        {
            var finalBams = perChromPaths;
            var mergedBam = Path.Combine(outFolder, outputBamName);

            Logger.WriteToLog($"Calling samtools cat on {perChromPaths.Count} files to create {mergedBam}.");

            samtoolsWrapper.SamtoolsCat(mergedBam, finalBams);

            Logger.WriteToLog($"Calling samtools index on {mergedBam}.");
            samtoolsWrapper.SamtoolsIndex(mergedBam);
            Logger.WriteToLog("Done finalizing bam.");
        }

    }
}