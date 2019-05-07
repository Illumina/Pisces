using System.Collections.Generic;
using System.IO;
using System.Linq;
using BamStitchingLogic;
using Common.IO.Utility;
using Gemini.IndelCollection;
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
        private readonly BamRealignmentFactory _bamRealignmentFactory;
        private readonly StitcherOptions _stitcherOptions;
        private readonly RealignmentOptions _realignmentOptions;
        private readonly ISamtoolsWrapper _samtoolsWrapper;

        public GeminiWorkflow(IGeminiDataSourceFactory dataSourceFactory, 
            IGeminiDataOutputFactory dataOutputFactory, GeminiOptions geminiOptions,
            GeminiSampleOptions geminiSampleOptions, RealignmentOptions realignmentOptions, StitcherOptions stitcherOptions, string outputDirectory, RealignmentAssessmentOptions realignmentAssessmentOptions, IndelFilteringOptions indelFilteringOptions, ISamtoolsWrapper samtoolsWrapper)
        {
            _dataSourceFactory = dataSourceFactory;
            _dataOutputFactory = dataOutputFactory;
            _geminiOptions = geminiOptions;
            _geminiSampleOptions = geminiSampleOptions;
            _realignmentOptions = realignmentOptions;
            _samtoolsWrapper = samtoolsWrapper;
            _stitcherOptions = stitcherOptions ?? new StitcherOptions();

            _geminiFactory = new GeminiFactory(geminiOptions, indelFilteringOptions);
            var bamRealignmentFactory = new BamRealignmentFactory(geminiOptions,
                realignmentAssessmentOptions, stitcherOptions, realignmentOptions,
                outputDirectory);

            _bamRealignmentFactory = bamRealignmentFactory;


        }

        public void Execute()
          {       

            var refIdMapping = _dataSourceFactory.GetRefIdMapping(_geminiSampleOptions.InputBam);

            var blockFactorySource = new BlockFactorySource(_stitcherOptions, _geminiOptions, refIdMapping, _bamRealignmentFactory, _dataSourceFactory, _geminiSampleOptions, _realignmentOptions, _geminiFactory);
            var evalresults = new DataflowReadEvaluator(_geminiOptions, _dataSourceFactory, _geminiSampleOptions, _dataOutputFactory, blockFactorySource).ProcessBam();
            WriteIndelsCsv(_geminiSampleOptions.OutputFolder, _geminiOptions.IndelsCsvName,
                evalresults.IndelEvidence);
            MergeAndFinalizeBam(evalresults.CategorizedBams, _samtoolsWrapper, _geminiSampleOptions.OutputFolder,
                _geminiOptions.KeepUnmergedBams,
                indexFinalBam: _geminiSampleOptions.RefId == null || _geminiOptions.IndexPerChrom,  doSort: _geminiSampleOptions.RefId == null || _geminiOptions.SortPerChrom);

        }
        
        private static string MergeAndFinalizeBam(List<string> finalBams, ISamtoolsWrapper samtoolsWrapper, string outFolder, string outFileName, bool keepUnmerged = false, bool doIndex = false, bool doSort = true)
        {
            var mergedBam = Path.Combine(outFolder, outFileName);
            var sortedMerged = mergedBam + ".sorted";
            var sortedMergedFinal = sortedMerged + ".bam";

            Logger.WriteToLog($"Calling cat on {finalBams.Count} with output at {mergedBam}.");
            foreach (var finalBam in finalBams)
            {
                Logger.WriteToLog($"Intermediate bam: {finalBam}\t{new FileInfo(finalBam).Length}B");
            }
            samtoolsWrapper.SamtoolsCat(mergedBam, finalBams);
            if (doSort)
            {
                Logger.WriteToLog($"Calling samtools sort on {mergedBam}.");
                samtoolsWrapper.SamtoolsSort(mergedBam, sortedMerged, 3, 500);
            }

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

        private static void MergeAndFinalizeBam(Dictionary<string, Dictionary<PairClassification, List<string>>> categorizedAlignments, ISamtoolsWrapper samtoolsWrapper, string outFolder, bool keepUnmerged = false, bool indexFinalBam = false, bool doSort = true)
        {
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
                    var sortedMergedChromBam = MergeAndFinalizeBam(chromFinalBams.SelectMany(x=>x).ToList(), samtoolsWrapper, outFolder, chrom + "_merged.bam",  keepUnmerged: keepUnmerged, doIndex: false, doSort: doSort);
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
                MergeAndFinalizeBam(finalBams, samtoolsWrapper, outFolder, "merged.bam", keepUnmerged, indexFinalBam, doSort: doSort);
            }
        }


        private void WriteIndelsCsv(string outFolder, string indelsCsvName, Dictionary<string, IndelEvidence> indelStringLookup)
        {
            using (var writer = _dataOutputFactory.GetTextWriter(Path.Combine(outFolder, indelsCsvName)))
            {
                writer.WriteLine("Indel,Observations,LeftAnchor,RightAnchor,Mess,Quality,Forward,Reverse,Stitched,ReputableSupport,IsRepeat,IsSplit,Outcome");
                foreach (var kvp in indelStringLookup)
                {
                    writer.WriteLine(kvp.Key + "," + kvp.Value.ToString());
                }
            }
        }
    }
}