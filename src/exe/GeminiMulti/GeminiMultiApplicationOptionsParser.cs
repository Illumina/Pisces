using System;
using System.Collections.Generic;
using System.IO;
using CommandLine.NDesk.Options;
using CommandLine.Options;
using CommandLine.Util;
using Common.IO.Utility;
using Gemini;
using Gemini.Types;

namespace GeminiMulti
{
    public class GeminiMultiApplicationOptions : GeminiApplicationOptions
    {
        public int NumProcesses = 1;
        public string ExePath;
        public bool MultiProcess = true;
        public string[] Chromosomes { get; set; }
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


            var multiOptions = new OptionSet()
            {
                // GeminiMulti options
                {
                    "MultiProcess=",
                    OptionTypes.BOOL +
                    $"Whether to use multi-process, as opposed to multi-thread, processing for each chromosome. Default: true.",
                    o => ProgramOptions.MultiProcess = bool.Parse(o)
                },
                {
                    "Chromosomes=",
                    OptionTypes.LIST +
                    $"Comma-separated list of chromosomes to process, if only processing particular chromosomes. Default: empty (all chromosomes will be processed).",
                    o => ProgramOptions.Chromosomes = o.Split(",")
                },
            };


            var stitchingOptions = new OptionSet
            {
                {
                    "MinBaseCallQuality=",
                    OptionTypes.INT +
                    $" Cutoff for which, in case of a stitching conflict, bases with qscore less than this value will automatically be disregarded in favor of the mate's bases.",
                    o => ProgramOptions.StitcherOptions.MinBaseCallQuality = int.Parse(o)
                },
                {
                    "NifyDisagreement=",
                    OptionTypes.BOOL +
                    $" Whether or not to turn high-quality disagreeing overlap bases to Ns. Default: false.",
                    o => ProgramOptions.StitcherOptions.NifyDisagreements = bool.Parse(o)
                },
                {
                    "MaxReadLength=",
                    OptionTypes.INT +
                    $" Maximum expected length of individual reads, used to determine the maximum expected stitched read length (2*len - 1). For optimal performance, set as low as appropriate (i.e. the actual single-read length + max deletion length you expect to stitch) for your data. Default: 1024.",
                    o => ProgramOptions.StitcherOptions.MaxReadLength = int.Parse(o)
                },
                {
                    "DontStitchRepeatOverlap=",
                    OptionTypes.BOOL +
                    $" Whether to not stitch read pairs whose only overlap is a repeating sequence. Default: true.",
                    o => ProgramOptions.StitcherOptions.DontStitchHomopolymerBridge = bool.Parse(o)
                },
                {
                    "IgnoreReadsAboveMaxLength=",
                    OptionTypes.BOOL +
                    $" Whether to passively ignore read pairs that would be above the max stitched length (e.g. extremely long deletions). Default: false.",
                    o => ProgramOptions.StitcherOptions.IgnoreReadsAboveMaxLength = bool.Parse(o)
                },

                // Stitching - processing
                {
                    "CountNsTowardDisagreeingBases=",
                    OptionTypes.BOOL +
                    $" Whether to count overlapping-base disagreements where one of the mates reports an 'N' as a full-force disagreement (ie Nify the base if configured to do so, and count toward the number of disagreements in determining whether the stitching result should be rejected). Default: false.",
                    o => ProgramOptions.StitcherOptions.CountNsTowardDisagreeingBases = bool.Parse(o)
                },
                {
                    "MaxNumDisagreeingStitchedBases=",
                    OptionTypes.INT +
                    $" Maximum number of stitched bases that can disagree between the two reads before a stitched read is rejected. Default: int.MaxValue",
                    o => ProgramOptions.StitcherOptions.MaxNumDisagreeingBases = int.Parse(o)
                },

                {"StringTagsToKeepFromR1=", OptionTypes.LIST + $" Comma-delimited list of string tags to retain from read 1 when stitching. Default: none.",  o=>  ProgramOptions.StitcherOptions.StringTagsToKeepFromR1 = ParseCommaSeparatedFieldToList(o) },

            };

