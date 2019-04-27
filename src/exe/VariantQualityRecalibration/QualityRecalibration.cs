using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.Calculators;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.Domain.Models.Alleles;
using Common.IO.Utility;
using Pisces.IO;

namespace VariantQualityRecalibration
{
    public class QualityRecalibrationData
    {
        public Dictionary<MutationCategory, int> BasicLookupTable { get; set; }
        public Dictionary<MutationCategory, int> AmpliconEdgeVariantsLookupTable { get; set; }
        public Dictionary<string, List<int>> AmpliconEdgeVariantsList { get; set; }

        public Dictionary<MutationCategory, int> EdgeRiskLookupTable { get; set; }
    }

    public class QualityRecalibration
    {
        public static void Recalibrate(SignatureSorterResultFiles countsFilePaths, VQROptions options)
        {
            string vcfFileName = Path.GetFileName(options.VcfPath);
            string vcfOut = Path.Combine(options.OutputDirectory, vcfFileName + ".recal");

            if (File.Exists(vcfOut))
                File.Delete(vcfOut);

            try
            {
                //Read in the results files that have the data for the error modes detected.
                //Decide which types of variants we want to re-Qscore
                var recalibrationData = GetRecalibrationTables(countsFilePaths, options);

                //Update Vcf, variant by variant, based on the table data.
                VcfUpdater<QualityRecalibrationData>.UpdateVcfAlleleByAllele(vcfOut, options, false, recalibrationData, UpdateAllele, CanSkipVcfLines,
                    VQRVcfWriter.GetVQRVcfFileWriter);

                //let the user know it worked
                if (File.Exists(vcfOut))
                    Logger.WriteToLog("The following vcf was recalibrated: " + options.VcfPath);

            }
            catch (Exception ex)
            {
                Logger.WriteToLog("Recalibrate failed for " + options.VcfPath);
                Logger.WriteToLog("Exception: " + ex);
            }


        }


        private static QualityRecalibrationData GetRecalibrationTables(SignatureSorterResultFiles resultsFilePaths,
            VQROptions options)
        {

            CountData BasicCounts = null;
            CountData EdgeCounts = null;

            QualityRecalibrationData recalibrationData = new QualityRecalibrationData();

            if (options.DoBasicChecks)
            {


                if (!File.Exists(resultsFilePaths.BasicCountsFilePath))
                {
                    Logger.WriteToLog("Cannot do basic recalibration. Cannot find {0} ", resultsFilePaths.BasicCountsFilePath);
                }
                else
                {
                    Logger.WriteToLog("Found counts file: {0} ", resultsFilePaths.BasicCountsFilePath);

                    BasicCounts = CountsFileReader.ReadCountsFile(resultsFilePaths.BasicCountsFilePath);
                    recalibrationData.BasicLookupTable = GetPhredScaledCalibratedRates(options.BamFilterParams.MinimumBaseCallQuality, options.ZFactor, BasicCounts);


                    //if no work to do here...
                    if ((recalibrationData.BasicLookupTable == null) || (recalibrationData.BasicLookupTable.Count == 0))
                    {
                        Logger.WriteToLog("No general recalibration needed.");
                    }
                    else
                    {
                        Logger.WriteToLog("General mutation bias detected. This sample may have sample-specific prep issues such as FFPE or oxidation damage.");
                    }
                }
            }

            if (options.DoAmpliconPositionChecks)
            {

                if (!File.Exists(resultsFilePaths.AmpliconEdgeCountsFilePath))
                {
                    Logger.WriteToLog("Cannot do amplicon-position based recalibration. Cannot find {0} ", resultsFilePaths.AmpliconEdgeCountsFilePath);
                }
                else
                {
                    Logger.WriteToLog("Found counts file: {0} ", resultsFilePaths.AmpliconEdgeCountsFilePath);


                    EdgeCounts = CountsFileReader.ReadCountsFile(resultsFilePaths.AmpliconEdgeCountsFilePath);
                    recalibrationData.AmpliconEdgeVariantsLookupTable = GetPhredScaledCalibratedRates(options.BamFilterParams.MinimumBaseCallQuality, options.ZFactor, EdgeCounts);
                    recalibrationData.AmpliconEdgeVariantsList = VariantListReader.ReadVariantListFile(resultsFilePaths.AmpliconEdgeSuspectListFilePath);


                    if ((recalibrationData.AmpliconEdgeVariantsLookupTable == null) || (recalibrationData.AmpliconEdgeVariantsLookupTable.Count == 0))
                    {
                        Logger.WriteToLog("No position-in-amplicon recalibration needed.");
                    }
                }

            }

            //compare edge-issues with FFPE-like issues.
            //Did the bulk of variants appear to come from the edge of amplicons..?
            //Look at the diff in percents. 
            //If a variant is X more likely to be called when its by an edge - thats an estimate of the error.
            if (options.DoBasicChecks && options.DoAmpliconPositionChecks)
                recalibrationData.EdgeRiskLookupTable = GetPhredScaledCalibratedRatesForEdges(options.BamFilterParams.MinimumBaseCallQuality, options.AlignmentWarningThreshold, BasicCounts, EdgeCounts);


            return recalibrationData;
        }

