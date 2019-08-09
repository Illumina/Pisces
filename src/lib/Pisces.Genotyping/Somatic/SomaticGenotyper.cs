using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Types;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Genotyping
{
    public class SomaticGenotyper : IGenotypeCalculator
    {
     
        float _minVariantFrequencyFilter = 0.01f;
        float _targetLODFrequency = 0.01f;
        PloidyModel _ploidyModel = PloidyModel.Somatic;
        public int MinGQScore { get; set; }
        public int MaxGQScore { get; set; }
        public int MinDepthToGenotype { get; set; }

        public float MinVarFrequency { get; set; } //TODO <- this is not used by the SomaticG calc, and could be refactored out of IGenotypeCalculator
        public float MinVarFrequencyFilter { get; set; }
       
        public void SetMinFreqFilter(float minFreqFilter)
	    {
		    MinVarFrequencyFilter = minFreqFilter > MinVarFrequency ? minFreqFilter : MinVarFrequency;
	    }

	    public SomaticGenotyper(){ }

        public SomaticGenotyper(float minVariantFrequencyFilter, int minCalledVariantDepth, int minGQscore, int maxGQscore,
            float minEmitVariantFrequency, float targetVariantFrequency)
        {
            _minVariantFrequencyFilter = minVariantFrequencyFilter;
            _targetLODFrequency = targetVariantFrequency;
            MinGQScore = minGQscore;
            MaxGQScore = maxGQscore;
            MinDepthToGenotype = minCalledVariantDepth;
	        MinVarFrequency = minEmitVariantFrequency; //TODO <- this is not used by the SomaticG calc, and could be refactored out of IGenotypeCalculator
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


            foreach (var allele in alleles)
            {
                allele.Genotype = CalculateSomaticGenotype(allele, _minVariantFrequencyFilter, MinDepthToGenotype);
                allele.GenotypeQscore = SomaticGenotypeQualityCalculator.Compute(allele, _targetLODFrequency, MinGQScore, MaxGQScore);
            }

            return new List<CalledAllele>();
        }

        private static Genotype CalculateSomaticGenotype(CalledAllele allele, float minFrequencyFilter, int minDepthToGenotype)
        {

            var defaultGenotype = Genotype.HomozygousRef;

            if (allele.TotalCoverage < minDepthToGenotype)
            {
                return (allele.Type == AlleleCategory.Reference) ? Genotype.RefLikeNoCall : Genotype.AltLikeNoCall;
            }

            if (allele.Type != AlleleCategory.Reference)
            {
               
                // if we see no evidence of a reference allele, according to the genotype model
                // then presume our variant is a homozygous alt
                if (allele.RefFrequency < minFrequencyFilter)
                {
                    if ((1 - allele.Frequency) > minFrequencyFilter)
                        return Genotype.AltAndNoCall;

                    return Genotype.HomozygousAlt;
                }
                return Genotype.HeterozygousAltRef;
            }
            else 
            {
                //we did not see enough reference
                if (allele.Frequency < minFrequencyFilter)
                    return Genotype.RefLikeNoCall;

                //we see too much of something else (unknown)
                if ((1 -allele.Frequency) > minFrequencyFilter)
                    return Genotype.RefAndNoCall;
            }
            return defaultGenotype;
        }
    }
}

