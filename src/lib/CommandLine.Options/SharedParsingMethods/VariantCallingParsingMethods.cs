using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using CommandLine.IO;
using CommandLine.NDesk.Options;

namespace CommandLine.Options
{
    public class VariantCallingOptionsParser
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
                    OptionTypes.STRING +  $" 'somatic' or 'diploid'. default, {options.PloidyModel}.",
                    value => options.PloidyModel = ConvertToPloidy(value)
                },
                {
                    "diploidgenotypeparameters=",
                    OptionTypes.STRING + " A,B,C. default 0.20,0.70,0.80",
                    value=> options.DiploidThresholdingParameters = ConvertToDiploidThresholding(value)
                },
                {
                    "sbmodel=",
                    OptionTypes.STRING + " ",
                    value=>options.StrandBiasModel = ConvertToStrandBias(value)
                },
                {
                    "maxvq|maxvariantqscore=",
                    OptionTypes.INT + " MaximumVariantQScore to cap output variant Qscores",
                    value=>options.MaximumVariantQScore = int.Parse(value)
                },
                {
                    "maxgq|maxgenotypeqscore=",
                    OptionTypes.INT + " Maximum genotype QScore to cap output variant Qscores ",
                    value=>options.MaximumGenotypeQScore = int.Parse(value)
                },
                {
                    "mingq|mingenotypeqscore=",
                    OptionTypes.INT + " Minimum genotype QScore to cap output variant Qscores ",
                    value=>options.MinimumGenotypeQScore = int.Parse(value)
                },
                {
                    "sbfilter|maxacceptablestrandbiasfilter=",
                    OptionTypes.FLOAT + " Strand bias cutoff",
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
            var variantCalliingOptionDict = GetVariantCallingParsingMethods(options);


            foreach (var key in variantCalliingOptionDict.Keys)
            {
                foreach (var optSet in variantCalliingOptionDict[key])
                {
                    if (!parsingMethods.ContainsKey(key))
                    {
                        parsingMethods.Add(key, new OptionSet());
                    }

                    parsingMethods[key].Add(optSet);
                }
            }
        }


        private static StrandBiasModel ConvertToStrandBias(string value)
        {
            if (value.ToLower().Contains("poisson"))
                return StrandBiasModel.Poisson;
            else if (value.ToLower().Contains("extended"))
                return StrandBiasModel.Extended;
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

        private static PloidyModel ConvertToPloidy(string value)
        {
            if (value.ToLower().Contains("somatic"))
                return PloidyModel.Somatic;
            else if (value.ToLower().Contains("diploid"))
                return PloidyModel.Diploid;
            else
                throw new ArgumentException(string.Format("Unknown ploidy model '{0}'", value));
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
