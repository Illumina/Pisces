using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Genotyping
{

    public class DiploidThresholdingGenotyper : IGenotypeCalculator
    {

       
        DiploidThresholdingParameters _diploidSnvThresholdingParameters;
        DiploidThresholdingParameters _diploidIndelThresholdingParameters;

        PloidyModel _ploidyModel = PloidyModel.DiploidByThresholding;

        public int MinGQScore { get; set; }
        public int MaxGQScore { get; set; }
        public int MinDepthToGenotype { get; set; }
	    public float MinVarFrequency { get; set; }
	    public float MinVarFrequencyFilter { get; set; }
	    public void SetMinFreqFilter(float minFreqFilter)
	    {
		    MinVarFrequencyFilter = minFreqFilter > MinVarFrequency ? minFreqFilter : MinVarFrequency;
	    }

	    public DiploidThresholdingGenotyper()
        {
            var defaultParams = new VariantCallingParameters();
            _diploidSnvThresholdingParameters = defaultParams.DiploidSNVThresholdingParameters;
            _diploidIndelThresholdingParameters = defaultParams.DiploidINDELThresholdingParameters;
            MinGQScore = defaultParams.MinimumGenotypeQScore;
            MaxGQScore = defaultParams.MaximumGenotypeQScore;
            MinDepthToGenotype = defaultParams.MinimumCoverage;
            MinVarFrequency = _diploidSnvThresholdingParameters.MinorVF;

        }

        public DiploidThresholdingGenotyper(DiploidThresholdingParameters snvParameters, DiploidThresholdingParameters indelParameters,
            int minCalledVariantDepth, int minGQscore, int maxGQscore)
        {
            _diploidSnvThresholdingParameters = snvParameters;
            _diploidIndelThresholdingParameters = indelParameters;
            MinGQScore = minGQscore;
            MaxGQScore = maxGQscore;
            MinDepthToGenotype = minCalledVariantDepth;
	        MinVarFrequency = _diploidSnvThresholdingParameters.MinorVF;
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
            var allelesToPrune = new List<CalledAllele>();


            var singleGTForLoci = CalculateDiploidGenotype(alleles, MinDepthToGenotype,
                _diploidSnvThresholdingParameters, _diploidIndelThresholdingParameters, out allelesToPrune);
            int phaseSetIndex = 1;  //reserve -1 for unset, and 0 for reference, and 1 and 2 for alts
            foreach (var allele in alleles)
            {
                allele.Genotype = singleGTForLoci;
                allele.GenotypeQscore = DiploidGenotypeQualityCalculator.Compute(allele, MinGQScore, MaxGQScore);

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

            return allelesToPrune;
        }

        private static Genotype CalculateDiploidGenotype(IEnumerable<CalledAllele> alleles, 
            int minDepthToGenotype, DiploidThresholdingParameters snvThresholdingParameters,
            DiploidThresholdingParameters indelThresholdingParameters, out List<CalledAllele> allelesToPrune)
        {
            allelesToPrune = new List<CalledAllele>();
            var orderedVariants = GenotypeCalculatorUtilities.FilterAndOrderAllelesByFrequency(alleles, allelesToPrune, snvThresholdingParameters.MinorVF);
            var referenceFrequency = GenotypeCalculatorUtilities.GetReferenceFrequency(alleles, snvThresholdingParameters.MinorVF);
            var refExists = (referenceFrequency >= snvThresholdingParameters.MinorVF);
            var depthIssue = GenotypeCalculatorUtilities.CheckForDepthIssue(alleles, minDepthToGenotype);
            var parameters = snvThresholdingParameters;
            bool refCall = orderedVariants.Count == 0 || (orderedVariants[0].Frequency < snvThresholdingParameters.MinorVF);

            //do we apply SNP threshholds or indel thresholds?
            parameters = SelectParameters(indelThresholdingParameters, orderedVariants, parameters, refCall);

           var preliminaryGenotype = GetPreliminaryGenotype(orderedVariants, parameters, refCall);

            var finalGTForLoci = GenotypeCalculatorUtilities.ConvertSimpleGenotypeToComplextGenotype(alleles, orderedVariants, referenceFrequency, refExists, depthIssue, refCall,
              parameters.MinorVF, parameters.SumVFforMultiAllelicSite, preliminaryGenotype);

            allelesToPrune = GenotypeCalculatorUtilities.GetAllelesToPruneBasedOnGTCall(finalGTForLoci, orderedVariants, allelesToPrune);


            return finalGTForLoci;
        }

        private static SimplifiedDiploidGenotype GetPreliminaryGenotype(List<CalledAllele> orderedVariants, DiploidThresholdingParameters parameters, bool refCall)
        {         
            //obvious reference call
            if (refCall)
            {
                return SimplifiedDiploidGenotype.HomozygousRef;
            }//else, types of alt calls...
            else if ((orderedVariants[0].Frequency >= parameters.MinorVF) &&
                (orderedVariants[0].Frequency <= parameters.MajorVF))
            {
                return SimplifiedDiploidGenotype.HeterozygousAltRef;
            }
            else if (orderedVariants[0].Frequency > parameters.MajorVF)
            {
                return SimplifiedDiploidGenotype.HomozygousAlt;
            }
            else
            {
                return SimplifiedDiploidGenotype.HomozygousRef;
            }
        }

        public static DiploidThresholdingParameters SelectParameters(DiploidThresholdingParameters indelThresholdingParameters, List<CalledAllele> orderedVariants, DiploidThresholdingParameters snpThresholdingParameters, bool refCall)
        {

            if (refCall)
                return snpThresholdingParameters;
            else
            {
                var dominantVariant = orderedVariants.First();
                if (dominantVariant.Type != AlleleCategory.Snv)
                    return indelThresholdingParameters;
                else
                    return snpThresholdingParameters;
            }
        }

    }
}

