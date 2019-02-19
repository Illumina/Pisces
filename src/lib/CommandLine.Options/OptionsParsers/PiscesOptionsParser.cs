using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using CommandLine.NDesk.Options;
using CommandLine.Util;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Common.IO.Utility;

namespace CommandLine.Options
{
    public class PiscesOptionsParser : BaseOptionParser
    {

        public PiscesOptionsParser()
        {
            Options = new PiscesApplicationOptions();
        }


        public PiscesApplicationOptions PiscesOptions { get => (PiscesApplicationOptions) Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var options = (PiscesApplicationOptions)Options;

            var requiredOps = new OptionSet
            {
                {
                    "g|genomepaths|genomefolders=",
                    OptionTypes.FOLDERS + " Genome folder(s).  Single value, or comma delimited list corresponding to -bamFiles.  Must be single value if -bamFolder is specified.  No default value.",
                    value=> options.GenomePaths = value.Split(',').ToArray()
                }
            };

            var commonOps = new OptionSet
            {
                {
                    "i|intervalpaths=",
                    OptionTypes.PATHS+ " IntervalPath(s), single value or comma delimited list corresponding to BAMPath(s). At most one value should be provided if BAM folder is specified",
                    value=> options.IntervalPaths = value.Split(OptionHelpers.Delimiter)
                },

                {
                    "forcedalleles=",
                    OptionTypes.PATHS + " vcf path(s) for alleles that are forced to report",
                    value=> options.ForcedAllelesFileNames = value.Split(',').ToList()
                },

                {
                    "callmnvs=",
                    OptionTypes.BOOL + " Call MNVs (a.k.a. phased SNPs) 'true' or 'false'",
                    value=> options.CallMNVs = bool.Parse(value)
                },
                {
                    "maxmnvlength=",
                    OptionTypes.FOLDER + " Max length phased SNPs that can be called",
                    value=> options.MaxSizeMNV = int.Parse(value)
                },
                {
                    "maxgapbetweenmnv|maxrefgapinmnv=",
                    OptionTypes.INT + " Max allowed gap between phased SNPs that can be called",
                    value=> options.MaxGapBetweenMNV = int.Parse(value)
                },

                {
                    "outputsbfiles=",
                    OptionTypes.BOOL + " Output strand bias files, 'true' or 'false'",
                    value=> options.OutputBiasFiles = bool.Parse(value)
                },         
                {
                    "threadbychr=",
                    OptionTypes.BOOL + " Thread by chr. More memory intensive.  This will temporarily create output per chr.",
                    value=> options.ThreadByChr = bool.Parse(value)
                },
                {
                    "collapse=",
                    OptionTypes.BOOL + $" Whether or not to collapse variants together, 'true' or 'false'. default, {options.Collapse}",
                    value=> options.Collapse = bool.Parse(value)
                },
                {
                    "collapsefreqthreshold=",
                    OptionTypes.FLOAT+ $" When collapsing, minimum frequency required for target variants. Default {options.CollapseFreqThreshold}",
                    value=> options.CollapseFreqThreshold = float.Parse(value)
                },
                {
                    "collapsefreqratiothreshold=",
                    OptionTypes.FLOAT+ " When collapsing, minimum ratio required of target variant frequency to collapsible variant frequency. Default \'0.5f\'",
                    value=> options.CollapseFreqRatioThreshold = float.Parse(value)
                },              
                {
                    "priorspath=",
                    OptionTypes.FOLDER + " PriorsPath for vcf file containing known variants, used with -collapse to preferentially reconcile variants",
                    value=> options.PriorsPath = value
                },
                {
                    "trimmnvpriors=",
                   OptionTypes.BOOL +  " Whether or not to trim preceeding base from MNVs in priors file.  Note: COSMIC convention is to include preceeding base for MNV.  Default is false.",
                    value=> options.TrimMnvPriors = bool.Parse(value)
                },
                {
                    "coveragemethod=",
                    OptionTypes.STRING + "\'approximate\' or \'exact\'. Exact is more precise but requires more memory (minimum 8 GB).  Default approximate",
                    value=> options.CoverageMethod= ConvertToCoverageMethod(value)
                },            
                {
                    "baselogname=",
                    OptionTypes.STRING +  " ",
                    value=> options.LogFileNameBase = value
                },
                {
                    "d|debug=",
                    OptionTypes.BOOL +  "",
                    value=> options.DebugMode = bool.Parse(value)
                },
                 {
                    "usestitchedxd=",
                    OptionTypes.BOOL +  " Set to true to make use of the consensus read-direction information (the XD tag) from stitched reads. This is on by default when using Stitcher output bam, but must be deliberately set for Gemini output.",
                    value=> options.UseStitchedXDInfo = bool.Parse(value)
                },
                {
                    "trackedAnchorSize=",
                    OptionTypes.FLOAT+ " Maximum size of anchor to granularly track, when collecting reference coverage at insertion sites. If zero, all coverage counts equally. Higher values will yield more precise spanning coverage results but require more memory and compromise speed. Default \'5\'",
                    value=> options.TrackedAnchorSize = uint.Parse(value)
                },

            };

            var optionDict = new Dictionary<string, OptionSet>
            {
               {OptionSetNames.Required,requiredOps},
               {OptionSetNames.Common,commonOps },
            };

            BamProcessorParsingUtils.AddBamProcessorArgumentParsing(optionDict, options);
            BamFilterOptionsUtils.AddBamFilterArgumentParsing(optionDict, options.BamFilterParameters);
            VariantCallingOptionsParserUtils.AddVariantCallingArgumentParsing(optionDict, options.VariantCallingParameters);
            VcfWritingParserUtils.AddVcfWritingArgumentParsing(optionDict, options.VcfWritingParameters);
        
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
                BamProcessorParsingUtils.ValidateBamProcessorPaths(PiscesOptions.BAMPaths, PiscesOptions.GenomePaths, null);
                
                if (string.IsNullOrEmpty(Options.OutputDirectory))
                {
                    Options.OutputDirectory = Path.GetDirectoryName(PiscesOptions.BAMPaths[0]);
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
                ValidateAndSetDerivedValues();
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


        public void ValidateAndSetDerivedValues()
        {
            var Options = PiscesOptions;
            bool bamPathsSpecified = BamProcessorParsingUtils.ValidateBamProcessorPaths(
                Options.BAMPaths, Options.GenomePaths, Options.IntervalPaths);

            Options.SetDerivedParameters();
            Options.BamFilterParameters.Validate();
            Options.VariantCallingParameters.Validate();

            if (Options.CallMNVs)
            {
                ValidationHelper.VerifyRange(Options.MaxSizeMNV, 1, PiscesApplicationOptions.RegionSize, "MaxPhaseSNPLength");
                ValidationHelper.VerifyRange(Options.MaxGapBetweenMNV, 0, int.MaxValue, "MaxGapPhasedSNP");
            }
            ValidationHelper.VerifyRange(Options.MaxNumThreads, 1, int.MaxValue, "MaxNumThreads");
            ValidationHelper.VerifyRange(Options.CollapseFreqThreshold, 0f, float.MaxValue, "CollapseFreqThreshold");
            ValidationHelper.VerifyRange(Options.CollapseFreqRatioThreshold, 0f, float.MaxValue, "CollapseFreqRatioThreshold");

            if (!string.IsNullOrEmpty(Options.PriorsPath))
            {
                if (!File.Exists(Options.PriorsPath))
                    throw new ArgumentException(string.Format("PriorsPath '{0}' does not exist.", Options.PriorsPath));
            }



            if (Options.ThreadByChr && !Options.InsideSubProcess && !string.IsNullOrEmpty(Options.ChromosomeFilter))
                throw new ArgumentException("Cannot thread by chromosome when filtering on a particular chromosome.");

            if (!string.IsNullOrEmpty(Options.OutputDirectory) && bamPathsSpecified && (Options.BAMPaths.Length > 1))
            {
                //make sure none of the input BAMS have the same name. Or else we will have an output collision.
                for (int i = 0; i < Options.BAMPaths.Length; i++)
                {
                    for (int j = i + 1; j < Options.BAMPaths.Length; j++)
                    {
                        if (i == j)
                            continue;

                        var fileA = Path.GetFileName(Options.BAMPaths[i]);
                        var fileB = Path.GetFileName(Options.BAMPaths[j]);

                        if (fileA == fileB)
                        {
                            throw new ArgumentException(string.Format("VCF file name collision. Cannot process two different bams with the same name {0} into the same output folder {1}.", fileA, Options.OutputDirectory));
                        }
                    }
                }
            }

            if (Options.ForcedAllelesFileNames != null && Options.ForcedAllelesFileNames.Count > 0 && !Options.VcfWritingParameters.AllowMultipleVcfLinesPerLoci)
            {
                throw new ArgumentException("Cannot support forced Alleles when crushing vcf lines, please set -crushvcf false");
            }
        }

    }
}