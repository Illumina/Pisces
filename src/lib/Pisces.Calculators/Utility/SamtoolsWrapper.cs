using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Common.IO.Utility;
using Gemini.Interfaces;

namespace Gemini.Utility
{
    public class SamtoolsWrapper : ISamtoolsWrapper
    {
        private readonly string _samtoolsPath;
        private readonly bool _isWeirdSamtools;

        public SamtoolsWrapper(string samtoolsPath, bool isWeirdSamtools = false)
        {
            _samtoolsPath = samtoolsPath;
            _isWeirdSamtools = isWeirdSamtools;
        }

        public void SamtoolsSort(string originalBam, string sortedBam, int samtoolsThreadCount,
            int samtoolsMemoryMbPerThread, bool byName = false)
        {
            if (!File.Exists(originalBam))
            {
                throw new ArgumentException($"Cannot sort {originalBam}: file does not exist.");
            }

            var arguments = _isWeirdSamtools
                ? $"sort -@ {samtoolsThreadCount} -m {samtoolsMemoryMbPerThread}M {(byName ? "-n" : "")} \"{originalBam}\" \"{(sortedBam.EndsWith(".bam") ? sortedBam.Remove(sortedBam.Length - ".bam".Length) : sortedBam)}\""
                : $"sort -@ {samtoolsThreadCount} -m {samtoolsMemoryMbPerThread}M {(byName ? "-n" : "")} -o \"{sortedBam + ".bam"}\" \"{originalBam}\"";

            ExecuteSamtoolsProcess(arguments);
        }

        public void SamtoolsCat(string mergedBam, IEnumerable<string> inputBams)
        {
            if (File.Exists(mergedBam))
            {
                File.Move(mergedBam, mergedBam + "_old_" + DateTime.Now.Ticks);
            }

            //else
            {
                BatchCatBams(mergedBam + ".tmp", inputBams.ToList(), mergedBam);
            }
        }

        private string BatchCatBams(string mergedBam, IReadOnlyCollection<string> inputBams, string finalMergedBAm)
        {
            if (inputBams.Count() == 1)
            {
                var inputBam = inputBams.First();
                Logger.WriteToLog(
                    $"Skipping samtools cat due to single input bam, instead moving {inputBam} directly to output at {finalMergedBAm}");
                // Note: choosing to move instead of copy here because ultimately we end up deleting intermediate files anyway, unless the user is running in debug
                // If user is running in debug, they therefore won't see the original bam in their chrom subdir... but since there's only one original bam anyway, it was not providing any additional value
                File.Move(inputBam, finalMergedBAm);
                return finalMergedBAm;
            }

            var maxCmdLength = 1500;
            var currentCmdLength = inputBams.Sum(x => x.Length);

            if (currentCmdLength > maxCmdLength)
            {
                var bamsQueue = new Queue<string>(inputBams.ToList());
                var finalBams = new List<string>();
                var loopCmdLength = 0;
                var tmpBamCount = 0;
                var currentBams = new List<string>();

                while (bamsQueue.Count > 0)
                {
                    var nextBam = bamsQueue.Dequeue();
                    if (loopCmdLength + nextBam.Length > maxCmdLength)
                    {
                        tmpBamCount++;
                        var tmpMergedBam = mergedBam + ".tmp" + tmpBamCount;
                        finalBams.Add(tmpMergedBam);
                        Logger.WriteToLog($"Calling intermediate samtools cat on {currentBams.Count()} bams with output at {tmpMergedBam}");
                        ExecuteSamtoolsProcess(
                            $"cat -o \"{tmpMergedBam}\" {string.Join(" ", currentBams.Select(b => $"\"{b}\""))}");

                        loopCmdLength = 0;

                        foreach (var currentBam in currentBams)
                        {
                            File.Delete(currentBam);
                        }
                        currentBams.Clear();
                    }

                    currentBams.Add(nextBam);
                    loopCmdLength += nextBam.Length;
                }

                BatchCatBams(mergedBam + ".tmp", finalBams.Concat(currentBams).ToList(), finalMergedBAm);
            }
            else
            {
                Logger.WriteToLog($"Calling final samtools cat on {inputBams.Count()} bams with output at {finalMergedBAm}");
                ExecuteSamtoolsProcess(
                    $"cat -o \"{finalMergedBAm}\" {string.Join(" ", inputBams.Select(b => $"\"{b}\""))}");
            }
            return mergedBam;

        }

        public void SamtoolsIndex(string bamToIndex)
        {
            ExecuteSamtoolsProcess($"index -@ 8 \"{bamToIndex}\"");
        }

        private void ExecuteSamtoolsProcess(string arguments)
        {
            // TODO convert these to use the same CliTask code as other places (or otherwise converge on a single way to launch external processes).

            var samtoolsProcess = new Process();
            samtoolsProcess.StartInfo.UseShellExecute = false;
            samtoolsProcess.StartInfo.RedirectStandardError = true;
            samtoolsProcess.StartInfo.FileName = _samtoolsPath;
            samtoolsProcess.StartInfo.Arguments = arguments;
            samtoolsProcess.Start();
            while (!samtoolsProcess.StandardError.EndOfStream)
            {
                Logger.WriteToLog(samtoolsProcess.StandardError.ReadLine());
            }
            samtoolsProcess.WaitForExit();

            if (samtoolsProcess.ExitCode != 0)
            {
                throw new Exception($"Samtools process failed: {samtoolsProcess.StartInfo.FileName} with command {samtoolsProcess.StartInfo.Arguments}");
            }

        }
    }
}