using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.IO.Utility;
using Gemini.Interfaces;
using Gemini.Types;
using Gemini.Utility;

namespace Gemini
{
    public class GeminiWorkflow
    {
        private readonly IGeminiDataSourceFactory _dataSourceFactory;
        private readonly IGeminiFactory _geminiFactory;
        private readonly IGeminiDataOutputFactory _dataOutputFactory;
        private readonly GeminiOptions _geminiOptions;
        private readonly GeminiSampleOptions _geminiSampleOptions;

        public GeminiWorkflow(IGeminiDataSourceFactory dataSourceFactory, IGeminiFactory geminiFactory, IGeminiDataOutputFactory dataOutputFactory, GeminiOptions geminiOptions, GeminiSampleOptions geminiSampleOptions, RealignmentOptions realignmentOptions)
        {
            _dataSourceFactory = dataSourceFactory;
            _geminiFactory = geminiFactory;
            _dataOutputFactory = dataOutputFactory;
            _geminiOptions = geminiOptions;
            _geminiSampleOptions = geminiSampleOptions;
        }

        public void Execute()
        {
            var samtoolsWrapper = _geminiFactory.GetSamtoolsWrapper();

            // Get Evidence and Categorize Alignments
            var categorizedBamAndIndelEvidenceSource = _geminiFactory.GetCategorizationAndEvidenceSource();
            categorizedBamAndIndelEvidenceSource.CollectAndCategorize(_dataSourceFactory, _dataOutputFactory);
            var indelStringLookup = categorizedBamAndIndelEvidenceSource.GetIndelStringLookup();
            var categorizedAlignments = categorizedBamAndIndelEvidenceSource.GetCategorizedAlignments();

            Logger.WriteToLog($"Found {indelStringLookup.Keys.Count} total indels.");
            WriteIndelsCsv(_geminiSampleOptions.OutputFolder, _geminiOptions.IndelsCsvName, indelStringLookup);

            // Realign
            var realigner = _geminiFactory.GetCategorizedBamRealigner();
            if (!_geminiOptions.StitchOnly)
            {
                realigner.RealignAroundCandidateIndels(indelStringLookup, categorizedAlignments);
            }

            // Finalize
            MergeAndFinalizeBam(categorizedAlignments, samtoolsWrapper, _geminiSampleOptions.OutputFolder,
                _geminiOptions.KeepUnmergedBams, indexFinalBam: _geminiSampleOptions.RefId == null || _geminiOptions.IndexPerChrom);
        }

        private static string MergeAndFinalizeBam(List<string> finalBams, ISamtoolsWrapper samtoolsWrapper, string outFolder, string outFileName, bool keepUnmerged = false, bool doIndex = false)
        {
            var mergedBam = Path.Combine(outFolder, outFileName);
            var sortedMerged = mergedBam + ".sorted";
            var sortedMergedFinal = sortedMerged + ".bam";

            Logger.WriteToLog($"Calling cat with output at {mergedBam}.");
            samtoolsWrapper.SamtoolsCat(mergedBam, finalBams);
            Logger.WriteToLog($"Calling samtools sort on {mergedBam}.");
            samtoolsWrapper.SamtoolsSort(mergedBam, sortedMerged, 3, 50);

            if (doIndex)
            {
                Logger.WriteToLog($"Calling samtools index on {sortedMergedFinal}.");
                samtoolsWrapper.SamtoolsIndex(sortedMergedFinal);
            }

            Logger.WriteToLog("Done finalizing bam.");

            if (File.Exists(sortedMergedFinal) && !keepUnmerged)
            {
                Logger.WriteToLog("Deleting intermediate bams.");
                foreach (var finalBam in finalBams)
                {
                    File.Delete(finalBam);
                }
                File.Delete(mergedBam);

                Logger.WriteToLog("Finished deleting intermediate bams.");
            }

            return sortedMergedFinal;
        }

        private static void MergeAndFinalizeBam(Dictionary<string, Dictionary<PairClassification, List<string>>> categorizedAlignments, ISamtoolsWrapper samtoolsWrapper, string outFolder, bool keepUnmerged = false, bool indexFinalBam = false)
        {
            //var finalBams = categorizedAlignments.Values.SelectMany(x => x.Values).ToList();
            var finalBams = new List<string>();

            if (categorizedAlignments.Keys.Count > 1)
            {
                foreach (var chrom in categorizedAlignments.Keys)
                {
                    var chromFinalBams = categorizedAlignments[chrom].Values;
                    if (!chromFinalBams.Any())
                    {
                        Logger.WriteToLog($"No bams to merge for chromosome {chrom}.");
                        continue;
                    }
                    var sortedMergedChromBam = MergeAndFinalizeBam(chromFinalBams.SelectMany(x=>x).ToList(), samtoolsWrapper, outFolder, chrom + "_merged.bam", keepUnmerged, false);
                    finalBams.Add(sortedMergedChromBam);
                }
            }
            else
            {
                if (categorizedAlignments.Keys.Any())
                {
                    var chrom = categorizedAlignments.Keys.First();
                    finalBams = categorizedAlignments[chrom].Values.SelectMany(x => x).ToList();
                }
                else
                {
                    Logger.WriteToLog("No chromosomes with bams to merge.");
                }
            }

            if (finalBams.Any())
            {
                MergeAndFinalizeBam(finalBams, samtoolsWrapper, outFolder, "merged.bam", keepUnmerged, indexFinalBam);
            }
        }


        private void WriteIndelsCsv(string outFolder, string indelsCsvName, Dictionary<string, int[]> indelStringLookup)
        {
            using (var writer = _dataOutputFactory.GetTextWriter(Path.Combine(outFolder, indelsCsvName)))
            {
                foreach (var kvp in indelStringLookup)
                {
                    writer.WriteLine(kvp.Key + "," + string.Join(",", kvp.Value));
                }
            }
        }
    }
}