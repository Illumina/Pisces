using System;
using System.Collections.Generic;
using System.IO;
using CommandLine.NDesk.Options;
using CommandLine.Options;
using CommandLine.Util;
using Common.IO.Utility;
using Gemini;
using Gemini.Types;
using Stitcher;

namespace GeminiMulti
{
    public class GeminiMultiApplicationOptions : GeminiApplicationOptions
    {
        public int NumProcesses = 1;
        public string ExePath;

    }

    public class GeminiMultiApplicationOptionsParser : GeminiApplicationOptionsParser
    {

        public GeminiMultiApplicationOptionsParser()
        {
            Options = new GeminiMultiApplicationOptions();
        }

        public new GeminiMultiApplicationOptions ProgramOptions { get => (GeminiMultiApplicationOptions)Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {"Bam=", OptionTypes.PATH + $" to the original bam file. (Required).", o => ProgramOptions.InputBam = o},
                {"Genome=", OptionTypes.PATH + $" to the genome directory. (Required).", o => ProgramOptions.GeminiOptions.GenomePath = o},
                {"Samtools=", OptionTypes.PATH + $" to the samtools executable. (Required).", o => ProgramOptions.GeminiOptions.SamtoolsPath = o},
                {"NumProcesses=", OptionTypes.INT + $" indicating the number of Gemini subprocesses to run. (Required).", o => ProgramOptions.NumProcesses = int.Parse(o)},
                {"ExePath=", OptionTypes.PATH + $" to the executable file for the Gemini subprocess. (Required).", o => ProgramOptions.ExePath = o},
                {"OutFolder=", OptionTypes.PATH + $" of directory in which to create the new bam file. (Required).", o => ProgramOptions.OutputDirectory = o},
            };


