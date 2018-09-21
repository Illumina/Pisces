using System;
using System.IO;
using System.Collections.Generic;
using Common.IO.Utility;
using CommandLine.Options;
using CommandLine.NDesk.Options;
using CommandLine.Util;

namespace Stitcher
{
    public class StitcherApplicationOptionsParser : BaseOptionParser
    {
        
        public StitcherApplicationOptionsParser()
        {
            Options = new StitcherApplicationOptions();
        }

        public StitcherApplicationOptions ProgramOptions { get => (StitcherApplicationOptions)Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
              {"Bam=", OptionTypes.PATH + $" to the original bam file. (Required).", o => ProgramOptions.InputBam = o},
            };

            var commonOps = new OptionSet
            {
                {"OutFolder=", OptionTypes.PATH + $" of directory in which to create the new bam file. (Required).", o => ProgramOptions.OutputDirectory = o},
                {"MinBaseCallQuality=", OptionTypes.INT + $" cutoff for which, in case of a stitching conflict, bases with qscore less than this value will automatically be disregarded in favor of the mate's bases.",  o=>  ProgramOptions.StitcherOptions.MinBaseCallQuality = int.Parse(o) },
                {"MinMapQuality=", OptionTypes.INT+ $"  indicating reads with map quality less than this value should be filtered. Should not be negative. Default: 1.", o=> ProgramOptions.StitcherOptions.FilterMinMapQuality = uint.Parse(o) },
                {"FilterPairLowMapQ=", OptionTypes.BOOL + $" indicating whether read pairs should be filtered when one or both reads are below minmapquality. Default: true.", o =>  ProgramOptions.StitcherOptions.FilterPairLowMapQ = bool.Parse(o) },
                {"FilterPairUnmapped=", OptionTypes.BOOL + $" indicating whether read pairs should be filtered when one or both reads are not mapped. Default: false.", o =>  ProgramOptions.StitcherOptions.FilterPairUnmapped = bool.Parse(o) },
                {"FilterDuplicates=",OptionTypes.BOOL + $"  indicating whether reads marked as duplicates shall be filtered. Default: true.", o =>  ProgramOptions.StitcherOptions.FilterDuplicates = bool.Parse(o) },
                {"FilterForProperPairs=", OptionTypes.BOOL + $" indicating whether reads marked as not proper pairs shall be filtered. Default: false.", o =>  ProgramOptions.StitcherOptions.FilterForProperPairs = bool.Parse(o) },
                {"FilterUnstitchablePairs=", OptionTypes.BOOL + $" indicating whether read pairs with incompatible CIGAR strings shall be filtered. Default: false.", o =>  ProgramOptions.StitcherOptions.FilterUnstitchablePairs = bool.Parse(o) },
                {"NifyUnstitchablePairs=", OptionTypes.BOOL + $" indicating whether read pairs with incompatible CIGAR strings shall be N-ified. Default: true.", o =>  ProgramOptions.StitcherOptions.NifyUnstitchablePairs = bool.Parse(o) },
                {"StitchGappedPairs=", OptionTypes.BOOL + $" indicating whehter read pairs with no overlap shall still be conceptually 'stitched'. "
                                       + "While there will be no bases in the stitched section, the forward and reverse segments shall still be linked"
                                       + "and variants in them might be phased downstream. Default: false.", o =>  ProgramOptions.StitcherOptions.StitchGappedPairs = bool.Parse(o) },
                {"UseSoftClippedBases=", OptionTypes.BOOL + $" indicating whether we should allow bases softclipped from the (non-probe) ends of reads to inform stitching. Default: true.", o=>  ProgramOptions.StitcherOptions.UseSoftClippedBases = bool.Parse(o) },
                {"IdentifyDuplicates=", OptionTypes.BOOL + $" indicating whether we should check each alignment's position and sequence to see if it is a duplicate (rather than trusting the flags). Default: false.", o=>  ProgramOptions.StitcherOptions.IdentifyDuplicates = bool.Parse(o) },
                {"NifyDisagreement=", OptionTypes.BOOL + $" indicating whether or not to turn high-quality disagreeing overlap bases to Ns. Default: false.", o=>  ProgramOptions.StitcherOptions.NifyDisagreements = bool.Parse(o) },
                {"Debug=", OptionTypes.BOOL + $" indicating whether we should run in debug (verbose) mode. Default: false.", o=>  ProgramOptions.StitcherOptions.Debug = bool.Parse(o) },
                {"LogFileName=", OptionTypes.STRING + $" Name for stitcher log file. Default: StitcherLog.txt.", o=>  ProgramOptions.LogFileNameBase = o.Trim() },
                {"ThreadByChr=", OptionTypes.BOOL + $" Whether to thread by chromosome (beta). Default: false.", o=>  ProgramOptions.StitcherOptions.ThreadByChromosome  = bool.Parse(o)  },
                {"DebugSummary=", OptionTypes.BOOL + $" indicating whether we should run in debug (verbose) mode. Default: false.", o=>  ProgramOptions.StitcherOptions.DebugSummary = bool.Parse(o)  },
                {"StitchProbeSoftclips=", OptionTypes.BOOL + $" indicating whether to allow probe softclips that overlap the mate to contribute to a stitched direction. Default: false.", o=>  ProgramOptions.StitcherOptions.StitchProbeSoftclips = bool.Parse(o)  },
                {"NumThreads=", OptionTypes.INT + $" number of threads. Default: 1.", o=>  ProgramOptions.StitcherOptions.NumThreads = int.Parse(o) },
                {"SortMemoryGB=",  OptionTypes.FLOAT + $" max memory in GB used to sort the bam. Temporary files are used if memory exceeds this value. If 0, the bam will not be sorted. Default: 0.0.", o=>  ProgramOptions.StitcherOptions.SortMemoryGB = float.Parse(o) },
                {"MaxReadLength=", OptionTypes.INT + $" indicating the maximum expected length of individual reads, used to determine the maximum expected stitched read length (2*len - 1). For optimal performance, set as low as appropriate (i.e. the actual single-read length) for your data. Default: 1024.", o=>  ProgramOptions.StitcherOptions.MaxReadLength = int.Parse(o)  },
                {"DontStitchRepeatOverlap=", OptionTypes.BOOL+ $" indicating whether to not stitch read pairs whos overlap is a repeating sequence. Default: true.", o=> ProgramOptions.StitcherOptions.DontStitchHomopolymerBridge = bool.Parse(o)  },
                { "IgnoreReadsAboveMaxLength=", OptionTypes.BOOL + $" indicating whether to passively ignore read pairs that would be above the max stitched length (e.g. extremely long deletions). Default: false.", o=>  ProgramOptions.StitcherOptions.IgnoreReadsAboveMaxLength = bool.Parse(o)  },
            };



            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required,requiredOps},
                {OptionSetNames.Common,commonOps },
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