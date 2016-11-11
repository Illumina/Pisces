using System;
using Pisces.Domain.Utility;
using Pisces.Processing.Utility;

namespace Pisces.Processing
{
    public class BaseApplicationOptions
    {
        public string[] BAMPaths;
        public string BAMFolder;
        public string[] GenomePaths;
        public string OutputFolder;
        public string ChromosomeFilter;
        public bool InsideSubProcess;
        public bool MultiProcess = true;
        public int MaxNumThreads = 20;


        protected const char _delimiter = ',';


        protected bool UpdateOptions(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "-insidesubprocess":
                    InsideSubProcess = bool.Parse(value);
                    break;
                case "-multiprocess":
                    MultiProcess = bool.Parse(value);
                    break;
                case "-chrfilter":
                    ChromosomeFilter = value;
                    break;
                case "-bampaths":
                    BAMPaths = value.Split(_delimiter);
                    break;
                case "-bamfolder":
                    BAMFolder = value;
                    break;
                case "-outfolder":
                    OutputFolder = value;
                    break;
                case "-maxnumthreads":
                    MaxNumThreads = int.Parse(value);
                    break;
                default:
                    return false;
            }
            return true;
        }

        protected static void PrintUsageInfo()
        {
            Console.WriteLine(" -BamPaths : BAMPath(s), single value or comma delimited list");
            Console.WriteLine(" -BAMFolder : BAM parent folder");
            Console.WriteLine(" -MultiProcess : When threading by chr, launch separate processes to parallelize. Default true");
            Console.WriteLine(" -ChrFilter      : Chromosome to process. If provided, other chromosomes are filtered out of output.  No default value.");
            Console.WriteLine(" -OutFolder      : Output folder.  No default value.");
            Console.WriteLine(" -MaxNumThreads   : Maximum number of threads. Default 20");
        }
    }
}
