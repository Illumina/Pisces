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

        AdaptiveGenotypingParameters _adaptiveGenotypingParameters;
        PloidyModel _ploidyModel = PloidyModel.DiploidByAdaptiveGT;

        public int MinGQScore { get; set; }
        public int MaxGQScore { get; set; }
        public int MinDepthToGenotype { get; set; }
        public float MinVarFrequency { get; set; }
        public float MinVarFrequencyFilter { get; set; }
        public void SetMinFreqFilter(float minFreqFilter)
        {
            //update the defaults to match cmd line inputs given
            MinVarFrequencyFilter = minFreqFilter > MinVarFrequency ? minFreqFilter : MinVarFrequency;
            _adaptiveGenotypingParameters.MinVarFrequency = MinVarFrequencyFilter;
        }


        public DiploidAdaptiveGenotyper()
        {
            var defaultParams = new VariantCallingParameters();
            _adaptiveGenotypingParameters = new AdaptiveGenotypingParameters();
            MinGQScore = defaultParams.MinimumGenotypeQScore;
            MaxGQScore = defaultParams.MaximumGenotypeQScore;
            MinDepthToGenotype = defaultParams.MinimumCoverage;
            MinVarFrequency = _adaptiveGenotypingParameters.MinVarFrequency;
        }

        public DiploidAdaptiveGenotyper(int minCalledVariantDepth, int minGQscore, int maxGQscore,
            AdaptiveGenotypingParameters adaptiveGenotypingParameters)
        {
            MinGQScore = minGQscore;
            MaxGQScore = maxGQscore;
            _adaptiveGenotypingParameters = adaptiveGenotypingParameters;
        }

        public PloidyModel PloidyModel
        {
            get
            {
                return _ploidyModel;
            }
        }


        public List<CalledAllele> SetGenotypes(IEnumerable<CalledAllele> alleles)
        {
            // Temporary workaround
            _adaptiveGenotypingParameters.MinVarFrequency = 0.1f;
            var allelesToPrune = new List<CalledAllele>();

            var singleGTForLoci = CalculateDiploidGenotypeFromBinomialModel(alleles, MinDepthToGenotype,
                _adaptiveGenotypingParameters, out allelesToPrune);

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
                    MixtureModelResult adaptiveGTResult = AdaptiveGenotyperQualityCalculator.GetQScores(allele,
                            _adaptiveGenotypingParameters.GetModelsForVariantType(allele),
                            _adaptiveGenotypingParameters.GetPriorsForVariantType(allele));

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

                var mixtureModelResult = AdaptiveGenotyperQualityCalculator.GetMultiAllelicQScores(allele1, allele2,
                    new List<double[]> { _adaptiveGenotypingParameters.GetModelsForVariantType(allele1),
                        _adaptiveGenotypingParameters.GetModelsForVariantType(allele2) });


                foreach (CalledAllele allele in alleles)
                {
                    allele.GenotypeQscore = Math.Max(Math.Min(mixtureModelResult.QScore, MaxGQScore), MinGQScore);
                    allele.GenotypePosteriors = mixtureModelResult.GenotypePosteriors;
                }
            }

            return allelesToPrune;
        }




        private static Genotype CalculateDiploidGenotypeFromBinomialModel(IEnumerable<CalledAllele> alleles,
            int minDepthToGenotype, AdaptiveGenotypingParameters adaptiveGenotypingParameters,
            out List<CalledAllele> allelesToPrune)
        {
            allelesToPrune = new List<CalledAllele>();
            var orderedVariants = GenotypeCalculatorUtilities.FilterAndOrderAllelesByFrequency(alleles, allelesToPrune, adaptiveGenotypingParameters.MinVarFrequency);
            var referenceFrequency = GenotypeCalculatorUtilities.GetReferenceFrequency(orderedVariants, adaptiveGenotypingParameters.MinVarFrequency);
            var refExists = referenceFrequency >= adaptiveGenotypingParameters.MinVarFrequency;
            var depthIssue = GenotypeCalculatorUtilities.CheckForDepthIssue(alleles, minDepthToGenotype);
            bool refCall = orderedVariants.Count == 0;


            //assume its ref call
            SimplifiedDiploidGenotype preliminaryGenotype = SimplifiedDiploidGenotype.HomozygousRef;

            //this is order by descending - so most frequent is first.
            if (!refCall)
            {
                //do we apply SNP threshholds or indel thresholds?
                var dominantVariant = orderedVariants[0];

                double[] model = adaptiveGenotypingParameters.GetModelsForVariantType(dominantVariant);
                double[] priors = adaptiveGenotypingParameters.GetPriorsForVariantType(dominantVariant);

                MixtureModelResult adaptiveGTresult = AdaptiveGenotyperQualityCalculator.GetQScores(dominantVariant, model, priors);
                preliminaryGenotype = adaptiveGTresult.GenotypeCategory;
            }


            var finalGTForLoci = GenotypeCalculatorUtilities.ConvertSimpleGenotypeToComplextGenotype(alleles, orderedVariants, referenceFrequency, refExists, depthIssue, refCall,
                adaptiveGenotypingParameters.MinVarFrequency, adaptiveGenotypingParameters.SumVFforMultiAllelicSite, preliminaryGenotype);

            allelesToPrune = GenotypeCalculatorUtilities.GetAllelesToPruneBasedOnGTCall(finalGTForLoci, orderedVariants, allelesToPrune);

            return finalGTForLoci;


        }

    }
}
