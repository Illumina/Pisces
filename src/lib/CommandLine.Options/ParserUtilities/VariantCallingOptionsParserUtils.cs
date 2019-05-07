using System;
using System.IO;
using System.Collections.Generic;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.Genotyping;
using Common.IO.Utility;
using CommandLine.NDesk.Options;

namespace CommandLine.Options
{
    public class VariantCallingOptionsParserUtils
    {
        public static Dictionary<string, OptionSet> GetVariantCallingParsingMethods(VariantCallingParameters options)
        {
            var variantCallingOps = new OptionSet
            {
                {
                    "minvq|minvariantqscore=",
                    OptionTypes.INT + " MinimumVariantQScore to report variant",
                    value => options.MinimumVariantQScore = int.Parse(value)
                },
                {
                    "c|mindp|mindepth|mincoverage=",
                    OptionTypes.INT + " Minimum depth to call a variant",
                    value => options.MinimumCoverage = int.Parse(value)
                },
                {
                    "minvf|minimumvariantfrequency|minimumfrequency=",
                    OptionTypes.FLOAT + " MinimumFrequency to call a variant",
                    value => options.MinimumFrequency = float.Parse(value)
                },
                {
                    "targetlodfrequency|targetvf=",
                    OptionTypes.FLOAT + " Target Frequency to call a variant. Ie, to target a 5% allele frequency, we must call down to 2.6%, to capture that 5% allele 95% of the time. This parameter is used by the Somatic Genotyping Model",
                    value => options.TargetLODFrequency = float.Parse(value)
                },
                {
                    "vqfilter|variantqualityfilter=",
                    OptionTypes.INT + " FilteredVariantQScore to report variant as filtered",
                    value => options.MinimumVariantQScoreFilter = int.Parse(value)
                },
                {
                    "vffilter|minvariantfrequencyfilter=",
                    OptionTypes.FLOAT + " FilteredVariantFrequency to report variant as filtered",
                    value => options.MinimumFrequencyFilter = float.Parse(value)
                },
                {
                    "gqfilter|genotypequalityfilter=",
                    OptionTypes.INT +" Filtered Genotype quality to report variant as filtered",
                    value => options.LowGenotypeQualityFilter = int.Parse(value)
                },
                {
                    "repeatfilter_ToBeRetired=",
                    OptionTypes.INT + " FilteredIndelRepeats to report variant as filtered. To be retired. Please transition to RMxN.",
                    value => options.IndelRepeatFilter = int.Parse(value)
                },
                {
                    "mindpfilter|mindepthfilter=",
                    OptionTypes.INT + " FilteredLowDepth to report variant as filtered",
                    value => options.LowDepthFilter = int.Parse(value)
                },
                {
                    "ssfilter|enablesinglestrandfilter=",
                    OptionTypes.BOOL + " Flag variants as filtered if coverage limited to one strand",
                    value => options.FilterOutVariantsPresentOnlyOneStrand = bool.Parse(value)

                },
                {
                    "nl|noiselevelforqmodel=",
                    OptionTypes.INT + " Overrides the noise level to used by the quality model with this value. By default, this is driven by the basecall filter.",
                    value => options.ForcedNoiseLevel = int.Parse(value)
                },
                {
                    "ploidy=",
                    OptionTypes.STRING +  $" 'somatic' or 'diploid'. default, {options.PloidyModel}. To test drive the new adaptive model, try 'DiploidByAdaptiveGT' ",
                    value => options.PloidyModel = ConvertToPloidy(value)
                },
                {
                    "diploidsnvgenotypeparameters=",
                    OptionTypes.STRING + " A,B,C. default " + options.DiploidSNVThresholdingParameters.ToString(),
                    value=> options.DiploidSNVThresholdingParameters = ConvertToDiploidThresholding(value)
                },
                {
                    "diploidindelgenotypeparameters=",
                    OptionTypes.STRING + " A,B,C. default " + options.DiploidINDELThresholdingParameters.ToString(),
                    value=> options.DiploidINDELThresholdingParameters =ConvertToDiploidThresholding(value)
                },
                {
                    "adaptivegenotypeparameters_fromfile=",
                    OptionTypes.PATH + " file name. default, none.",
                    value=> options.AdaptiveGenotypingParameters =  UpdateAdaptiveGenotypingParameters(options.AdaptiveGenotypingParameters , value )
                },
                 {
                    "adaptivegenotypeparameters_snvmodel=",
                    OptionTypes.STRING + " A,B,C. default " + OptionHelpers.ListOfParamsToDelimiterSeparatedString( options.AdaptiveGenotypingParameters.SnvModel),
                    value=> options.AdaptiveGenotypingParameters.SnvModel = ConvertToAdaptiveGenotypingParameters(value)
                },
                {
                    "adaptivegenotypeparameters_indelmodel=",
                    OptionTypes.STRING + " A,B,C. default " + OptionHelpers.ListOfParamsToDelimiterSeparatedString( options.AdaptiveGenotypingParameters.IndelModel),
                    value=>  options.AdaptiveGenotypingParameters.IndelModel = ConvertToAdaptiveGenotypingParameters(value)
                },
                 {
                    "adaptivegenotypeparameters_snvprior=",
                    OptionTypes.STRING + " A,B,C. default " + OptionHelpers.ListOfParamsToDelimiterSeparatedString( options.AdaptiveGenotypingParameters.SnvPrior),
                    value=> options.AdaptiveGenotypingParameters.SnvPrior = ConvertToAdaptiveGenotypingParameters(value)
                },
                {
                    "adaptivegenotypeparameters_indelprior=",
                    OptionTypes.STRING + " A,B,C. default " + OptionHelpers.ListOfParamsToDelimiterSeparatedString( options.AdaptiveGenotypingParameters.IndelPrior),
                    value=>  options.AdaptiveGenotypingParameters.IndelPrior = ConvertToAdaptiveGenotypingParameters(value)
                },
                {
                    "sbmodel=",
                    OptionTypes.STRING + " ",
                    value=>options.StrandBiasModel = ConvertToStrandBiasModel(value)
                },
                {
                    "maxvq|maxvariantqscore=",
                    OptionTypes.INT + " MaximumVariantQScore to cap output variant Qscores. Default, " + options.MaximumVariantQScore,
                    value=>options.MaximumVariantQScore = int.Parse(value)
                },
                {
                    "maxgq|maxgenotypeqscore=",
                    OptionTypes.INT + " Maximum genotype QScore to cap output genotype Qscores. Default, " + options.MaximumGenotypeQScore,
                    value=>options.MaximumGenotypeQScore = int.Parse(value)
                },
                {
                    "maxgp|maxgenotypeposteriorscore=",
                    OptionTypes.INT + " Maximum genotype posterior score to cap output genotype posteriors. Default, " + options.AdaptiveGenotypingParameters.MaxGenotypePosteriors,
                    value=>options.AdaptiveGenotypingParameters.MaxGenotypePosteriors = int.Parse(value)
                },
                {
                    "mingq|mingenotypeqscore=",
                    OptionTypes.INT + " Minimum genotype QScore to cap output genotype Qscores. Default, " + options.MinimumGenotypeQScore,
                    value=>options.MinimumGenotypeQScore = int.Parse(value)
                },
                {
                    "sbfilter|maxacceptablestrandbiasfilter=",
                    OptionTypes.FLOAT + " Strand bias cutoff. Default, " + options.StrandBiasAcceptanceCriteria,
                    value=>options.StrandBiasAcceptanceCriteria = float.Parse(value)
                },
                {
                    "noisemodel=",
                    OptionTypes.STRING + $" Window/Flat. Default {options.NoiseModel}",
                    value=>options.NoiseModel = value.ToLower() == "window" ? NoiseModel.Window : NoiseModel.Flat
                },
                {
                    "gender=",
                    OptionTypes.BOOL + " Gender of the sample, if known. Male=TRUE, Female=FALSE . Default, unset.",
                    value=>options.IsMale = value.ToLower() == "male"
                },
                {
                    "rmxnfilter=",
                    OptionTypes.STRING + " M,N,F. Comma-separated list of integers indicating max length of the repeat section (M), the minimum number of repetitions of that repeat (N), to be applied if the variant frequency is less than (F). Default is R5x9,F=20.",
                    value=> ParseRMxNFilter(value, ref options.RMxNFilterMaxLengthRepeat,ref options.RMxNFilterMinRepetitions, ref options.RMxNFilterFrequencyLimit)

                },
                {
                    "ncfilter=",
                    OptionTypes.FLOAT + " No-call rate filter",
                    value=>options.NoCallFilterThreshold = float.Parse(value)
                },
                {
                    "abfilter=",
                    OptionTypes.FLOAT +
                       " Amplicon bias filter threshold. By default, this filter is off. If on, the threshold has the following meaning: " +
                       " If a variant shows up at Y percent on amplicon A and X percent on amplicon B, the X observation must be at least as probable as the Amplicon bias filter threshold, using the observations of Y as frequency estimate. " +
                       " To turn on, set to a positive float. '0.01' seems to work well. ",
                    value => options.AmpliconBiasFilterThreshold=  ParseAmpliconBiasFilter(value, options.AmpliconBiasFilterThreshold)

                    //tjd+ - remember to change the help text when we update the deafult to 'on, 0.01'
                    // "Default value of the threshold is " + options.AmpliconBiasFilterThreshold + ". Set to FALSE to turn off entirely. Set to TRUE is equivalent to omitting this 'abfilter' argument entirely, and invokes the default vaules. If any reads are found without an amplicon name (no XN tag) this feature automatically shutts off." ,
                    //" Amplicon bias filter threshold. By default, if a variant shows up at Y percent on amplicon A and X percent on amplicon B, the X observation must be at least as probable as the Amplicon bias filter threshold, using the observations of Y as frequency estimate. " +
                   //tjd-

                }

            };


            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.VariantCalling,variantCallingOps },
            };


