using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.IO.Utility;
using Gemini.Interfaces;
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

            var paths = new List<string>();
            var tasks = new List<ICliTask>();
            var taskDirectories = new List<string>();
            var taskLogDir = Path.Combine(outMultiPath, "TaskLogs");

            foreach (var chrom in MultiProcessHelpers.GetOrderedChromosomes(chromRefIds))
            {
                var outdir = Path.Combine(outMultiPath, chrom);
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
                foreach (var failedTask in tasks.Where(x=>x.ExitCode != 0))
                {
                    Logger.WriteWarningToLog($"Processing failed for {failedTask.Name}. See error log for details.");
                }

                throw new Exception($"Application failed: {tasks.Count(x => x.ExitCode != 0)} tasks failed.");
            }

            Logger.WriteToLog($"Completed {tasks.Count} tasks.");

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

        private void CleanUp(string outMultiPath, List<string> taskDirectories, string taskLogsDir)
        {
            try
            {
                Logger.WriteToLog("Consolidating log files.");
                var perChromLogsDir = Path.Combine(outMultiPath, "GeminiChromosomeLogs");
                Directory.CreateDirectory(perChromLogsDir);
                var chromTaskLogsDir = Path.Combine(perChromLogsDir, "TaskLogs");

                // tjd: if these no longer exist, lets remove them from the clean up routines and from the method signatures
                if (Directory.Exists(chromTaskLogsDir))
                {
                    Directory.Move(chromTaskLogsDir, chromTaskLogsDir + "_" + DateTime.Now.Ticks);
                }

                foreach (var taskDirectory in taskDirectories)
                {
                    var logFileDir = Path.Combine(taskDirectory, "GeminiLogs");
                    var indelsCsv = Path.Combine(taskDirectory, "Indels.csv");

                    var parentName = new DirectoryInfo(logFileDir).Parent.Name;

                    foreach (var file in Directory.GetFiles(logFileDir))
                    {
                        File.Copy(file, Path.Combine(perChromLogsDir, parentName + "_" + new FileInfo(file).Name), true);
                    }

                    File.Copy(indelsCsv, Path.Combine(perChromLogsDir, parentName + "_" + new FileInfo(indelsCsv).Name), true);
                }

                if (!_options.StitcherOptions.Debug)
                {
                    Logger.WriteToLog("Deleting intermediate files.");
                    foreach (var taskDirectory in taskDirectories)
                    {
                        Directory.Delete(taskDirectory, true);
                    }
                }

                // tjd: if these no longer exist, lets remove them from the clean up routines and from the method signatures
                if (Directory.Exists(taskLogsDir))
                {
                    Directory.Move(taskLogsDir, Path.Combine(perChromLogsDir, "TaskLogs"));
                }
            }
            catch (Exception ex)
            {
                //If we cant clean up for some reason, dont crash the whole program. Just log and continue.
                //Leaving the remaining log files might help us figure out why we had issues cleaning up.
                Logger.WriteToLog("Issue cleaning up files:");
                Logger.WriteToLog(ex.ToString());
            }
            Logger.WriteToLog("Done cleaning up.");
        }

        private static void MergeAndFinalizeBam(List<string> perChromPaths, ISamtoolsWrapper samtoolsWrapper, string outFolder, string outputBamName)
        {
            var finalBams = perChromPaths;
            var mergedBam = Path.Combine(outFolder, outputBamName);

            Logger.WriteToLog($"Calling samtools cat to create {mergedBam}.");

            samtoolsWrapper.SamtoolsCat(mergedBam, finalBams);

            Logger.WriteToLog($"Calling samtools index on {mergedBam}.");
            samtoolsWrapper.SamtoolsIndex(mergedBam);
            Logger.WriteToLog("Done finalizing bam.");
        }

    }
}