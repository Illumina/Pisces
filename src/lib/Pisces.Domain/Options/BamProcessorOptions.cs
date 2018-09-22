using System;
using System.IO;
using System.Linq;

namespace Pisces.Domain.Options
{

    public class BamProcessorOptions : BaseApplicationOptions
    {
        public string[] BAMPaths;
        public string[] GenomePaths;
        public string ChromosomeFilter;
        public bool InsideSubProcess;
        public bool MultiProcess = true;
        public int MaxNumThreads = 20;


        public override string GetMainInputDirectory()
        {

            if (BAMPaths[0] == null || BAMPaths[0].Count() == 0)
                return null;

            return Path.GetDirectoryName(BAMPaths[0]);

        }
    }
}
