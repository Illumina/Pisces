using System.IO;
using Pisces.Domain.Options;

namespace VennVcf
{
    public class VennVcfOptions : VcfConsumerAppOptions //should really clean this inheritance up. we dont need VcfProcessorOptions. We just need enough to write a vcf
    {
        #region Members
        public string LogFileName = "VennVcfLog.txt";
        public string[] InputFiles;
        public string ConsensusFileName = "";
        public SampleAggregationParameters SampleAggregationParameters = new SampleAggregationParameters();
        public bool DebugMode;

        #endregion

        public override string GetMainInputDirectory()
        {
            if ((InputFiles==null) || string.IsNullOrEmpty(InputFiles[0]))
                return null;

            return Path.GetDirectoryName(InputFiles[0]);
        }
    }
}