        private static TypeOfUpdateNeeded UpdateAllele(VcfConsumerAppOptions appOptions, QualityRecalibrationData recalibrationData, CalledAllele inAllele, out List<CalledAllele> outAlleles)
        {
            outAlleles = new List<CalledAllele> { inAllele };
            VQROptions options = (VQROptions)appOptions;
            var cat = MutationCounter.GetMutationCategory(inAllele);
            TypeOfUpdateNeeded updateHappened = TypeOfUpdateNeeded.NoChangeNeeded;

            if (options.DoBasicChecks && recalibrationData.BasicLookupTable.ContainsKey(cat))
            {
                UpdateVariantQScoreAndRefilter(options.MaxQScore, options.VariantCallingParams.MinimumVariantQScoreFilter, recalibrationData.BasicLookupTable, inAllele, cat, false);
                updateHappened = TypeOfUpdateNeeded.Modify;
            }

            if (options.DoAmpliconPositionChecks
                && recalibrationData.AmpliconEdgeVariantsLookupTable.ContainsKey(cat)
                && recalibrationData.AmpliconEdgeVariantsList.ContainsKey(inAllele.Chromosome)
                && recalibrationData.AmpliconEdgeVariantsList[inAllele.Chromosome].Contains(inAllele.ReferencePosition))
            {
                UpdateVariantQScoreAndRefilter(options.MaxQScore, options.VariantCallingParams.MinimumVariantQScoreFilter, recalibrationData.EdgeRiskLookupTable, inAllele, cat, true);
                updateHappened = TypeOfUpdateNeeded.Modify;
            }

            return updateHappened;
        }


        public static TypeOfUpdateNeeded CanSkipVcfLines(List<string> originalVarStrings)
        {
            foreach (var s in originalVarStrings)
            {
                var lineResult = CanSkipVcfLine(s);

                if (lineResult == TypeOfUpdateNeeded.Modify)
                    return TypeOfUpdateNeeded.Modify;
            }

            return TypeOfUpdateNeeded.NoChangeNeeded;
        }
        public static TypeOfUpdateNeeded CanSkipVcfLine(string originalVarString)
        {
            var splat = originalVarString.Split();

            int refIndex = 3;
            int altIndex = 4;
            int filterIndex = 6;

            //skip all ref calls
            if (splat[altIndex] == ".")
                return TypeOfUpdateNeeded.NoChangeNeeded;

            //skip everything not a SNP
            if (splat[refIndex].Length > 1)
                return TypeOfUpdateNeeded.NoChangeNeeded;

            //skip everything not a SNP
            if (splat[altIndex].Length > 1)
                return TypeOfUpdateNeeded.NoChangeNeeded;

            //skip all ForcedReport variants, bc they are not real.
            if (splat[filterIndex].ToLower().Contains("ForcedReport"))
                return TypeOfUpdateNeeded.NoChangeNeeded;

            //if its a real SNP, we better check if we need to do somthing
            return TypeOfUpdateNeeded.Modify;
        }