            var readFilteringOptions = new OptionSet()
            {
                // STITCHING + HYGEA OPTIONS CARRYING OVER
                {"SkipAndRemoveDups=", OptionTypes.BOOL + $" Whether to skip and remove duplicates. Default: True.",  o=>  ProgramOptions.GeminiOptions.SkipAndRemoveDups = bool.Parse(o) },

                // STITCHING OPTIONS CARRYING OVER
                {"MinMapQuality=", OptionTypes.INT+ $" Reads pairs with map quality less than this value should be filtered. If only one mate in a pair has a low map quality, it is treated as Split (or derivations thereof). Should not be negative. Default: 1.",
                    o => ProgramOptions.StitcherOptions.FilterMinMapQuality = uint.Parse(o) },

                //{"FilterPairLowMapQ=", OptionTypes.BOOL + $" indicating whether read pairs should be filtered when one or both reads are below minmapquality. Default: true.", o =>  ProgramOptions.StitcherOptions.FilterPairLowMapQ = bool.Parse(o) },
                //{"FilterPairUnmapped=", OptionTypes.BOOL + $" indicating whether read pairs should be filtered when one or both reads are not mapped. Default: false.", o =>  ProgramOptions.StitcherOptions.FilterPairUnmapped = bool.Parse(o) },
                {"FilterForProperPairs=", OptionTypes.BOOL + $" Whether reads marked as not proper pairs shall be filtered. Default: false.", o =>  ProgramOptions.StitcherOptions.FilterForProperPairs = bool.Parse(o) },

                { "TreatAbnormalOrientationAsImproper=", OptionTypes.BOOL + $" Whether to treat non-F1R2/F2R1 read pairs as improper even if flagged as properly paired. Default: False.",  o=>  ProgramOptions.GeminiOptions.TreatAbnormalOrientationAsImproper = bool.Parse(o) },

            };

            var realignmentOptions = new OptionSet()
            {
                // HYGEA OPTIONS CARRYING OVER
                //{"AllowRescoringOrigZero=", OptionTypes.BOOL + " Option to allow setting mapq of perfectly realigned reads (0 mismatch) to 40 even if original mapq was 0.  If false, perfectly realigned reads with original mapq between 1-20 are still assigned mapq of 40, but those with 0 are left at 0. Default value is true.",value => ProgramOptions.RealignmentOptions.AllowRescoringOrigZero = bool.Parse(value)},
                {"MaskPartialInsertion=", OptionTypes.BOOL + $" Option to softclip a partial insertion at the end of a realigned read (a complete but un-anchored insertion is allowed).  Default: false.",  o=>  ProgramOptions.RealignmentOptions.MaskPartialInsertion = bool.Parse(o) },
                {"MinimumUnanchoredInsertionLength=", OptionTypes.INT + " Minimum length of an unanchored insertion (i.e. no flanking reference base on one side) allowed in a realigned read. Insertions shorter than the specified length will be softclipped. Default value is 0, i.e. allowing unanchored insertions of any length. ", value => ProgramOptions.RealignmentOptions.MinimumUnanchoredInsertionLength = int.Parse(value)},

                {"SoftclipUnknownIndels=", OptionTypes.BOOL + $" Whether to softclip out unknown indels. Default: false.",  o=>  ProgramOptions.GeminiOptions.SoftclipUnknownIndels = bool.Parse(o) },

                // Realigning - assessment
                {"CheckSoftclipsForMismatches=", OptionTypes.BOOL + $" Whether to count mismatches in softclips toward total mismatches. Default: false.",  o=>  ProgramOptions.RealignmentAssessmentOptions.CheckSoftclipsForMismatches = bool.Parse(o) },
                {"TrackMismatches=", OptionTypes.BOOL + $" Whether to track and compare mismatches when realigning. Default: false.",  o=>  ProgramOptions.RealignmentAssessmentOptions.TrackActualMismatches = bool.Parse(o) },

                // Realigning - processing
                {"CategoriesToRealign=", OptionTypes.LIST + $" Category names that should be attempted to realign. Default: {string.Join(",",ProgramOptions.RealignmentOptions.CategoriesForRealignment)}",  o=>  ProgramOptions.RealignmentOptions.CategoriesForRealignment = ParseCommaSeparatedFieldToList<PairClassification>(o) },
                {"CategoriesToSnowball=", OptionTypes.LIST + $" Category names that should be attempted to snowball. Default: none.", o=>  ProgramOptions.RealignmentOptions.CategoriesForSnowballing = ParseCommaSeparatedFieldToList<PairClassification>(o) },
                {"PairAwareEverything=", OptionTypes.BOOL + $" Whether to pass everything through pair aware realignment, or just the expected categories (Disagree, FailStitch, UnstitchIndel). Default: false.",  o=>  ProgramOptions.RealignmentOptions.PairAwareEverything = bool.Parse(o) },

                // What types of reads to handle
                {"ForceHighLikelihoodRealigners=", OptionTypes.BOOL + $" Whether to force realignment in high-likelihood categories even if the neighborhood would not have been eligible for realignment. Default: false.",  o=>  ProgramOptions.GeminiOptions.ForceHighLikelihoodRealigners = bool.Parse(o) },

            };

