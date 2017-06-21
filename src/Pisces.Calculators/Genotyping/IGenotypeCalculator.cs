using System;
using System.Collections.Generic;
using Common.IO;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Calculators
{
    public interface IGenotypeCalculator
    {
        List<CalledAllele> SetGenotypes(IEnumerable<CalledAllele> alleles);
        PloidyModel PloidyModel { get; }
        int MinGQScore { get; set; }
        int MaxGQScore { get; set; }
        int MinDepthToGenotype { get; set; }
		float MinVarFrequency { get; set; }
		float MinVarFrequencyFilter { get; set; }

	    void SetMinFreqFilter(float minFreqFilter);

    }
   
    public class GenotypeCreator
    {
        public static IGenotypeCalculator CreateGenotypeCalculator(PloidyModel ploidyModel, float minCalledVariantFreq,
             int minCalledVariantDepth, DiploidThresholdingParameters parameters, int minGQscore, int maxGQscore,float minVarFreq=0,string refName=null,bool? isMale=null)
        {
	        if (ploidyModel == PloidyModel.Somatic || refName == "chrM")
		        return new SomaticGenotypeCalculator(minCalledVariantFreq, minCalledVariantDepth, minGQscore, maxGQscore,
			        minVarFreq);

			if(isMale == null)
				return new DiploidGenotypeCalculator(parameters, minCalledVariantDepth, minGQscore, maxGQscore);

			if (isMale.Value &&(refName=="chrY" || refName== "chrX"))
				return new HaploidGenotyeCalculator(minCalledVariantDepth,minGQscore,maxGQscore,parameters.MinorVF,parameters.MajorVF);

	        if (!isMale.Value && refName == "chrY")
	        {
		        Common.IO.Utility.Logger.WriteWarningToLog("chrY exists in Female samples");
                return new HaploidGenotyeCalculator(minCalledVariantDepth, minGQscore, maxGQscore, parameters.MinorVF, parameters.MajorVF);
			}


			return new DiploidGenotypeCalculator(parameters, minCalledVariantDepth, minGQscore, maxGQscore);

        }
    }

    }