        public static void UpdateVariantQScoreAndRefilter(int maxQscore, int filterQScore, Dictionary<MutationCategory, int> qCalibratedRates, CalledAllele originalVar, MutationCategory cat,
        bool subsample)
        {
            double depth;
            double callCount;


            //tjd+
            // We can revisit this math at a later date. Exactly how we lower the Qscores
            // or filter the suspect calls is TBD. We should work with this new algorithm
            // on a larger collection of datasets representing various alignment issues,
            // and modify the code/parameters as needed.
            // A few thoughts: Amplicon edge issues don't get better the deeper you sequence.
            // We need to scale out depth from the Q score calculation (since the original Q score is a fxn of depth/freq).
            // One option is to Cap at ~100 DP (TODO, this should be a parameter, a fxn of your bascallquality).
            // double subSampleToThis = 100;
            // tjd-

            double denominator = MathOperations.QtoP(qCalibratedRates[cat]);
            double subSampleToThis = 1.0 / denominator;

            if ((qCalibratedRates[cat] == 0) || (denominator == 0))
                subsample = false;

            bool canUpdateQ = HaveInfoToUpdateQ(originalVar, out depth, out callCount);

            if (subsample && (depth > subSampleToThis))
            {
                callCount = callCount * subSampleToThis / depth;
                depth = subSampleToThis;
            }

            if (canUpdateQ)
            {
                int newQ = VariantQualityCalculator.AssignPoissonQScore(
                   (int)callCount, (int)depth, qCalibratedRates[cat], Math.Min(originalVar.VariantQscore, maxQscore));  //note, using "originalVar.VariantQscore" and the maxQ stops us from ever RAISING the q score over these values.

                InsertNewQ(qCalibratedRates, originalVar, cat, newQ, true);

                //update filters if needed
                if (newQ < filterQScore)
                {
                    if (originalVar.Filters.Contains(FilterType.LowVariantQscore))
                        return;

                    originalVar.Filters.Add(FilterType.LowVariantQscore);

                }
            }
        }

        public static void InsertNewQ(Dictionary<MutationCategory, int> qCalibratedRates, CalledAllele originalVar, MutationCategory cat, int newQ, bool isSomatic)
        {
            originalVar.VariantQscore = newQ;
            originalVar.NoiseLevelApplied = qCalibratedRates[cat];

            if (isSomatic)
                originalVar.GenotypeQscore = newQ;
        }

        public static bool HaveInfoToUpdateQ(CalledAllele originalVar, out double depth, out double callCount)
        {
            //cases where we dont know what to do:
            depth = -1;
            callCount = -1;

            if (originalVar.VariantQscore < 1)
                return false;

            if ((originalVar.Type == AlleleCategory.Unsupported) || (originalVar.Type == AlleleCategory.NonReference))
                return false;


            //otherwise, we should handle it:
            depth = originalVar.TotalCoverage;
            callCount = originalVar.AlleleSupport;
            return true;
        }