            return optionDict;
        }

        public static void AddVariantCallingArgumentParsing(Dictionary<string, OptionSet> parsingMethods, VariantCallingParameters options)
        {
            var variantCallingOptionDict = GetVariantCallingParsingMethods(options);


            foreach (var key in variantCallingOptionDict.Keys)
            {
                foreach (var optSet in variantCallingOptionDict[key])
                {
                    if (!parsingMethods.ContainsKey(key))
                    {
                        parsingMethods.Add(key, new OptionSet());
                    }

                    parsingMethods[key].Add(optSet);
                }
            }
        }


        private static StrandBiasModel ConvertToStrandBiasModel(string value)
        {
            if (value.ToLower().Contains("poisson"))
                return StrandBiasModel.Poisson;
            else if (value.ToLower().Contains("extended"))
                return StrandBiasModel.Extended;
            else if (value.ToLower().Contains("diploid"))
                return StrandBiasModel.Diploid;
            else
                throw new ArgumentException(string.Format("Unknown strand bias model '{0}'", value));
        }

        private static DiploidThresholdingParameters ConvertToDiploidThresholding(string value)
        {
            var parameters = OptionHelpers.ParseStringToFloat(value.Split(OptionHelpers.Delimiter));
            if (parameters.Length != 3)
                throw new ArgumentException(string.Format("DiploidGenotypeParameters argument requires exactly three values."));
            var diploidThresholdingParameters = new DiploidThresholdingParameters(parameters);
            return diploidThresholdingParameters;
        }


        private static double[] ConvertToAdaptiveGenotypingParameters(string value)
        {
            return (OptionHelpers.ParseStringToDouble(value.Split(OptionHelpers.Delimiter)));
        }

        private static List<MixtureModelParameters> ReadAdaptiveGenotypingParametersFromFile(string value)
        {
            var ListOfMixtureModels = MixtureModel.ReadModelsFile(value); 

            return ListOfMixtureModels;
        }

        private static AdaptiveGenotypingParameters UpdateAdaptiveGenotypingParameters(AdaptiveGenotypingParameters parameters, string inputModelFilePath)
        {

            if (File.Exists(inputModelFilePath))
            {
                var newModels = MixtureModel.ReadModelsFile(inputModelFilePath);

                parameters.SnvModel = newModels[0].Means;
                parameters.SnvPrior = newModels[0].Priors;

                parameters.IndelModel = newModels[1].Means;
                parameters.IndelPrior = newModels[1].Priors;
            }
            else
            {
                throw new ArgumentException("No AdaptiveGT model file found at " + inputModelFilePath);
            }
           

            return parameters;
        }


        private static PloidyModel ConvertToPloidy(string value)
        {
            if (value.ToLower() == "somatic")
                return PloidyModel.Somatic;
            else if (value.ToLower() == "diploid")
                return PloidyModel.DiploidByThresholding;
            else if (value.ToLower() == "diploidbyadaptivegt")
                return PloidyModel.DiploidByAdaptiveGT;
            else
                throw new ArgumentException(string.Format("Unknown ploidy model '{0}'", value));
        }

        public static float? ParseAmpliconBiasFilter(string value, float? defaultValue)
        {
            float filterValue = 0F;
            bool worked1 = (float.TryParse(value, out filterValue));
            if (worked1)
            {
                if (filterValue <= 0)
                {
                    throw new ArgumentException(string.Format("AmpliconBiasFilterThreshold should be > 0. The parsed value was '{0}'.", filterValue));
                }
                else
                {
                    return filterValue;
                }
            }

            //else


            bool turnOn = true;
            bool worked2 = (bool.TryParse(value, out turnOn));

            if (worked2)
            {
                if (turnOn) //set to TRUE
                    return 0.01F;
                else        //set to FALSE
                    return null;
            }
            else  //set to something unparsable.
                throw new ArgumentException(string.Format("Unable to parse AmpliconBiasFilterThreshold '{0}'", value));

        }
        private static void ParseRMxNFilter(string value, ref int? RMxNFilterMaxLengthRepeat, ref int? RMxNFilterMinRepetitions, ref float RMxNFilterFrequencyLimit)
        {
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
                return;
            }
            //else, it wasnt a bool...
            var rmxnThresholds = OptionHelpers.ParseStringToFloat(value.Split(OptionHelpers.Delimiter));
            if ((rmxnThresholds.Length < 2) || (rmxnThresholds.Length > 3))
                throw new ArgumentException(string.Format("RMxNFilter argument requires two or three values."));
            RMxNFilterMaxLengthRepeat = (int)rmxnThresholds[0];
            RMxNFilterMinRepetitions = (int)rmxnThresholds[1];

            if (rmxnThresholds.Length > 2)
                RMxNFilterFrequencyLimit = (float)rmxnThresholds[2];
        }


    }
}
