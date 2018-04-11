using System;
using Pisces.Domain.Utility;
using Pisces.Domain.Types;

namespace Pisces.Domain.Options
{
   
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


    public class VariantCallingParameters
    {
        public float MinimumFrequency = 0.01f;
        public float MinimumFrequencyFilter = -1; //if unset, will raise to MinimumFrequency 
        public float TargetLODFrequency = -1; //if unset, will raise to TargetLODFrequency

        public int MaximumVariantQScore = 100;
        public int MinimumVariantQScore = 20;
        public int MinimumVariantQScoreFilter = 30;

        public int MaximumGenotypeQScore = 100;
        public int MinimumGenotypeQScore = 0;
        public int? LowGenotypeQualityFilter;

        public int MinimumCoverage = 10;
        public int? LowDepthFilter;

        public int? IndelRepeatFilter;

        public int? RMxNFilterMaxLengthRepeat = 5;
        public int? RMxNFilterMinRepetitions = 9;
        public float RMxNFilterFrequencyLimit = 0.20f; //this was recommended by Kristina K after empirical testing 

        public PloidyModel PloidyModel = PloidyModel.Somatic;
        public DiploidThresholdingParameters DiploidThresholdingParameters = new DiploidThresholdingParameters();
        public bool? IsMale;

        public int ForcedNoiseLevel = -1;
        public int NoiseLevelUsedForQScoring = 20;
        public NoiseModel NoiseModel = NoiseModel.Flat;

        public float StrandBiasAcceptanceCriteria = 0.5f;
        public StrandBiasModel StrandBiasModel = StrandBiasModel.Extended;  //maybe should add "none" for scylla
        public bool FilterOutVariantsPresentOnlyOneStrand = false;

        public void SetDerivedParameters(BamFilterParameters bamFilterParameters)
        {
			
           NoiseLevelUsedForQScoring = GetNoiseLevelUsedForQScoring(bamFilterParameters);
        }

        public int GetNoiseLevelUsedForQScoring(BamFilterParameters bamFilterParameters)
        {
            return ForcedNoiseLevel == -1 ? bamFilterParameters.MinimumBaseCallQuality : ForcedNoiseLevel;
        }

        public void Validate()
        {
            ValidationHelper.VerifyRange(MinimumVariantQScore, 0, int.MaxValue, "MinimumVariantQscore");
            ValidationHelper.VerifyRange(MaximumVariantQScore, 0, int.MaxValue, "MaximumVariantQScore");
            if (MaximumVariantQScore < MinimumVariantQScore)
                throw new ArgumentException("MinimumVariantQScore must be less than or equal to MaximumVariantQScore.");

            ValidationHelper.VerifyRange(MinimumFrequency, 0f, 1f, "MinimumFrequency");
            ValidationHelper.VerifyRange(MinimumVariantQScoreFilter, MinimumVariantQScore, MaximumVariantQScore, "FilteredVariantQScore");

            if (LowGenotypeQualityFilter != null)
                ValidationHelper.VerifyRange((float)LowGenotypeQualityFilter, 0, int.MaxValue, "FilteredLowGenomeQuality");
            if (IndelRepeatFilter != null)
                ValidationHelper.VerifyRange((int)IndelRepeatFilter, 0, 10, "FilteredIndelRepeats");
            if (LowDepthFilter != null)
                ValidationHelper.VerifyRange((int)LowDepthFilter, MinimumCoverage, int.MaxValue, "FilteredLowDepth");

            if ((LowDepthFilter == null) || (LowDepthFilter < MinimumCoverage))
            {
                Console.WriteLine("Options helper: Setting LowDepthFilter = MinimumCoverage");
                LowDepthFilter = MinimumCoverage;
            }


            if (MinimumFrequencyFilter < MinimumFrequency)
            {
                Console.WriteLine("Options helper: Setting MinimumFrequencyFilter to MinimumFrequency");
                MinimumFrequencyFilter = MinimumFrequency;
            }
            ValidationHelper.VerifyRange(MinimumFrequencyFilter, 0, 1f, "FilteredVariantFrequency");


            if (TargetLODFrequency < MinimumFrequencyFilter)
            {
                Console.WriteLine("Options helper: Setting TargetLODFrequency to MinimumFrequencyFilter");
                TargetLODFrequency = MinimumFrequencyFilter;
            }
            ValidationHelper.VerifyRange(TargetLODFrequency, 0, 1f, "TargetLODFrequency");


            if (ForcedNoiseLevel != -1)
                ValidationHelper.VerifyRange(ForcedNoiseLevel, 0, int.MaxValue, "AppliedNoiseLevel");

            ValidationHelper.VerifyRange(StrandBiasAcceptanceCriteria, 0f, int.MaxValue, "Strand bias cutoff");


            if (RMxNFilterMaxLengthRepeat != null || RMxNFilterMinRepetitions != null)
            {
                if (RMxNFilterMaxLengthRepeat == null || RMxNFilterMinRepetitions == null)
                {
                    throw new ArgumentException(string.Format("If specifying RMxN filter thresholds, you must supply both RMxNFilterMaxLengthRepeat and RMxNFilterMinRepetitions."));
                }
                ValidationHelper.VerifyRange((int)RMxNFilterMaxLengthRepeat, 0, 100, "RMxNFilterMaxLengthRepeat");
                ValidationHelper.VerifyRange((int)RMxNFilterMinRepetitions, 0, 100, "RMxNFilterMinRepetitions");
            }
        }


    }
    

    public class DerivedParameters
    {

        public static int GetNoiseLevelUsedForQScoring(
            VariantCallingParameters varCallingParameters, BamFilterParameters bamFilterParameters)
        {
            return varCallingParameters.ForcedNoiseLevel == -1 ? bamFilterParameters.MinimumBaseCallQuality : varCallingParameters.ForcedNoiseLevel;
        }

    }

}
