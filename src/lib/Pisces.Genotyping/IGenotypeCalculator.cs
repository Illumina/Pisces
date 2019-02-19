using System.Collections.Generic;
using Pisces.Domain.Types;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Genotyping
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

    }