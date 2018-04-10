using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using CommandLine.IO;
using CommandLine.NDesk.Options;
using CommandLine.IO.Utilities;
using Pisces.Domain.Options;
using Pisces.Domain.Types;

namespace CommandLine.Options
{
    public class PiscesOptionsParser : BaseOptionParser
    {

        public PiscesApplicationOptions PiscesOptions;

        public PiscesOptionsParser()
        {
            PiscesOptions = new PiscesApplicationOptions();
        }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {


            var requiredOps = new OptionSet
            {
                {
                    "g|genomepaths|genomefolders=",
                    OptionTypes.FOLDERS + " Genome folder(s).  Single value, or comma delimited list corresponding to -bamFiles.  Must be single value if -bamFolder is specified.  No default value.",
                    value=> PiscesOptions.GenomePaths = value.Split(',').ToArray()
                }
            };

            var commonOps = new OptionSet
            {
                {
                    "i|intervalpaths=",
                    OptionTypes.PATHS+ " IntervalPath(s), single value or comma delimited list corresponding to BAMPath(s). At most one value should be provided if BAM folder is specified",
                    value=> PiscesOptions.IntervalPaths = value.Split(OptionHelpers.Delimiter)
                },

                {
                    "forcedalleles=",
                    OptionTypes.PATHS + " vcf path(s) for alleles that are forced to report",
                    value=> PiscesOptions.ForcedAllelesFileNames = value.Split(',').ToList()
                },

                {
                    "callmnvs=",
                    OptionTypes.BOOL + " Call MNVs (a.k.a. phased SNPs) 'true' or 'false'",
                    value=> PiscesOptions.CallMNVs = bool.Parse(value)
                },
                {
                    "maxmnvlength=",
                    OptionTypes.FOLDER + " Max length phased SNPs that can be called",
                    value=> PiscesOptions.MaxSizeMNV = int.Parse(value)
                },
                {
                    "maxgapbetweenmnv|maxrefgapinmnv=",
                    OptionTypes.INT + " Max allowed gap between phased SNPs that can be called",
                    value=> PiscesOptions.MaxGapBetweenMNV = int.Parse(value)
                },

                {
                    "outputsbfiles=",
                    OptionTypes.BOOL + " Output strand bias files, 'true' or 'false'",
                    value=> PiscesOptions.OutputBiasFiles = bool.Parse(value)
                },         
                {
                    "threadbychr=",
                    OptionTypes.BOOL + " Thread by chr. More memory intensive.  This will temporarily create output per chr.",
                    value=> PiscesOptions.ThreadByChr = bool.Parse(value)
                },
                {
                    "collapse=",
                    OptionTypes.BOOL + $" Whether or not to collapse variants together, 'true' or 'false'. default, {PiscesOptions.Collapse}",
                    value=> PiscesOptions.Collapse = bool.Parse(value)
                },
                {
                    "collapsefreqthreshold=",
                    OptionTypes.FLOAT+ $" When collapsing, minimum frequency required for target variants. Default {PiscesOptions.CollapseFreqThreshold}",
                    value=> PiscesOptions.CollapseFreqThreshold = float.Parse(value)
                },
                {
                    "collapsefreqratiothreshold=",
                    OptionTypes.FLOAT+ " When collapsing, minimum ratio required of target variant frequency to collapsible variant frequency. Default \'0.5f\'",
                    value=> PiscesOptions.CollapseFreqRatioThreshold = float.Parse(value)
                },              
                {
                    "priorspath=",
                    OptionTypes.FOLDER + " PriorsPath for vcf file containing known variants, used with -collapse to preferentially reconcile variants",
                    value=> PiscesOptions.PriorsPath = value
                },
                {
                    "trimmnvpriors=",
                   OptionTypes.BOOL +  " Whether or not to trim preceeding base from MNVs in priors file.  Note: COSMIC convention is to include preceeding base for MNV.  Default is false.",
                    value=> PiscesOptions.TrimMnvPriors = bool.Parse(value)
                },
                {
                    "coverageMethod=",
                    OptionTypes.STRING + "\'approximate\' or \'exact\'. Exact is more precise but requires more memory (minimum 8 GB).  Default approximate",
                    value=> PiscesOptions.CoverageMethod= ConvertToCoverageMethod(value)
                },            
                {
                    "baselogname=",
                    OptionTypes.STRING +  "",
                    value=> PiscesOptions.LogFileNameBase = value
                },
                {
                    "d|debug=",
                    OptionTypes.BOOL +  "",
                    value=> PiscesOptions.DebugMode = bool.Parse(value)
                }
            };

            var optionDict = new Dictionary<string, OptionSet>
            {
               {OptionSetNames.Required,requiredOps},
               {OptionSetNames.Common,commonOps },
            };

            BamProcessorParsingMethods.AddBamProcessorArgumentParsing(optionDict, PiscesOptions);
            BamFilterOptionsParser.AddBamFilterArgumentParsing(optionDict, PiscesOptions.BamFilterParameters);
            VariantCallingOptionsParser.AddVariantCallingArgumentParsing(optionDict, PiscesOptions.VariantCallingParameters);
            VcfWritingOptionsParser.AddVcfWritingArgumentParsing(optionDict, PiscesOptions.VcfWritingParameters);
        
            return optionDict;
        }

        private static CoverageMethod ConvertToCoverageMethod(string value)
        {
            if (value.ToLower() == "approximate")
                return CoverageMethod.Approximate;
            else if (value.ToLower() == "exact")
                return CoverageMethod.Exact;
            else
                throw new ArgumentException(string.Format("Unknown coverage method '{0}'", value));
        }

        public override void ValidateOptions()
        {

            try
            {
                BamProcessorParsingMethods.ValidateBamProcessorPaths(PiscesOptions.BAMPaths, PiscesOptions.GenomePaths, null);
                
                if (string.IsNullOrEmpty(PiscesOptions.OutputDirectory))
                {
                    PiscesOptions.OutputDirectory = Path.GetDirectoryName(PiscesOptions.BAMPaths[0]);
                }

                //will automatically update the error code if there is a problem
                CheckAndCreateDirectory(PiscesOptions.OutputDirectory, " output directory", "-o", false);
                if (ParsingResult.ExitCode != 0)
                    return;
                
            }
            catch (Exception ex)
            {
                ParsingResult.UpdateExitCode(ExitCodeType.MissingCommandLineOption);
                ParsingResult.ShowHelpMenu = true;
                var unsupportedOpsString = string.Join(",", ParsingResult.UnsupportedOps);
                ParsingResult.Exception = ex;
                ExitCodeUtilities.ShowExceptionAndUpdateExitCode(ex);
                return;
            }

            try
            {
                PiscesOptions.ValidateAndSetDerivedValues();
            }
            catch (Exception ex)
            {
                ParsingResult.UpdateExitCode(ExitCodeType.BadArguments);
                ParsingResult.ShowHelpMenu = true;
                var unsupportedOpsString = string.Join(",", ParsingResult.UnsupportedOps);
                ParsingResult.Exception = ex;
                ExitCodeUtilities.ShowExceptionAndUpdateExitCode(ex);
                return;
            }
        }
    }
}