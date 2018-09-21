using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Pisces.Domain.Options;
using Common.IO.Utility;
using CommandLine.NDesk.Options;

namespace CommandLine.Options
{
    public class BamProcessorParsingUtils 
    {


        public static Dictionary<string, OptionSet> GetBamProcessorParsingMethods(BamProcessorOptions bamProcessorOptions)
        {
            var requiredOps = new OptionSet
            {
                {
                    "b|bam|bampaths=",
                    OptionTypes.PATHS + " BAM filepath(s).  Single value or comma delimited list of multiple file paths. If a single value, it can be a full BAM path or a directory containing BAM files. No default value.",
                    value=> bamProcessorOptions.BAMPaths = UpdateBamPathsWithBamsFromFolder(value)
            }
            };

            var commonOps = new OptionSet
            {

                 {
                    "insidesubprocess=",
                    OptionTypes.BOOL + " When threading by chr, this setting flags an internal process. Default false",
                    value => bamProcessorOptions.InsideSubProcess = bool.Parse(value)
                   },                        
                {
                    "multiprocess=", 
                    OptionTypes.BOOL + " When threading by chr, launch separate processes to parallelize. Default true",
                    value => bamProcessorOptions.MultiProcess = bool.Parse(value)
                },
                {
                    "chrfilter=",
                    OptionTypes.STRING + " Chromosome to process. If provided, other chromosomes are filtered out of output.  No default value.",
                    value => bamProcessorOptions.ChromosomeFilter = value
                },
                {
                    "o|outfolder=",
                    OptionTypes.FOLDER + " Output folder.  No default value.",
                    value => bamProcessorOptions.OutputDirectory = value
                },
                {
                    "t|maxThreads|maxNumThreads=", 
                    OptionTypes.INT + " Maximum number of threads. Default 20",
                    value => bamProcessorOptions.MaxNumThreads = int.Parse(value)
                }
            };

            var optionDict = new Dictionary<string, OptionSet>
            {
                { OptionSetNames.Required,requiredOps},
                { OptionSetNames.Common,commonOps},
            };
           
            return optionDict;
        }

        
        public static void AddBamProcessorArgumentParsing(Dictionary<string, OptionSet> parsingMethods, BamProcessorOptions options)
        {
            var bamProcessorOptionDict = GetBamProcessorParsingMethods(options);


            foreach (var key in bamProcessorOptionDict.Keys)
            {
                foreach (var optSet in bamProcessorOptionDict[key])
                    parsingMethods[key].Add(optSet);
            }
        }


        public static string[] UpdateBamPathsWithBamsFromFolder(string bamPathsString)
        {
            string[] BAMPathsArray = bamPathsString.Split(',').ToArray();

            if ((BAMPathsArray != null) && (BAMPathsArray.Length == 1) && (!File.Exists(BAMPathsArray[0])))
            {
                var possibleDirectory = BAMPathsArray[0];

                if (Directory.Exists(possibleDirectory))
                {
                    var bamFilesFound = Directory.GetFiles(possibleDirectory, "*.bam");
                    if (!bamFilesFound.Any())
                        throw new ArgumentException(string.Format("No BAM files found in {0}", possibleDirectory));

                    return bamFilesFound;
                }
            }

            return BAMPathsArray;
        }

        /// <summary>
        /// TODO - break these out into missing argument, bad argument, missing file exceptions
        /// </summary>
        /// <param name="bamPaths"></param>
        /// <param name="genomePaths"></param>
        /// <param name="intervalPaths"></param>
        /// <returns></returns>
        public static bool ValidateBamProcessorPaths(string[] bamPaths, string[] genomePaths, string[] intervalPaths)
        {
            var bamPathsSpecified = bamPaths != null && bamPaths.Length > 0;


            //BAMPath(s) should be specified.
            if (!bamPathsSpecified)
                throw new ArgumentException("Specify BAMPath(s)");

            if (genomePaths == null || genomePaths.Length == 0)
                throw new ArgumentException("No GenomePaths specified.");

            if (bamPathsSpecified)
            {
                if (bamPaths.Distinct().Count() != bamPaths.Count())
                    throw new ArgumentException("Duplicate BAMPaths detected.");

                if (bamPaths.Length != genomePaths.Length && genomePaths.Length > 1)
                    throw new ArgumentException(
                        "Multiple GenomePaths specified, but number does not correspond with number of BAMPaths.");

                if (intervalPaths != null && bamPaths.Length != intervalPaths.Length && intervalPaths.Length > 1)
                    throw new ArgumentException(
                        "Multiple IntervalPaths specified, but number does not correspond with number of BAMPaths.");


                // check files and directories exist
                foreach (var bamPath in bamPaths)
                    if (!File.Exists(bamPath))
                        throw new ArgumentException(string.Format("BAM file '{0}' does not exist.", bamPath));
            }

            foreach (var genomePath in genomePaths)
                if (!Directory.Exists(genomePath))
                    throw new ArgumentException(string.Format("genomeFolder '{0}' does not exist.", genomePath));

            if (intervalPaths != null)
                foreach (var intervalPath in intervalPaths)
                    if (!File.Exists(intervalPath))
                        throw new ArgumentException(string.Format("IntervalPath '{0}' does not exist.", intervalPath));

            return true;

        }


    }
}