        private static
          Dictionary<MutationCategory, int> GetPhredScaledCalibratedRatesForEdges(int baselineQNoise, double warningThreshold, CountData basicCountsData, CountData edgeIssueCountsData)
        {

            Dictionary<MutationCategory, int> AdjustedErrorRates = new Dictionary<MutationCategory, int>();
            double GeneralEdgeRiskIncrease = edgeIssueCountsData.ObservedMutationRate / basicCountsData.ObservedMutationRate;

            if (GeneralEdgeRiskIncrease > warningThreshold)
            {
                Logger.WriteToLog("Warning, high levels of mismatches detected at loci near edges, relative to all other loci, by a factor of " + GeneralEdgeRiskIncrease);
            }

            double MutationRateInEdge = edgeIssueCountsData.ObservedMutationRate;

            double MuationsCalledNotInEdge = basicCountsData.TotalMutations - edgeIssueCountsData.TotalMutations;
            double TotalLociNotInEdge = basicCountsData.NumPossibleVariants - edgeIssueCountsData.NumPossibleVariants;

            double MutationRateNotInEdge = MuationsCalledNotInEdge / TotalLociNotInEdge;

            //if the error rate at the edges was the same as the error rate in the middle,
            // we would expect this many variants at the edges:      
            double NullHypothesisExpectedMismatches = MutationRateNotInEdge * edgeIssueCountsData.NumPossibleVariants;

            double HowManyVariantsWeActuallySaw = edgeIssueCountsData.TotalMutations;

            double HowManyAreProbablyWrong = edgeIssueCountsData.TotalMutations - NullHypothesisExpectedMismatches;

            //error rate in edge region, Given You Called a Variant.
            double EstimatedErrorRateInEdgeRegions = HowManyAreProbablyWrong / edgeIssueCountsData.TotalMutations;


            foreach (var cat in edgeIssueCountsData.CountsByCategory.Keys)
            {
                double countsAtEdge = edgeIssueCountsData.CountsByCategory[cat];
                double overallMutations = edgeIssueCountsData.TotalMutations;

                double proportion = countsAtEdge / edgeIssueCountsData.TotalMutations;

                //how much this particular variant category contributed to the error rate increase
                double estimatedErrorRateByCategory = proportion * EstimatedErrorRateInEdgeRegions;
                int riskAsQRate = (int)MathOperations.PtoQ(estimatedErrorRateByCategory);
                AdjustedErrorRates.Add(cat, riskAsQRate);
            }

            return AdjustedErrorRates;
        }

        private static
        Dictionary<MutationCategory, int> GetPhredScaledCalibratedRates(int baselineQNoise, double zFactor, CountData counts)
        {
            double baseNoiseRate = MathOperations.QtoP(baselineQNoise);

            var countsByCategory = counts.CountsByCategory;
            var PhredScaledRatesByCategory = new Dictionary<MutationCategory, int>();

            countsByCategory.Remove(MutationCategory.Deletion);
            countsByCategory.Remove(MutationCategory.Insertion);
            countsByCategory.Remove(MutationCategory.Other);
            countsByCategory.Remove(MutationCategory.Reference);

            double[] sortedFinalCounts = countsByCategory.Values.OrderBy(d => d).ToArray();

            if (countsByCategory.Keys.Count != 12)
                return null;

            //take the average value, throwing out the top two and bottom two outlyiers.
            double numDataPoints = 8; //12 - 4;
            double avg = 0;
            for (int i = 2; i < 10; i++)
                avg += (sortedFinalCounts[i] / numDataPoints);

            //get the variance
            double variance = 0;
            for (int i = 2; i < 10; i++)
                variance += ((avg - sortedFinalCounts[i]) * (avg - sortedFinalCounts[i]) / numDataPoints);

            //threshold = avg + z * sigma
            double threshold = avg + zFactor * Math.Sqrt(variance);

            foreach (var cat in countsByCategory.Keys)
            {

                double mutationCount = countsByCategory[cat];

                if (mutationCount > threshold)
                {

                    //baseline noise level is 'b' .
                    //the observed transition-rate is how frequently we observe a vf >b.

                    //so our expected freq due to noise =
                    // (prob of observation f_i <= b) + (prob of observation f_i > b)

                    double observedNoiseRate = 0;
                    if (counts.NumPossibleVariants > 0)
                        observedNoiseRate = mutationCount / counts.NumPossibleVariants;

                    //deliberately taking floor instead of rounding.
                    PhredScaledRatesByCategory.Add(cat, (int)(MathOperations.PtoQ(observedNoiseRate + baseNoiseRate)));

                }
            }

            return PhredScaledRatesByCategory;

        }
    }
}
