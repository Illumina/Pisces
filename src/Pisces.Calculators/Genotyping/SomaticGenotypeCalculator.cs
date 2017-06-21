using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Types;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Calculators
{
    public class SomaticGenotypeCalculator : IGenotypeCalculator
    {
     
        float _minCalledVariantFrequency = 0.01f;
        PloidyModel _ploidyModel = PloidyModel.Somatic;
        public int MinGQScore { get; set; }
        public int MaxGQScore { get; set; }
        public int MinDepthToGenotype { get; set; }
	    public float MinVarFrequency { get; set; }
	    public float MinVarFrequencyFilter { get; set; }
	    public void SetMinFreqFilter(float minFreqFilter)
	    {
		    MinVarFrequencyFilter = minFreqFilter > MinVarFrequency ? minFreqFilter : MinVarFrequency;
	    }

	    public SomaticGenotypeCalculator(){ }

        public SomaticGenotypeCalculator(float minCalledVariantFrequency, int minCalledVariantDepth, int minGQscore, int maxGQscore,float minVarFreq)
        {
            _minCalledVariantFrequency = minCalledVariantFrequency;
            MinGQScore = minGQscore;
            MaxGQScore = maxGQscore;
            MinDepthToGenotype = minCalledVariantDepth;
	        MinVarFrequency = minVarFreq;
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
                allele.Genotype = CalculateSomaticGenotype(allele, _minCalledVariantFrequency, MinDepthToGenotype);
                allele.GenotypeQscore = SomaticGenotypeQualityCalculator.Compute(allele, _minCalledVariantFrequency, MinGQScore, MaxGQScore);
            }

            return new List<CalledAllele>();
        }

        private static Genotype CalculateSomaticGenotype(CalledAllele allele, float minFrequency, int minDepthToGenotype)
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
                if (allele.RefFrequency < minFrequency)
                {
                    if ((1 - allele.Frequency) > minFrequency)
                        return Genotype.AltAndNoCall;

                    return Genotype.HomozygousAlt;
                }
                return Genotype.HeterozygousAltRef;
            }
            else 
            {
                //we did not see enough reference
                if (allele.Frequency < minFrequency)
                    return Genotype.RefLikeNoCall;

                //we see too much of something else (unknown)
                if ((1 -allele.Frequency) > minFrequency)
                    return Genotype.RefAndNoCall;
            }
            return defaultGenotype;
        }
    }
}

