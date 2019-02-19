using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.IO.Sequencing;
using Pisces.Calculators;
using Pisces.Domain.Types;
using Common.IO.Utility;
using Common.IO;
using Pisces.IO;

namespace VariantQualityRecalibration
{
    public class QualityRecalibration
    {
        public static void Recalibrate(SignatureSorterResultFiles countsFilePaths, VQROptions options)
        {
            string vcfFileName = Path.GetFileName(options.InputVcf);
            string vcfOut = Path.Combine(options.OutputDirectory, vcfFileName + ".recal");

            if (File.Exists(vcfOut))
                File.Delete(vcfOut);

            try
            {
                DoRecalibrationWork(vcfOut, countsFilePaths, options);


                if (File.Exists(vcfOut))
                {
                    Logger.WriteToLog("The following vcf was recalibrated: " + options.InputVcf);
                }

            }
            catch (Exception ex)
            {
                Logger.WriteToLog("Recalibrate failed for " + options.InputVcf);
                Logger.WriteToLog("Exception: " + ex);
            }


        }


        private static void DoRecalibrationWork(string vcfOut, SignatureSorterResultFiles resultsFilePaths, 
            VQROptions options)
        {
           
            CountData BasicCounts = null;
            CountData EdgeCounts = null;

            Dictionary<MutationCategory, int> BasicLookupTable = null;
            Dictionary<MutationCategory, int> AmpliconEdgeVariantsLookupTable = null;
            Dictionary<string, List<int>> AmpliconEdgeVariantsList = null;

            Dictionary<MutationCategory, int> EdgeRiskLookupTable = null;

            if (options.DoBasicChecks)
            {


                if (!File.Exists(resultsFilePaths.BasicCountsFilePath))
                {
                    Logger.WriteToLog("Cannot do basic recalibration. Cannot find {0} ", resultsFilePaths.BasicCountsFilePath);
                    return;
                }
                else
                {
                    Logger.WriteToLog("Found counts file: {0} ", resultsFilePaths.BasicCountsFilePath);
                }

                BasicCounts = CountsFileReader.ReadCountsFile(resultsFilePaths.BasicCountsFilePath);
                BasicLookupTable = GetPhredScaledCalibratedRates(options.BaseQNoise, options.ZFactor, BasicCounts);

                //if no work to do here...
                if ((BasicLookupTable == null) || (BasicLookupTable.Count == 0))
                {
                    Logger.WriteToLog("No general recalibration needed.");
                    return;
                }
                else
                {
                    Logger.WriteToLog("General mutation bias detected. This sample may have sample-specific prep issues such as FFPE or oxidation damage.");
                }
            }

            if (options.DoAmpliconPositionChecks)
            {

                if (!File.Exists(resultsFilePaths.AmpliconEdgeCountsFilePath))
                {
                    Logger.WriteToLog("Cannot do amplicon-position based recalibration. Cannot find {0} ", resultsFilePaths.AmpliconEdgeCountsFilePath);
                    return;
                }
                else
                {
                    Logger.WriteToLog("Found counts file: {0} ", resultsFilePaths.AmpliconEdgeCountsFilePath);
                }

                EdgeCounts = CountsFileReader.ReadCountsFile(resultsFilePaths.AmpliconEdgeCountsFilePath);
                AmpliconEdgeVariantsLookupTable = GetPhredScaledCalibratedRates(options.BaseQNoise, options.ZFactor, EdgeCounts);
                AmpliconEdgeVariantsList = VariantListReader.ReadVariantListFile(resultsFilePaths.AmpliconEdgeSuspectListFilePath);



                if ((AmpliconEdgeVariantsLookupTable == null) || (AmpliconEdgeVariantsLookupTable.Count == 0))
                {
                    Logger.WriteToLog("No position-in-amplicon recalibration needed.");
                    return;
                }
              
            }

            //compare edge-issues with FFPE-like issues.
            //Did the bulk of variants appear to come from the edge of amplicons..?
            //Look at the diff in percents. 
            //If a variant is X more likely to be called when its by an edge - thats an estimate of the error.
            if (options.DoBasicChecks && options.DoAmpliconPositionChecks)
            {

               EdgeRiskLookupTable = GetPhredScaledCalibratedRatesForEdges(options.BaseQNoise, options.AlignmentWarningThreshold, BasicCounts, EdgeCounts);
            }

            using (VcfReader reader = new VcfReader(options.InputVcf))
            using (VcfRewriter writer = new VcfRewriter(vcfOut))
            {
                writer.WriteHeader(reader.HeaderLines, options.QuotedCommandLineArgumentsString);

                var originalVar = new VcfVariant();
                while (reader.GetNextVariant(originalVar))
                {

                    var cat = MutationCounter.GetMutationCategory(originalVar);

                    if (options.DoBasicChecks && BasicLookupTable.ContainsKey(cat))
                    {
                        UpdateVariant(options.MaxQScore, options.FilterQScore, BasicLookupTable, originalVar, cat, false);
                    }

                    if (options.DoAmpliconPositionChecks
                        && AmpliconEdgeVariantsLookupTable.ContainsKey(cat) 
                        && AmpliconEdgeVariantsList.ContainsKey(originalVar.ReferenceName)
                        && AmpliconEdgeVariantsList[originalVar.ReferenceName].Contains(originalVar.ReferencePosition))
                    {
                        
                        UpdateVariant(options.MaxQScore, options.FilterQScore, EdgeRiskLookupTable, originalVar, cat, true);
                    }

                    writer.WriteVariantLine(originalVar);
                }

            }
        }

