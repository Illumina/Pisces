using System;
using System.Linq;
using System.Collections.Generic;
using CommandLine.IO;
using CommandLine.NDesk.Options;
using CommandLine.Options;
using CommandLine.IO.Utilities;

namespace RealignIndels
{
    public class HygeaOptionParser : BaseOptionParser
    {

        public HygeaOptions HygeaOptions;

        public HygeaOptionParser()
        {
            HygeaOptions = new HygeaOptions();
        }   
    
        public override Dictionary<string, OptionSet> GetParsingMethods()
        {


            var requiredOps = new OptionSet
            {
                {
                    "genomefolders=",
                    OptionTypes.FOLDERS + "Genome folder(s).  Single value, or comma delimited list corresponding to -bamFiles.  Must be single value if -bamFolder is specified.  No default value.",
                    value=> HygeaOptions.GenomePaths = value.Split(',').ToArray()
                }
            };

            var commonOps = new OptionSet{
                {
                    "minBaseQuality=",
                    OptionTypes.INT + " Minimum basecall quality required to use a base of the read.  Default is 10.",
                    o => HygeaOptions.MinimumBaseCallQuality = int.Parse(o)
                },
                {
                    "minDenovoFreq=",
                    OptionTypes.FLOAT + " Minimum frequency to use a denovo indel as realignment target.  Default is 0.01 (1%)",
                    o => HygeaOptions.IndelFreqCutoff = float.Parse(o)
                },
                {
                    "priorsFile=",
                    OptionTypes.PATH + " Filepath for vcf file containing known priors to be used as realignment targets.  No default value.",
                    value => HygeaOptions.PriorsPath = value
                },
                {  "maxIndelSize=",
                    OptionTypes.INT + " Maximum allowed indel size for realignment.  Default value is 100.",
                    value => HygeaOptions.MaxIndelSize = int.Parse(value)
                },
                {
                    "tryThree=",
                    OptionTypes.BOOL + " Option to turn on realignment attempts to three indels.  Not recommended when there are many known priors.  Default value is false.",
                    value => HygeaOptions.TryThree = bool.Parse(value)
                },
                {
                    "remaskSoftclips=",
                    OptionTypes.BOOL + " Option to re-apply softclips to portions of the realigned read that were previously softclipped and are now M.  Default value is true.",
                    value => HygeaOptions.RemaskSoftclips = bool.Parse(value)
                },
                {
                    "skipDuplicates=",
                    OptionTypes.BOOL + " Option to skip realignment of duplicate reads.  When true, duplicates will not be realigned but will still be outputted.  Default value is false.",
                    value => HygeaOptions.SkipDuplicates = bool.Parse(value)
                },
                {
                    "skipAndRemoveDuplicates=",
                    OptionTypes.BOOL + " Option to skip realignment of duplicate reads and remove them from the bam file.  When true, duplicates will not be written to output bam at all.  Default value is true.",
                    value=> HygeaOptions.SkipAndRemoveDuplicates = bool.Parse(value)
                },
                {
                    "allowRescoringOrigZero=",
                    OptionTypes.BOOL + " Option to allow setting mapq of perfectly realigned reads (0 mismatch) to 40 even if original mapq was 0.  If false, perfectly realigned reads with original mapq between 1-20 are still assigned mapq of 40, but those with 0 are left at 0. Default value is true.",
                    value => HygeaOptions.AllowRescoringOrigZero = bool.Parse(value)
                },
                {
                    "maxRealignShift=",
                    OptionTypes.INT + " Maximum length of shift of realigned read. Realignments with a shift of >= this number, relative to the original position, will be discarded and the original alignment will be kept. Default value is 250.",
                    value => HygeaOptions.MaxRealignShift = int.Parse(value)
                },
                {
                    "tryRealignSoftclippedReads=",
                    OptionTypes.BOOL + " Whether to treat softclips as realignable, making them eligible for realignment of otherwise perfect reads, and counting against alignments when comparing them.",
                    value => HygeaOptions.TryRealignSoftclippedReads = bool.Parse(value)
                },
                {
                    "useAlignmentScorer=",
                    OptionTypes.BOOL + " When comparing alignments, whether to use the alignment scorer rather than simply prioritizing alignments that minimize mismatch, softclip, and indel in that order. Alignment scoring is a simple additive function with that sums the product of each feature with its specified coefficient. Default scorer coefficients are -1 softclip, -1 indel, -2 mismatch, and 0 for all others, but can be tuned with the Alignment Scorer Parameters as described below.",
                    value => HygeaOptions.UseAlignmentScorer = bool.Parse(value)
                },
                {
                    "mismatchCoefficient=",
                  OptionTypes.INT + " (Alignment Scorer Parameter) Coefficient for mismatch penalty. Negative number indicates penalty.",
    value => HygeaOptions.MismatchCoefficient = int.Parse(value)
                },
                {
                    "indelCoefficient=",
                    OptionTypes.INT + " (Alignment Scorer Parameter) Coefficient for indel penalty. Negative number indicates penalty.",
                    value => HygeaOptions.IndelCoefficient = int.Parse(value)
                },
                {
                    "indelLengthCoefficient=",
                    OptionTypes.INT + "(Alignment Scorer Parameter) Coefficient for number of indel bases. Negative number indicates penalty.",
                    value => HygeaOptions.IndelLengthCoefficient = int.Parse(value)
                },
                {
                    "softclipCoefficient=",
                   OptionTypes.INT + " (Alignment Scorer Parameter) Coefficient for softclip penalty. Negative number indicates penalty.",
                    value => HygeaOptions.SoftclipCoefficient = int.Parse(value)
                },
                {
                    "anchorLengthCoefficient=",
                    OptionTypes.INT + " (Alignment Scorer Parameter) Coefficient for anchor length. Positive number indicates preference for highly anchored reads.",
                    value => HygeaOptions.AnchorLengthCoefficient = int.Parse(value)
                },
                {
                    "maskPartialInsertion=",
                    OptionTypes.BOOL + " Option to softclip a partial insertion at the end of a realigned read (a complete but un-anchored insertion is allowed).  Default value is false.",
                    value => HygeaOptions.MaskPartialInsertion = bool.Parse(value)
                },
                {   "Debug=",
                    OptionTypes.BOOL + " Debug mode",
                    value => HygeaOptions.Debug = bool.Parse(value)
                },

            };


            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required,requiredOps},
                {OptionSetNames.Common,commonOps },
            };

            BamProcessorParsingMethods.AddBamProcessorArgumentParsing(optionDict, HygeaOptions);
            return optionDict;
        }


        public override void ValidateOptions()
        {

            try
            {
                BamProcessorParsingMethods.ValidateBamProcessorPaths(HygeaOptions.BAMPaths, HygeaOptions.GenomePaths, null);
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
                HygeaOptions.Validate();
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