            var indelFilteringOptionss = new OptionSet()
            {
                // Indel filtering
                {"MinPreferredSupport=", OptionTypes.INT + $" Instances of a found variant before it can be considered to realign around. Default: 3.",  o=>  ProgramOptions.IndelFilteringOptions.FoundThreshold = int.Parse(o) },
                {"MinPreferredAnchor=", OptionTypes.INT + $" Minimum anchor around indel to count an observation toward good evidence. Default: 1.",  o=>  ProgramOptions.IndelFilteringOptions.MinAnchor = int.Parse(o) },
                {"MinRequiredIndelSupport=", OptionTypes.INT + $" Don't even allow otherwise strong indels that we attempt to rescue in if they have num observations below this. Default: 0.",  o=>  ProgramOptions.IndelFilteringOptions.StrictFoundThreshold= int.Parse(o) },
                {"MinRequiredAnchor=", OptionTypes.INT + $" Don't even allow otherwise strong indels that we attempt to rescue in if they have min anchor below this. Default: 0.",  o=>  ProgramOptions.IndelFilteringOptions.StrictAnchorThreshold = int.Parse(o) },
                {"MaxMessThreshold=", OptionTypes.INT + $" Don't allow indels with average mess above this value. Default: 20.",  o=>  ProgramOptions.IndelFilteringOptions.MaxMess = int.Parse(o) },
                {"BinSize=", OptionTypes.INT + $" Size of bin within which to consider indels overlapping and eligible for pruning. Default: 0 (do not clean up).",  o=>  ProgramOptions.IndelFilteringOptions.BinSize = int.Parse(o) },
                {"RequirePositiveOutcomeForSnowball=", OptionTypes.BOOL + $" Whether to filter out indels that did not have any realignment attempts at all during snowballing (stricter than base level of filtering indels that had failed realignment attempts). Default: True.",  o=>  ProgramOptions.GeminiOptions.RequirePositiveOutcomeForSnowball = bool.Parse(o) },

            };

            var realignBinOptions = new OptionSet()
            {
                // Realignment Bin Options
                {"MessySiteThreshold=", OptionTypes.INT + $" Minimum (raw) number of messy-type reads that must be present in a neighborhood for it to be considered messy and a potential realignable neighborhood. Must also meet the frequency thresholds. Default: 1.",  o=>  ProgramOptions.GeminiOptions.MessySiteThreshold = int.Parse(o) },
                {"MessySiteWidth=", OptionTypes.INT + $" Neighborhood width to use when binning realignment eligibility signals. Default: 500.",  o=>  ProgramOptions.GeminiOptions.MessySiteWidth = int.Parse(o) },
                {"CollectDepth=", OptionTypes.BOOL + $" When collecting realignment eligibility signals, whether to collect depth to gauge frequency information. Default: True.",  o=>  ProgramOptions.GeminiOptions.CollectDepth = bool.Parse(o) },
                {"ImperfectFreqThreshold=", OptionTypes.FLOAT + $" Proportion of imperfect reads in bin below which we should not bother to realign. Should be proportional to detection limit and bin width. Default: 0.03.",  o=>  ProgramOptions.GeminiOptions.ImperfectFreqThreshold = float.Parse(o) },
                {"IndelRegionFreqThreshold=", OptionTypes.FLOAT + $" Proportion of imperfect reads in bin below which we should not bother to realign. Should be proportional to detection limit and bin width. Default: 0.01.",  o=>  ProgramOptions.GeminiOptions.IndelRegionFreqThreshold = float.Parse(o) },
                {"RegionDepthThreshold=", OptionTypes.INT + $" When collecting realignment eligibility signals and depth, minimum total number of reads in a neighborhood below which the neighborhood would be ineligible for realignment. Default: 5.",  o=>  ProgramOptions.GeminiOptions.RegionDepthThreshold = int.Parse(o) },
                {"RecalculateUsableSitesAfterSnowball=", OptionTypes.BOOL + $" Whether to recalculate site usability after snowballing. Default: True.",  o=>  ProgramOptions.GeminiOptions.RecalculateUsableSitesAfterSnowball = bool.Parse(o) },
                //{"AvoidLikelySnvs=", OptionTypes.BOOL + $"DEV USE ONLY. Default: False.",  o=>  ProgramOptions.GeminiOptions.AvoidLikelySnvs = bool.Parse(o) },

            };