            // TODO: It's weird to me that these are called "common", when really they are app-specific. Not a big deal but something to follow up on.
            var commonOps = new OptionSet
            {
                // STITCHING + HYGEA OPTIONS CARRYING OVER
                {"MinBaseCallQuality=", OptionTypes.INT + $" cutoff for which, in case of a stitching conflict, bases with qscore less than this value will automatically be disregarded in favor of the mate's bases.",  o=>  ProgramOptions.StitcherOptions.MinBaseCallQuality = int.Parse(o) },
                {"FilterDuplicates=",OptionTypes.BOOL + $"  indicating whether reads marked as duplicates shall be filtered. Default: true.", o =>  ProgramOptions.StitcherOptions.FilterDuplicates = bool.Parse(o) }, // In hygea, there was skip duplicates and skipandremove duplicates. Condense to one. It's weird that we split it up to start with.
                {"Debug=", OptionTypes.BOOL + $" indicating whether we should run in debug (verbose) mode. Default: false.", o=>  ProgramOptions.StitcherOptions.Debug = bool.Parse(o) },

                // STITCHING OPTIONS CARRYING OVER
                //{"MinMapQuality=", OptionTypes.INT+ $"  indicating reads with map quality less than this value should be filtered. Should not be negative. Default: 1.", o=> ProgramOptions.StitcherOptions.FilterMinMapQuality = uint.Parse(o) },
                //{"FilterPairLowMapQ=", OptionTypes.BOOL + $" indicating whether read pairs should be filtered when one or both reads are below minmapquality. Default: true.", o =>  ProgramOptions.StitcherOptions.FilterPairLowMapQ = bool.Parse(o) },
                {"FilterPairUnmapped=", OptionTypes.BOOL + $" indicating whether read pairs should be filtered when one or both reads are not mapped. Default: false.", o =>  ProgramOptions.StitcherOptions.FilterPairUnmapped = bool.Parse(o) },
                {"FilterForProperPairs=", OptionTypes.BOOL + $" indicating whether reads marked as not proper pairs shall be filtered. Default: false.", o =>  ProgramOptions.StitcherOptions.FilterForProperPairs = bool.Parse(o) },
                //{"FilterUnstitchablePairs=", OptionTypes.BOOL + $" indicating whether read pairs with incompatible CIGAR strings shall be filtered. Default: false.", o =>  ProgramOptions.StitcherOptions.FilterUnstitchablePairs = bool.Parse(o) },
                //{"NifyUnstitchablePairs=", OptionTypes.BOOL + $" indicating whether read pairs with incompatible CIGAR strings shall be N-ified. Default: true.", o =>  ProgramOptions.StitcherOptions.NifyUnstitchablePairs = bool.Parse(o) },
                {"NifyDisagreement=", OptionTypes.BOOL + $" indicating whether or not to turn high-quality disagreeing overlap bases to Ns. Default: false.", o=>  ProgramOptions.StitcherOptions.NifyDisagreements = bool.Parse(o) },
                //{"LogFileName=", OptionTypes.STRING + $" Name for stitcher log file. Default: StitcherLog.txt.", o=>  ProgramOptions.LogFileNameBase = o.Trim() }, // TODO is this necessary?
                {"DebugSummary=", OptionTypes.BOOL + $" indicating whether we should run in debug (verbose) mode. Default: false.", o=>  ProgramOptions.StitcherOptions.DebugSummary = bool.Parse(o)  },
                {"NumThreads=", OptionTypes.INT + $" number of threads. Default: 1.", o=>  ProgramOptions.StitcherOptions.NumThreads = int.Parse(o) },
                //{"SortMemoryGB=",  OptionTypes.FLOAT + $" max memory in GB used to sort the bam. Temporary files are used if memory exceeds this value. If 0, the bam will not be sorted. Default: 0.0.", o=>  ProgramOptions.StitcherOptions.SortMemoryGB = float.Parse(o) },
                {"MaxReadLength=", OptionTypes.INT + $" indicating the maximum expected length of individual reads, used to determine the maximum expected stitched read length (2*len - 1). For optimal performance, set as low as appropriate (i.e. the actual single-read length) for your data. Default: 1024.", o=>  ProgramOptions.StitcherOptions.MaxReadLength = int.Parse(o)  },
                {"DontStitchRepeatOverlap=", OptionTypes.BOOL+ $" indicating whether to not stitch read pairs whos overlap is a repeating sequence. Default: true.", o=> ProgramOptions.StitcherOptions.DontStitchHomopolymerBridge = bool.Parse(o)  },
                {"IgnoreReadsAboveMaxLength=", OptionTypes.BOOL + $" indicating whether to passively ignore read pairs that would be above the max stitched length (e.g. extremely long deletions). Default: false.", o=>  ProgramOptions.StitcherOptions.IgnoreReadsAboveMaxLength = bool.Parse(o)  },

                // HYGEA OPTIONS CARRYING OVER
                {"MaxIndelSize=", OptionTypes.INT + $" Maximum allowed indel size for realignment. Default value is 100.",  o=>  ProgramOptions.RealignmentOptions.MaxIndelSize = int.Parse(o) },
                { "AllowRescoringOrigZero=",
                OptionTypes.BOOL + " Option to allow setting mapq of perfectly realigned reads (0 mismatch) to 40 even if original mapq was 0.  If false, perfectly realigned reads with original mapq between 1-20 are still assigned mapq of 40, but those with 0 are left at 0. Default value is true.",
                value => ProgramOptions.RealignmentOptions.AllowRescoringOrigZero = bool.Parse(value)},
                {"MaskPartialInsertion=", OptionTypes.BOOL + $" Option to softclip a partial insertion at the end of a realigned read (a complete but un-anchored insertion is allowed).  Default value is false.",  o=>  ProgramOptions.RealignmentOptions.MaskPartialInsertion = bool.Parse(o) },
                {
                    "MinimumUnanchoredInsertionLength=",
                    OptionTypes.INT + " Minimum length of an unanchored insertion (i.e. no flanking reference base on one side) allowed in a realigned read. Insertions shorter than the specified length will be softclipped. Default value is 0, i.e. allowing unanchored insertions of any length. ",
                    value => ProgramOptions.RealignmentOptions.MinimumUnanchoredInsertionLength = int.Parse(value)
                },

                // NEW GEMINI OPTIONS
                // Indel filtering
                {"MinPreferredSupport=", OptionTypes.INT + $" Instances of a found variant before it can be considered to realign around. Default: 3.",  o=>  ProgramOptions.IndelFilteringOptions.FoundThreshold = int.Parse(o) },
                {"MinPreferredAnchor=", OptionTypes.INT + $" Minimum anchor around indel to count an observation toward good evidence. Default: 1.",  o=>  ProgramOptions.IndelFilteringOptions.MinAnchor = uint.Parse(o) },
                {"MinRequiredIndelSupport=", OptionTypes.INT + $" Don't even allow otherwise strong indels that we attempt to rescue in if they have num observations below this. Default: 0.",  o=>  ProgramOptions.IndelFilteringOptions.StrictFoundThreshold= int.Parse(o) },
                {"MinRequiredAnchor=", OptionTypes.INT + $" Don't even allow otherwise strong indels that we attempt to rescue in if they have min anchor below this. Default: 0.",  o=>  ProgramOptions.IndelFilteringOptions.StrictAnchorThreshold = int.Parse(o) },
                {"MaxMessThreshold=", OptionTypes.INT + $" Don't allow indels with average mess above this value. Default: 20.",  o=>  ProgramOptions.IndelFilteringOptions.MaxMess = int.Parse(o) },
                {"BinSize=", OptionTypes.INT + $" Size of bin within which to compare indels to determine if a region is too messy to realign around. Default: 0 (do not clean up).",  o=>  ProgramOptions.IndelFilteringOptions.BinSize = int.Parse(o) },

                // What the reads look like
                {"KeepBothSideSoftclips=", OptionTypes.BOOL + $" Whether to trust that both-side softclips are probe and should stay softclipped. Default: false.",  o=>  ProgramOptions.GeminiOptions.KeepBothSideSoftclips = bool.Parse(o) },
                {"TrustSoftclips=", OptionTypes.BOOL + $" Whether to trust softclips. If true, having softclips doesn't automatically trigger indel realignment. Also, won't try to stitch the softclips. Default: false.",  o=>  ProgramOptions.GeminiOptions.TrustSoftclips = bool.Parse(o) },
                {"KeepProbe=", OptionTypes.BOOL + $" Whether to trust that probe-side softclips are probe and should stay softclipped. Default: false.",  o=>  ProgramOptions.GeminiOptions.KeepProbeSoftclip = bool.Parse(o) },

                // Realigning - assessment
                {"CheckSoftclipsForMismatches=", OptionTypes.BOOL + $" Whether to count mismatches in softclips toward total mismatches. Default: false.",  o=>  ProgramOptions.RealignmentAssessmentOptions.CheckSoftclipsForMismatches = bool.Parse(o) },
                {"TrackMismatches=", OptionTypes.BOOL + $" Whether to track and compare mismatches when realigning. Default: false.",  o=>  ProgramOptions.RealignmentAssessmentOptions.TrackActualMismatches = bool.Parse(o) },

                // Realigning - processing
                {"CategoriesToRealign=", OptionTypes.LIST + $" Category names that should be attempted to realign. Default: ImperfectStitched,FailStitch,UnstitchIndel,Unstitchable,Split,Disagree",  o=>  ProgramOptions.RealignmentOptions.CategoriesForRealignment = ParseCommaSeparatedFieldToList<PairClassification>(o) },
                {"CategoriesToSnowball=", OptionTypes.LIST + $" Category names that should be attempted to snowball. Default: none.", o=>  ProgramOptions.RealignmentOptions.CategoriesForSnowballing = ParseCommaSeparatedFieldToList<PairClassification>(o) },
                {"NumShardsToSnowball=", OptionTypes.INT + $" For multithreaded samples, the number of shards from which to gather evidence in snowballing. Default: all.",  o=>  ProgramOptions.RealignmentOptions.NumSubSamplesForSnowballing = int.Parse(o) },
                {"PairAwareEverything=", OptionTypes.BOOL + $" Whether to pass everything through pair aware realignment, or just the expected categories. Default: false.",  o=>  ProgramOptions.RealignmentOptions.PairAwareEverything = bool.Parse(o) },

                // Stitching - processing
                {"DeferIndelStitch=", OptionTypes.BOOL + $" Whether to stitch non-disagreeing indel reads to start, or wait til realignment.",  o=>  ProgramOptions.GeminiOptions.DeferStitchIndelReads = bool.Parse(o) },
                {"MaxNumDisagreeingStitchedBases=", OptionTypes.INT + $" Maximum number of stitched bases that can disagree between the two reads before a stitched read is rejected.",  o=>  ProgramOptions.StitcherOptions.MaxNumDisagreeingBases = int.Parse(o) },

                {"SamtoolsOldStyle=", OptionTypes.BOOL + $" Whether the provided samtools executable is the old version that uses an output prefix rather than an explicit '-o' output option (http://www.htslib.org/doc/samtools-1.1.htm). Default: false. ",  o=>  ProgramOptions.GeminiOptions.IsWeirdSamtools = bool.Parse(o) },

                {"StitchOnly=", OptionTypes.BOOL + $" Whether to only perform stitching, skipping realignment.",  o=>  ProgramOptions.GeminiOptions.StitchOnly = bool.Parse(o) },
                {"RealignOnly=", OptionTypes.BOOL + $" Whether to only perform realignment, skipping stitching.",  o=>  ProgramOptions.GeminiOptions.SkipStitching = bool.Parse(o) },

                {"SoftclipUnknownIndels=", OptionTypes.BOOL + $" Whether to softclip out unknown indels. Default: false.",  o=>  ProgramOptions.GeminiOptions.SoftclipUnknownIndels = bool.Parse(o) },


            };



            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required, requiredOps},
                {OptionSetNames.Common, commonOps},
            };


            return optionDict;
        }


        public override void ValidateOptions()
        {

            if (string.IsNullOrEmpty(ProgramOptions.OutputDirectory))
            {
                ProgramOptions.OutputDirectory = Path.GetDirectoryName(ProgramOptions.InputBam);
            }

            if (!Directory.Exists(ProgramOptions.OutputDirectory))
            {
                try
                {
                    //lets be nice...
                    Directory.CreateDirectory(ProgramOptions.OutputDirectory);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine("Validation Error: Unable to create the OutFolder.");
                    Console.WriteLine(ex);
                }
            }

            var doExit = ProgramOptions.InputBam == null || ProgramOptions.OutputDirectory == null || !File.Exists(ProgramOptions.InputBam) || !Directory.Exists(ProgramOptions.OutputDirectory);

            if (doExit)
            {
                var userException = new ArgumentException("Validation Error: You must supply a valid Bam and OutFolder.");
                Logger.WriteExceptionToLog(userException);

                ParsingResult.ErrorBuilder.AppendFormat("{0}ERROR: {1}\n", ParsingResult.ErrorSpacer, userException.Message);
                ParsingResult.UpdateExitCode(ExitCodeType.UnknownCommandLineOption);
                ParsingResult.ShowHelpMenu = true;
                ParsingResult.Exception = userException;

                ExitCodeUtilities.ShowExceptionAndUpdateExitCode(userException);
            }
        }
    }
}