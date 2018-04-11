
using Pisces.Domain.Options;

namespace VennVcf
{
    public class VennVcfOptions : VcfConsumerAppOptions //should really clean this inheritance up. we dont need VcfProcessorOptions. We just need enough to write a vcf
    {
        #region Members
        public string LogFileName = "VennVcfLog.txt";
        public string[] InputFiles;
        public string OutputDirectory = "";
        public string ConsensusFileName = "";
        public SampleAggregationParameters SampleAggregationParameters = new SampleAggregationParameters();
        public string CommandLine;
        public bool DebugMode;

        #endregion

    }
}