        public static void UpdateVariant(int maxQscore, int filterQScore, Dictionary<MutationCategory, int> qCalibratedRates, VcfVariant originalVar, MutationCategory cat,
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

            if ((qCalibratedRates[cat] == 0) || (denominator ==0))
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
                   (int) callCount, (int) depth, qCalibratedRates[cat], maxQscore);

                InsertNewQ(qCalibratedRates, originalVar, cat, newQ);
               
                //update filters if needed
                if (newQ < filterQScore)            
                {
                    var vcfConfig = new VcfWriterConfig();
                    vcfConfig.VariantQualityFilterThreshold = filterQScore;
                    var formatter = new VcfFormatter(vcfConfig );
                    
                    string lowQString = formatter.MapFilter(FilterType.LowVariantQscore);
                    string passString = VcfFormatter.PassFilter;

                    if (originalVar.Filters.Contains(lowQString))
                        return;

                    if (originalVar.Filters == passString)
                        originalVar.Filters = lowQString;
                    else
                        originalVar.Filters += VcfFormatter.FilterSeparator + lowQString;

                }
            }
        }

        public static void InsertNewQ(Dictionary<MutationCategory, int> qCalibratedRates, VcfVariant originalVar, MutationCategory cat, int newQ)
        {
            originalVar.Quality = newQ;
            if (originalVar.Genotypes[0].ContainsKey("GQ"))
                originalVar.Genotypes[0]["GQ"] = newQ.ToString();
            if (originalVar.Genotypes[0].ContainsKey("GQX"))
                originalVar.Genotypes[0]["GQX"] = newQ.ToString();
            if (originalVar.Genotypes[0].ContainsKey("NL"))
                originalVar.Genotypes[0]["NL"] = qCalibratedRates[cat].ToString();
        }

        public static bool HaveInfoToUpdateQ(VcfVariant originalVar, out double depth, out double callCount)
        {
            bool canUpdateQ = false;
            depth = -1;
            callCount = -1;

            if ((originalVar.InfoFields == null) || (originalVar.Genotypes == null)
                || (originalVar.Genotypes.Count < 1))
                return false;

            if (originalVar.InfoFields.ContainsKey("DP"))
                canUpdateQ = double.TryParse(originalVar.InfoFields["DP"], out depth);

            if (originalVar.Genotypes[0].ContainsKey("AD"))
            {
                string[] spat = originalVar.Genotypes[0]["AD"].Split(',');

                if (spat.Length == 2)
                    canUpdateQ = (canUpdateQ && double.TryParse(spat[1], out callCount));
            }

            return canUpdateQ;
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

            double MuationsCalledNotInEdge = basicCountsData.TotalMutations -edgeIssueCountsData.TotalMutations;
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
                int riskAsQRate = (int) MathOperations.PtoQ(estimatedErrorRateByCategory);
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
