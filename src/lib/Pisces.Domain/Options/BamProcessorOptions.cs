using System;
using System.IO;
using System.Linq;

namespace Pisces.Domain.Options
{
    
    public class BamProcessorOptions : BaseApplicationOptions
    {
        public string[] BAMPaths;
        public string[] GenomePaths;
        public string OutputDirectory;
        public string ChromosomeFilter;
        public bool InsideSubProcess;
        public bool MultiProcess = true;
        public int MaxNumThreads = 20;
        
        

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
