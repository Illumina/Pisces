using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.IO.Utility;
using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;
using Gemini.Models;
using Gemini.Realignment;
using Gemini.Types;
using Gemini.Utility;

namespace Gemini.Logic
{
    public class CategorizedBamRealigner : ICategorizedBamRealigner
    {
        private readonly GeminiOptions _geminiOptions;
        private readonly GeminiSampleOptions _geminiSampleOptions;
        private readonly RealignmentOptions _realignmentOptions;
        private readonly IGeminiFactory _factory;
        private readonly BasicIndelFilterer _indelFilterer;
        private readonly IHashableIndelSource _hashableIndelSource;
        private readonly IndelPruner _indelPruner;
        private readonly IGeminiDataSourceFactory _dataSourceFactory;

        public CategorizedBamRealigner(GeminiOptions geminiOptions,
            GeminiSampleOptions geminiSampleOptions, RealignmentOptions realignmentOptions,
            IGeminiFactory factory, IGeminiDataSourceFactory dataSourceFactory)
        {
            _geminiOptions = geminiOptions;
            _geminiSampleOptions = geminiSampleOptions;
            _realignmentOptions = realignmentOptions;
            _factory = factory;
            _dataSourceFactory = dataSourceFactory;
            _indelFilterer = factory.GetIndelFilterer();
            _indelPruner = factory.GetIndelPruner();
            _hashableIndelSource = dataSourceFactory.GetHashableIndelSource();

        }

        public void RealignAroundCandidateIndels(Dictionary<string, int[]> indelStringLookup,  Dictionary<string, Dictionary<PairClassification, List<string>>> categorizedAlignments)
        {
            var indelsLookup = _indelFilterer.GetRealignablePreIndels(indelStringLookup, true);

            Logger.WriteToLog(
                $"Realigning against {indelsLookup.Values.Sum(x => x.Count)} out of {indelStringLookup.Keys.Count}.");

            foreach (var chrom in indelsLookup.Keys)
            {
                var preIndelsForChromosome = indelsLookup.ContainsKey(chrom) ? indelsLookup[chrom] : new List<PreIndel>();
                var indelsForChrom = _indelPruner.GetPrunedPreIndelsForChromosome(preIndelsForChromosome);
                Logger.WriteToLog(
                    $"After pruning for messy areas, realigning against {indelsForChrom.Count()} indels for chromosome '{chrom}'.");

                if (_geminiOptions.Debug)
                {
                    foreach (var candidateIndel in indelsForChrom.OrderBy(x => x.ReferencePosition))
                    {
                        Console.WriteLine(candidateIndel + " " + candidateIndel.Score);
                    }
                }

                var chromIndels = _hashableIndelSource.GetFinalIndelsForChromosome(chrom, indelsForChrom);

                var snowballedEvidences = new List<Dictionary<string, int[]>>();
                snowballedEvidences.Add(indelStringLookup);

                IChromosomeIndelSource indelSource;
                using (var snippetSource = _dataSourceFactory.CreateGenomeSnippetSource(chrom))
                {
                    indelSource = _dataSourceFactory.GetChromosomeIndelSource(chromIndels, snippetSource);
                }

                if (_realignmentOptions.CategoriesForSnowballing.Any())
                {
                    Snowball(categorizedAlignments, chrom, indelSource, snowballedEvidences);

                    indelSource = GetRefilteredIndelsBasedOnSnowballedEvidence(snowballedEvidences, chrom,
                        _hashableIndelSource);
                }

                Realign(categorizedAlignments, indelSource, chrom);
            }
        }

        private IChromosomeIndelSource GetRefilteredIndelsBasedOnSnowballedEvidence(List<Dictionary<string, int[]>> snowballedEvidences, string chrom, IHashableIndelSource hashableIndelSource)
        {
            IChromosomeIndelSource indelSource;
            var newIndels = _indelFilterer.MergeIndelEvidence(snowballedEvidences);

            Logger.WriteToLog($"Done with snowballing");

            var chromIndels = newIndels.ContainsKey(chrom) ? newIndels[chrom] : new List<PreIndel>();
            var newIndelsForChrom = _indelPruner.GetPrunedPreIndelsForChromosome(chromIndels);

            // Get chromosome
            Logger.WriteToLog(
                $"After pruning for messy areas, realigning against {newIndelsForChrom.Count()} indels for chromosome '{chrom}'.");

            if (_geminiOptions.Debug)
            {
                foreach (var candidateIndel in newIndelsForChrom.OrderBy(x => x.ReferencePosition))
                {
                    Console.WriteLine(candidateIndel + " " + candidateIndel.Score);
                }
            }

            // TODO do we really need to get these again?
            var newChromIndels = hashableIndelSource.GetFinalIndelsForChromosome(chrom, newIndelsForChrom);
            using (var snippetSource = _dataSourceFactory.CreateGenomeSnippetSource(chrom))
            {
                indelSource = _dataSourceFactory.GetChromosomeIndelSource(newChromIndels, snippetSource);;
            }

            return indelSource;
        }


