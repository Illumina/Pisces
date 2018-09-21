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
        public static IGenotypeCalculator CreateGenotypeCalculator(PloidyModel ploidyModel, float minimumFrequencyFilter,
             int minEmitDepth, DiploidThresholdingParameters snvParameters, DiploidThresholdingParameters indelParameters, 
             int minGQscore, int maxGQscore, float targetLODVariantFrequency,
             float minimumEmitFrequency=0, 
             string refName=null,bool? isMale=null)
        {
	        if (ploidyModel == PloidyModel.Somatic || refName == "chrM")
		        return new SomaticGenotypeCalculator(minimumFrequencyFilter, minEmitDepth, minGQscore, maxGQscore,
			        minimumEmitFrequency, targetLODVariantFrequency);

			if(isMale == null)
				return new DiploidGenotypeCalculator(snvParameters, indelParameters, minEmitDepth, minGQscore, maxGQscore);

			if (isMale.Value &&(refName=="chrY" || refName== "chrX"))
				return new HaploidGenotyeCalculator(minEmitDepth,minGQscore,maxGQscore,snvParameters.MinorVF,snvParameters.MajorVF);

	        if (!isMale.Value && refName == "chrY")
	        {
		        Common.IO.Utility.Logger.WriteWarningToLog("chrY exists in Female samples");
                return new HaploidGenotyeCalculator(minEmitDepth, minGQscore, maxGQscore, snvParameters.MinorVF, snvParameters.MajorVF);
			}


			return new DiploidGenotypeCalculator(snvParameters, snvParameters, minEmitDepth, minGQscore, maxGQscore);

        }
    }

    }