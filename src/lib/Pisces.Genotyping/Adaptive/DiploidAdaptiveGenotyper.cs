using System;
using System.Linq;
using System.Collections.Generic;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Genotyping
{

    public class DiploidAdaptiveGenotyper : IGenotypeCalculator
    {
        private readonly AdaptiveGenotypingParameters _adaptiveGenotypingParameters;
        public int MinGQScore { get; set; }
        public int MaxGQScore { get; set; }
        public int MinDepthToGenotype { get; set; }
        public float MinVarFrequency { get; set; }
        public float MinVarFrequencyFilter { get; set; }
        public PloidyModel PloidyModel { get; } = PloidyModel.DiploidByAdaptiveGT;
        public void SetMinFreqFilter(float minFreqFilter)
        {
            //update the defaults to match cmd line inputs given
            MinVarFrequencyFilter = minFreqFilter > MinVarFrequency ? minFreqFilter : MinVarFrequency;
        }


        public DiploidAdaptiveGenotyper()
        {
            var defaultParams = new VariantCallingParameters();
            _adaptiveGenotypingParameters = new AdaptiveGenotypingParameters();
            MinGQScore = defaultParams.MinimumGenotypeQScore;
            MaxGQScore = defaultParams.MaximumGenotypeQScore;
            MinDepthToGenotype = defaultParams.MinimumCoverage;
        }

        public DiploidAdaptiveGenotyper(int minCalledVariantDepth, int minGQscore, int maxGQscore,
            AdaptiveGenotypingParameters adaptiveGenotypingParameters)
        {
            MinDepthToGenotype = minCalledVariantDepth;
            MinGQScore = minGQscore;
            MaxGQScore = maxGQscore;
            _adaptiveGenotypingParameters = adaptiveGenotypingParameters;
        }

        public List<CalledAllele> SetGenotypes(IEnumerable<CalledAllele> alleles)
        {
            var singleGTForLoci = CalculateDiploidGenotypeFromBinomialModel(alleles, MinDepthToGenotype,
                _adaptiveGenotypingParameters, out var allelesToPrune);

            int phaseSetIndex = 1;  //reserve -1 for unset, and 0 for reference, and 1 and 2 for alts
            foreach (var allele in alleles)
            {
                allele.Genotype = singleGTForLoci;

                if (allele.TotalCoverage == 0)
                {
                    allele.GenotypeQscore = MinGQScore;
                    allele.GenotypePosteriors = new float[] {
                        _adaptiveGenotypingParameters.MaxGenotypePosteriors,
                        _adaptiveGenotypingParameters.MaxGenotypePosteriors,
                        _adaptiveGenotypingParameters.MaxGenotypePosteriors };
                }
                else
                {
                    var (model, prior) = _adaptiveGenotypingParameters.GetModelsAndPriors(allele);
                    MixtureModelResult adaptiveGTResult = AdaptiveGenotyperCalculator.GetGenotypeAndQScore(allele,
                            model, prior);

                    allele.GenotypeQscore = Math.Max(Math.Min(adaptiveGTResult.QScore, MaxGQScore), MinGQScore);
                    allele.GenotypePosteriors = adaptiveGTResult.GenotypePosteriors;
                }

                if (allele.IsRefType)
                {
                    allele.PhaseSetIndex = 0;
                }
                else
                {
                    allele.PhaseSetIndex = phaseSetIndex;
                    phaseSetIndex++;
                }
            }

            // Calculate GP for multiallelic variant based on multinomial distribution
            if (alleles.First().Genotype == Genotype.HeterozygousAlt1Alt2)
            {

                CalledAllele allele1 = alleles.First(), allele2 = alleles.ElementAt(1);

                var (model1, _) = _adaptiveGenotypingParameters.GetModelsAndPriors(allele1);
                var (model2, _) = _adaptiveGenotypingParameters.GetModelsAndPriors(allele2);
                var mixtureModelResult = AdaptiveGenotyperCalculator.GetMultiAllelicQScores(allele1, allele2,
                    new List<double[]> { model1, model2 });

                foreach (CalledAllele allele in alleles)
                {
                    allele.GenotypeQscore = Math.Max(Math.Min(mixtureModelResult.QScore, MaxGQScore), MinGQScore);
                    allele.GenotypePosteriors = mixtureModelResult.GenotypePosteriors;
                }
            }

            return allelesToPrune;
        }

        private static Genotype CalculateDiploidGenotypeFromBinomialModel(
            IEnumerable<CalledAllele> alleles,
            int minDepthToGenotype,
            AdaptiveGenotypingParameters adaptiveGenotypingParameters,
            out List<CalledAllele> allelesToPrune)
        {
            allelesToPrune = new List<CalledAllele>();

            float minVariantFrequency = GetMinVarFrequency(alleles.First().TotalCoverage,
                adaptiveGenotypingParameters.SnvModel, adaptiveGenotypingParameters.SnvPrior);
            double referenceFrequency = GetReferenceFrequency(alleles);
            bool depthIssue = GenotypeCalculatorUtilities.CheckForDepthIssue(alleles, minDepthToGenotype);
            bool refExists = referenceFrequency > minVariantFrequency;

            List<CalledAllele> orderedVariants = GenotypeCalculatorUtilities.FilterAndOrderAllelesByFrequency(alleles,
                allelesToPrune, minVariantFrequency);
            bool refCall = orderedVariants.Count == 0;

            //assume its ref call
            var preliminaryGenotype = SimplifiedDiploidGenotype.HomozygousRef;

            //this is order by descending - so most frequent is first.
            if (!refCall)
            {
                //do we apply SNP threshholds or indel thresholds?
                var dominantVariant = orderedVariants[0];
                var (model, priors) = adaptiveGenotypingParameters.GetModelsAndPriors(dominantVariant);

                preliminaryGenotype = AdaptiveGenotyperCalculator.GetSimplifiedGenotype(dominantVariant, model, priors);
                minVariantFrequency = GetMinVarFrequency(dominantVariant.TotalCoverage, model, priors);
            }

            var finalGTForLoci = GenotypeCalculatorUtilities.ConvertSimpleGenotypeToComplexGenotype(alleles, orderedVariants,
                referenceFrequency, refExists, depthIssue, refCall, minVariantFrequency,
                adaptiveGenotypingParameters.SumVFforMultiAllelicSite, preliminaryGenotype);

            allelesToPrune = GenotypeCalculatorUtilities.GetAllelesToPruneBasedOnGTCall(finalGTForLoci, orderedVariants, allelesToPrune);

            return finalGTForLoci;
        }

        private static double GetReferenceFrequency(IEnumerable<CalledAllele> alleles)
        {
            double referenceFrequency = 1;

            foreach (CalledAllele allele in alleles)
            {
                if (allele.Type == AlleleCategory.Reference)
                    return allele.Frequency;

                referenceFrequency = referenceFrequency - allele.Frequency;
            }

            return Math.Max(referenceFrequency, 0);
        }

        /// <summary>
        /// This is the analytical solution what is the threshold variant frequency for given depth and model parameters.
        /// For typical model parameters this usually is around 0.18
        /// </summary>
        private static float GetMinVarFrequency(int n, double[] model, double[] priors)
        {
            var mu1 = model[0];
            var mu2 = model[1];
            var prior1 = priors[0];
            var prior2 = priors[1];

            var minVq = (Math.Log(prior2) - Math.Log(prior1) - n * Math.Log(1 - mu1) + n * Math.Log(1 - mu2)) /
                        (Math.Log(mu1) - Math.Log(1 - mu1) - Math.Log(mu2) + Math.Log(1 - mu2)) / n;

            return (float)minVq;
        }
    }
}
