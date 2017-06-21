using System;
using System.Collections.Generic;
using System.Linq;
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
        public float MinimumFrequencyFilter = 0.01f;

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
			//if (PloidyModel == PloidyModel.Diploid)
			//{
			//	MinimumFrequency = DiploidThresholdingParameters.MinorVF;
			//}


			//if (MinimumFrequencyFilter < MinimumFrequency)
   //         {
   //             MinimumFrequencyFilter = MinimumFrequency;
   //         }

           NoiseLevelUsedForQScoring = GetNoiseLevelUsedForQScoring(bamFilterParameters);
        }

        public int GetNoiseLevelUsedForQScoring(BamFilterParameters bamFilterParameters)
        {
            return ForcedNoiseLevel == -1 ? bamFilterParameters.MinimumBaseCallQuality : ForcedNoiseLevel;
        }

        public List<string> Parse(string[] arguments)
        {
            var lastArgumentField = string.Empty;
            var usedArguments = new List<string>();

            try
            {
                int argumentIndex = 0;
                while (argumentIndex < arguments.Length)
                {
                    if (string.IsNullOrEmpty(arguments[argumentIndex]))
                    {
                        argumentIndex++;
                        continue;
                    }
                    string value = null;
                    if (argumentIndex < arguments.Length - 1) value = arguments[argumentIndex + 1].Trim();

                    lastArgumentField = arguments[argumentIndex].ToLower();

                    switch (lastArgumentField)
                    {
                        case "-minvq":
                        case "-minvariantqscore":
                            MinimumVariantQScore = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-c":
                        case "-mindp":
                        case "-mindepth":
                        case "-mincoverage": //last release this is available. trying to be nice for backwards compatibility with Isas.
                            MinimumCoverage = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-minvf":  //used to be "f"
                        case "-minimumvariantfrequency":
                        case "-minimumfrequency":
                            MinimumFrequency = float.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-vqfilter": //used to be "F"
                        case "-variantqualityfilter":
                            MinimumVariantQScoreFilter = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-vffilter": //used to be "v"
                        case "-minvariantfrequencyfilter":
                            MinimumFrequencyFilter = float.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-gqfilter":
                        case "-genotypequalityfilter":
                            LowGenotypeQualityFilter = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-repeatfilter":
                            IndelRepeatFilter = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-mindpfilter":
                        case "-mindepthfilter":
                            LowDepthFilter = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-ssfilter": //used to be "fo"
                        case "-enablesinglestrandfilter":
                            FilterOutVariantsPresentOnlyOneStrand = bool.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-nl":
                        case "-noiselevelforqmodel":
                            ForcedNoiseLevel = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;                      
                        case "-ploidy":
                            if (value.ToLower().Contains("somatic"))
                                PloidyModel = PloidyModel.Somatic;
                            else if (value.ToLower().Contains("diploid"))
                                PloidyModel = PloidyModel.Diploid;
                            else
                                throw new ArgumentException(string.Format("Unknown ploidy model '{0}'", value));
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-diploidgenotypeparameters":
                            var parameters = OptionHelpers.ParseStringToFloat(value.Split(OptionHelpers.Delimiter));
                            if (parameters.Length != 3)
                                throw new ArgumentException(string.Format("DiploidGenotypeParamteers argument requires exactly three values."));
                            DiploidThresholdingParameters = new DiploidThresholdingParameters(parameters);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-sbmodel":
                            if (value.ToLower().Contains("poisson"))
                                StrandBiasModel = StrandBiasModel.Poisson;
                            else if (value.ToLower().Contains("extended"))
                                StrandBiasModel = StrandBiasModel.Extended;
                            else
                                throw new ArgumentException(string.Format("Unknown strand bias model '{0}'", value));
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-maxvq":
                        case "-maxvariantqscore":
                            MaximumVariantQScore = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-maxgq":
                        case "-maxgenotypeqscore":
                            MaximumGenotypeQScore = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-mingq":
                        case "-minqenotypeqscore":
                            MinimumGenotypeQScore = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-sbfilter":
                        case "-maxacceptablestrandbiasfilter":
                            StrandBiasAcceptanceCriteria = float.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-rmxnfilter":
                            bool turnOn = true;
                            bool worked = (bool.TryParse(value, out turnOn));
                            if (worked)
                            {
                                if (turnOn)
                                {
                                    // stick with defaults
                                }
                                else
                                {
                                    //turn off
                                    RMxNFilterMaxLengthRepeat = null;
                                    RMxNFilterMinRepetitions = null;
                                }
                                usedArguments.Add(lastArgumentField);
                                break;
                            }
                            //else, it wasnt a bool...
                            var rmxnThresholds = OptionHelpers.ParseStringToFloat(value.Split(OptionHelpers.Delimiter));
                            if ((rmxnThresholds.Length < 2) || (rmxnThresholds.Length > 3))
                                throw new ArgumentException(string.Format("RMxNFilter argument requires two or three values."));
                            RMxNFilterMaxLengthRepeat = (int)rmxnThresholds[0];
                            RMxNFilterMinRepetitions = (int)rmxnThresholds[1];

                            if (rmxnThresholds.Length > 2)
                                RMxNFilterFrequencyLimit = (float)rmxnThresholds[2];
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-noisemodel":
                            NoiseModel = value.ToLower() == "window" ? NoiseModel.Window : NoiseModel.Flat;
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-gender":
                            IsMale = value.ToLower() == "male";
                            break;
                        default:
                            
                            break;
                    }
                    argumentIndex += 2;
                }
                return usedArguments;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(string.Format("Unable to parse argument {0}: {1}", lastArgumentField, ex.Message));
            }
        }


        public void Validate()
        {
            ValidationHelper.VerifyRange(MinimumVariantQScore, 0, int.MaxValue, "MinimumVariantQscore");
            ValidationHelper.VerifyRange(MaximumVariantQScore, 0, int.MaxValue, "MaximumVariantQScore");
            if (MaximumVariantQScore < MinimumVariantQScore)
                throw new ArgumentException("MinimumVariantQScore must be less than or equal to MaximumVariantQScore.");

            ValidationHelper.VerifyRange(MinimumFrequency, 0f, 1f, "MinimumFrequency");
            ValidationHelper.VerifyRange(MinimumVariantQScoreFilter, MinimumVariantQScore, MaximumVariantQScore, "FilteredVariantQScore");

            ValidationHelper.VerifyRange(MinimumFrequencyFilter, 0, 1f, "FilteredVariantFrequency");

            if (LowGenotypeQualityFilter != null)
                ValidationHelper.VerifyRange((float)LowGenotypeQualityFilter, 0, int.MaxValue, "FilteredLowGenomeQuality");
            if (IndelRepeatFilter != null)
                ValidationHelper.VerifyRange((int)IndelRepeatFilter, 0, 10, "FilteredIndelRepeats");
            if (LowDepthFilter != null)
                ValidationHelper.VerifyRange((int)LowDepthFilter, MinimumCoverage, int.MaxValue, "FilteredLowDepth");

            if ((LowDepthFilter == null) || (LowDepthFilter < MinimumCoverage))
            {
                LowDepthFilter = MinimumCoverage;
            }

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