            var processingOptions = new OptionSet()
            {
                // Processing
                {"ReadCacheSize=", OptionTypes.INT + $" Batch size. Default: 1000.",  o=>  ProgramOptions.GeminiOptions.ReadCacheSize = int.Parse(o) },
                {"RegionSize=", OptionTypes.INT + $" Size of genomic region to process at one time. Appropriate setting depends upon read depth, density and available memory. Default: 10000000.",  o=>  ProgramOptions.GeminiOptions.RegionSize = int.Parse(o) },
                {"NumConcurrentRegions=", OptionTypes.INT + $" Number of concurrent regions to hold in memory/process at once. Default: 1.", o=>  ProgramOptions.GeminiOptions.NumConcurrentRegions = int.Parse(o) },
                {"MaxNumThreads=", OptionTypes.INT + $" Maximum number of threads per process. Default: 1.",  o=>  ProgramOptions.StitcherOptions.NumThreads = int.Parse(o) },

            };

            var readSilencingOptions = new OptionSet()
            {
                {"DirectionalMessThreshold=", OptionTypes.FLOAT + $" Proportion of directionally messy (ForwardMessy or ReverseMessy, etc) reads in neighborhood above which we should silence the affected mates. Default: 0.2.",  o=>  ProgramOptions.GeminiOptions.DirectionalMessThreshold = float.Parse(o) },
                {"MessyMapq=", OptionTypes.INT + $" Mapping quality of reads below which, when combined with high mismatch/softclips, a read is considered a suspicious/multi-mapping messy read. Default: 30.",  o=>  ProgramOptions.GeminiOptions.MessyMapq = int.Parse(o) },
                {"SilenceSuspiciousMdReads=", OptionTypes.BOOL + $" Whether to silence read pairs whose MD tags indicate suspicion. Default: False.",  o=>  ProgramOptions.GeminiOptions.SilenceSuspiciousMdReads = bool.Parse(o) },
                {"SilenceDirectionalMessReads=", OptionTypes.BOOL + $" Whether to silence read mates which are very messy and have clean mates, given that the proportion of such reads in the neighborhood exceeds DirectionalMessThreshold. Default: False.",  o=>  ProgramOptions.GeminiOptions.SilenceDirectionalMessReads = bool.Parse(o) },
                {"SilenceMessyMapMessReads=", OptionTypes.BOOL + $" Whether to silence read pairs that are messy and have one or both mates with mapping quality below MessyMapq, given that the proportion of such reads in the neighborhood exceeds DirectionalMessThreshold. Default: False.",  o=>  ProgramOptions.GeminiOptions.SilenceMessyMapMessReads = bool.Parse(o) },

            };

