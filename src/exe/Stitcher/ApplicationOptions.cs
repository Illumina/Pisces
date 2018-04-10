using System.IO;
using CommandLine.NDesk.Options;
using Pisces.Domain.Options;

namespace Stitcher
{
    public class ApplicationOptions: BaseApplicationOptions
    {
        public StitcherOptions StitcherOptions;
        public string InputBam = null;
        public string OutFolder = null;
        public bool ShowVersion = false;
        public OptionSet OptionSet;
        public string DefaultLogFolderName = "StitcherLogs";
        public string LogFileName = "StitcherLog.txt";

        public ApplicationOptions()
        {
            StitcherOptions = new StitcherOptions();
        }



        public string LogFolder
        {
            get
            {
                return Path.Combine(OutFolder, DefaultLogFolderName);
            }
        }
        /* moved to StitcherAppOptionsParser
        public ApplicationOptions(string[] args)
        {
            StitcherOptions = new StitcherOptions();
            var options = new OptionSet
            {
                {"Bam=", "{PATH} to the original bam file. (Required).", o => InputBam = o},
                {"OutFolder=", "{PATH} of directory in which to create the new bam file. (Required).", o => OutFolder = o},
                {"MinBaseCallQuality=", "{INT} cutoff for which, in case of a stitching conflict, bases with qscore less than this value will automatically be disregarded in favor of the mate's bases.",  o=> StitcherOptions.MinBaseCallQuality = int.Parse(o) },
                {"FilterMinMapQuality=", "{INT} indicating reads with map quality less than this value should be filtered. Default: 1.", o=> StitcherOptions.FilterMinMapQuality = uint.Parse(o) },
                {"FilterDuplicates=", "{BOOL} indicating whether reads marked as duplicates shall be filtered. Default: true.", o => StitcherOptions.FilterDuplicates = bool.Parse(o) },
                {"FilterForProperPairs=", "{BOOL} indicating whether reads marked as not proper pairs shall be filtered. Default: false.", o => StitcherOptions.FilterForProperPairs = bool.Parse(o) },
                {"FilterUnstitchablePairs=", "{BOOL} indicating whether read pairs with incompatible CIGAR strings shall be filtered. Default: false.", o => StitcherOptions.FilterUnstitchablePairs = bool.Parse(o) },
                {"NifyUnstitchablePairs=", "{BOOL} indicating whether read pairs with incompatible CIGAR strings shall be N-ified. Default: true.", o => StitcherOptions.NifyUnstitchablePairs = bool.Parse(o) },
                {"StitchGappedPairs=", "{BOOL} indicating whehter read pairs with no overlap shall still be conceptually 'stitched'. "
                                       + "While there will be no bases in the stitched section, the forward and reverse segments shall still be linked"
                                       + "and variants in them might be phased downstream. Default: false.", o => StitcherOptions.StitchGappedPairs = bool.Parse(o) },
                {"UseSoftClippedBases=", "{BOOL} indicating whether we should allow bases softclipped from the (non-probe) ends of reads to inform stitching. Default: true.", o=> StitcherOptions.UseSoftClippedBases = bool.Parse(o) },
                {"IdentifyDuplicates=", "{BOOL} indicating whether we should check each alignment's position and sequence to see if it is a duplicate (rather than trusting the flags). Default: false.", o=> StitcherOptions.IdentifyDuplicates = bool.Parse(o) },
                {"NifyDisagreement=", "{BOOL} indicating whether or not to turn high-quality disagreeing overlap bases to Ns. Default: false.", o=> StitcherOptions.NifyDisagreements = bool.Parse(o) },
                {"Debug=", "{BOOL} indicating whether we should run in debug (verbose) mode. Default: false.", o=> StitcherOptions.Debug = bool.Parse(o) },
                {"LogFileName=", "{STRING} Name for stitcher log file. Default: StitcherLog.txt.", o=> StitcherOptions.LogFileName = o.Trim() },
                {"ThreadByChr=", "{BOOL} Whether to thread by chromosome (beta). Default: false.", o=> StitcherOptions.ThreadByChromosome  = bool.Parse(o)  },
                {"DebugSummary=", "{BOOL} indicating whether we should run in debug (verbose) mode. Default: false.", o=> StitcherOptions.DebugSummary = bool.Parse(o)  },
                {"StitchProbeSoftclips=", "{BOOL} indicating whether to allow probe softclips that overlap the mate to contribute to a stitched direction. Default: false.", o=> StitcherOptions.StitchProbeSoftclips = bool.Parse(o)  },
                {"NumThreads=", "{INT} number of threads. Default: 1.", o=> StitcherOptions.NumThreads = int.Parse(o) },
                {"SortMemoryGB=", "{FLOAT} max memory in GB used to sort the bam. Temporary files are used if memory exceeds this value. If 0, the bam will not be sorted. Default: 0.0.", o=> StitcherOptions.SortMemoryGB = float.Parse(o) },
                {"MaxReadLength=", "{INT} indicating the maximum expected length of individual reads, used to determine the maximum expected stitched read length (2*len - 1). For optimal performance, set as low as appropriate (i.e. the actual single-read length) for your data. Default: 1024.", o=> StitcherOptions.MaxReadLength = int.Parse(o)  },
                {"IgnoreReadsAboveMaxLength=", "{BOOL} indicating whether to passively ignore read pairs that would be above the max stitched length (e.g. extremely long deletions). Default: false.", o=> StitcherOptions.IgnoreReadsAboveMaxLength = bool.Parse(o)  },
                {"v|ver" ,"displays the version", o => ShowVersion = o != null }
            };

            options.Parse(args);
            OptionSet = options;
        }*/
    }
}