        private void Snowball(Dictionary<string, Dictionary<PairClassification, List<string>>> categorizedAlignments, string chrom,
            IChromosomeIndelSource indelSource, List<Dictionary<string, int[]>> snowballedEvidences)
        {
            // TODO in some cases, indel realignment will remove indels that were originally seen, in favor of a better one (in particular in pair-aware). This isn't accounted for when we merge support after snowballing! In some ways it's ok because the alternative indel gets the additional support, which should help, but really it would be best to decrement the support for the one that was realigned away.
            foreach (var classification in _realignmentOptions.CategoriesForSnowballing)
            {
                var bamPaths = categorizedAlignments[chrom][classification];

                var outPaths = new List<string>();
                var
                    numBamsChecked =
                        0; // TODO if we're really going to do a partial snowballing (which maybe is too complicated anyway) wouldn't we maybe want to let the rest of the bams in the category benefit from the snoball?
                foreach (var bamPath in bamPaths)
                {
                    var outPath = GetRealignedOutputPath(bamPath, _geminiSampleOptions.OutputFolder);

                    Logger.WriteToLog($"Indels for {bamPath}: {indelSource.Indels.Count}");

                    if (indelSource.Indels.Any())
                    {
                        var realigner = _factory.GetRealigner(chrom,
                            numBamsChecked < _realignmentOptions.NumSubSamplesForSnowballing, indelSource);

                        var doStitch = !_geminiOptions.SkipStitching &&
                                       ClassificationIsStitchable(classification);
                        var alreadyStitched = ClassificationIsStitched(classification);

                        realigner.Execute(bamPath, outPath, doStitch, alreadyStitched,
                            _realignmentOptions.PairAwareEverything ||
                            ClassificationIsPairAwareRealignable(classification));

                        // TODO need to update mate with mate position !!!!! Urghhh. 
                        Logger.WriteToLog($"Completed realigning for {bamPath}. Output is at {outPath}.");

                        if (!_geminiOptions.KeepUnmergedBams)
                        {
                            Logger.WriteToLog($"Deleting file {bamPath} after realignment.");
                            File.Delete(bamPath);
                        }

                        var results = realigner.GetIndels();
                        snowballedEvidences.Add(results);

                        outPaths.Add(outPath);
                    }
                    else
                    {
                        outPaths.Add(bamPath);
                    }

                    numBamsChecked++;
                }

                categorizedAlignments[chrom][classification] = outPaths;
            }
        }

        private void Realign(
            Dictionary<string, Dictionary<PairClassification, List<string>>> categorizedAlignments,
            IChromosomeIndelSource indelSource, string chrom)
        {
            foreach (var classification in _realignmentOptions.CategoriesForRealignment.Except(_realignmentOptions
                .CategoriesForSnowballing))
            {
                var bamPaths = categorizedAlignments[chrom][classification];

                var outPaths = new List<string>();
                foreach (var bamPath in bamPaths)
                {
                    var outPath = GetRealignedOutputPath(bamPath, _geminiSampleOptions.OutputFolder);

                    Logger.WriteToLog($"Indels for {bamPath}: {indelSource.Indels.Count}");

                    if (indelSource.Indels.Any())
                    {
                        var realigner = _factory.GetRealigner(chrom, false, indelSource);

                        var doStitch = !_geminiOptions.SkipStitching && ClassificationIsStitchable(classification);
                        var alreadyStitched = ClassificationIsStitched(classification);

                        realigner.Execute(bamPath, outPath, doStitch, alreadyStitched,
                            _realignmentOptions.PairAwareEverything || ClassificationIsPairAwareRealignable(classification));

                        // TODO need to update mate with mate position !!!!! Urghhh. 
                        Logger.WriteToLog($"Completed realigning for {bamPath}. Output is at {outPath}.");

                        if (!_geminiOptions.KeepUnmergedBams)
                        {
                            Logger.WriteToLog($"Deleting file {bamPath} after realignment.");
                            File.Delete(bamPath);
                        }

                        outPaths.Add(outPath);
                    }
                    else
                    {
                        outPaths.Add(bamPath);
                    }
                }

                categorizedAlignments[chrom][classification] = outPaths;
            }
        }

        private static string GetRealignedOutputPath(string bamPath, string outFolder)
        {
            var realignableBamStub = Path.GetFileName(bamPath);
            var outPath = Path.Combine(outFolder, realignableBamStub + "_realigned");
            return outPath;
        }

        private static bool ClassificationIsStitched(PairClassification classification)
        {
            return classification == PairClassification.ImperfectStitched ||
                   classification == PairClassification.PerfectStitched ||
                   classification == PairClassification.MessyStitched;
        }

        private static bool ClassificationIsStitchable(PairClassification classification)
        {
            return classification == PairClassification.Disagree ||
                   classification == PairClassification.FailStitch ||
                   classification == PairClassification.UnstitchIndel || classification == PairClassification.UnstitchImperfect;
        }

        private static bool ClassificationIsPairAwareRealignable(PairClassification classification)
        {
            return classification == PairClassification.Disagree ||
                   classification == PairClassification.FailStitch ||
                   classification == PairClassification.UnstitchIndel;
        }

    }
}