            var debugOptions = new OptionSet()
            {
                // Logging options
                {"LogRegionsAndRealignments=", OptionTypes.BOOL + $" Debug option to write region stats to the log. Default: False.",  o=>  ProgramOptions.GeminiOptions.LogRegionsAndRealignments = bool.Parse(o) },
                {"LightDebug=", OptionTypes.BOOL + $" Whether to log minimal debug logging. Default: false.",  o=>  ProgramOptions.GeminiOptions.LightDebug = bool.Parse(o) },
                {"Debug=", OptionTypes.BOOL + $" Whether we should run in debug (verbose) mode. Default: false.", o=>  ProgramOptions.StitcherOptions.Debug = bool.Parse(o) },

                // Debug options
                {"KeepUnmerged=", OptionTypes.BOOL + $" Whether to keep unmerged bams, for debugging. Default: false.",  o=>  ProgramOptions.GeminiOptions.KeepUnmergedBams = bool.Parse(o) },
            };

            var readClassificationOptions = new OptionSet()
            {
                {"NumSoftclipsToBeConsideredMessy=", OptionTypes.INT + $" When classifying reads (eg imperfect, messy, directional messy), the min number of softclips that will trigger one of the messy classifications, given that softclips are not to be trusted. Default: 8.",  o=>  ProgramOptions.GeminiOptions.NumSoftclipsToBeConsideredMessy = int.Parse(o) },
                {"NumMismatchesToBeConsideredMessy=", OptionTypes.INT + $" When classifying reads (eg imperfect, messy, directional messy), the min number of mismatches that will trigger one of the messy classifications. Default: 3.",  o=>  ProgramOptions.GeminiOptions.NumMismatchesToBeConsideredMessy = int.Parse(o) },

            };

            var commonOps = new OptionSet
            {
                {"SamtoolsOldStyle=", OptionTypes.BOOL + $" Whether the provided samtools executable is the old version that uses an output prefix rather than an explicit '-o' output option (http://www.htslib.org/doc/samtools-1.1.htm). Default: false. ",  o=>  ProgramOptions.GeminiOptions.IsWeirdSamtools = bool.Parse(o) },

                // What the reads look like
                {"KeepBothSideSoftclips=", OptionTypes.BOOL + $" Whether to trust that both-side softclips are probe and should stay softclipped. Default: false.",  o=>  ProgramOptions.GeminiOptions.KeepBothSideSoftclips = bool.Parse(o) },
                {"TrustSoftclips=", OptionTypes.BOOL + $" Whether to trust softclips. If true, having softclips doesn't automatically trigger indel realignment. Also, won't try to stitch the softclips. Default: false.",  o=>  ProgramOptions.GeminiOptions.TrustSoftclips = bool.Parse(o) },
                {"KeepProbe=", OptionTypes.BOOL + $" Whether to trust that probe-side softclips are probe and should stay softclipped. Default: false.",  o=>  ProgramOptions.GeminiOptions.KeepProbeSoftclip = bool.Parse(o) },
                {"RemaskMessySoftclips=", OptionTypes.BOOL + $" If true, read-ends that were originally softclipped and are still highly mismatching to reference after realignment are re-softclipped, even if not configured to keep probe softclips. If false, only N-softclips are remasked when not keeping probe softclips.  Default value is false.",  o=>  ProgramOptions.GeminiOptions.RemaskMessySoftclips = bool.Parse(o) },

                // What to do
                {"StitchOnly=", OptionTypes.BOOL + $" Whether to only perform stitching, skipping realignment.",  o=>  ProgramOptions.GeminiOptions.StitchOnly = bool.Parse(o) },
                {"RealignOnly=", OptionTypes.BOOL + $" Whether to only perform realignment, skipping stitching.",  o=>  ProgramOptions.GeminiOptions.SkipStitching = bool.Parse(o) },

            };

            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required, requiredOps},
                {OptionSetNames.Common, commonOps},
                {"GEMINI_MULTI", multiOptions},
                {"STITCHING", stitchingOptions },
                {"READ_FILTERING", readFilteringOptions },
                {"REALIGNMENT", realignmentOptions },
                {"INDEL_FILTERING", indelFilteringOptionss },
                {"REALIGNMENT_BINS", realignBinOptions },
                {"PROCESSING", processingOptions },
                {"READ_SILENCING", readSilencingOptions },
                {"DEBUG", debugOptions },
                {"READ_CLASSIFICATION", readClassificationOptions }
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
