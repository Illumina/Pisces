using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pisces.Domain.Interfaces;
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

    }

    public class DiploidThresholdingParameters
    {
        public float MinorVF = 0.20f;  //could make separate threshold values for SNP and Indel...
        public float MajorVF = 0.70f;
        public float SumVFforMultiAllelicSite = 0.80f;

        public DiploidThresholdingParameters()
        {
        }

        //not too safe, but dev use only.
        public DiploidThresholdingParameters(float[] parameters)
        {
            MinorVF = parameters[0];
            MajorVF = parameters[1];
            SumVFforMultiAllelicSite = parameters[2];
        }

    }

    public class GenotypeCreator
    {
        public static IGenotypeCalculator CreateGenotypeCalculator(PloidyModel ploidyModel, float minCalledVariantFreq,
             int minCalledVariantDepth, DiploidThresholdingParameters parameters, int minGQscore, int maxGQscore)
        {
            return (ploidyModel == PloidyModel.Somatic)
                ? (IGenotypeCalculator)new SomaticGenotypeCalculator(minCalledVariantFreq, minCalledVariantDepth, minGQscore, maxGQscore)
                : new DiploidGenotypeCalculator(parameters, minCalledVariantDepth, minGQscore, maxGQscore);
        }
    }

    }