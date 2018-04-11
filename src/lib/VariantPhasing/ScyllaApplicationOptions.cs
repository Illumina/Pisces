using System.IO;
using Pisces.Domain.Options;
using Pisces.Domain.Types;

namespace VariantPhasing
{

    public class ScyllaApplicationOptions : VcfConsumerAppOptions
    {
        public string DefaultLogFolderName = "PhasingLogs";
        public string LogFileName = "VariantPhaserLog.txt";

        public string VcfPath;
        public string BamPath;
        public string OutputDirectory;

        public bool Debug = false;
        public int NumThreads = 20;
        public int NumReadTypes = 3;

        public ClusteringParameters ClusteringParams = new ClusteringParameters();
        public PhasableVariantCriteria PhasableVariantCriteria = new PhasableVariantCriteria();



        public string LogFolder
        {
            get
            {
                return Path.Combine(OutputDirectory, DefaultLogFolderName);
            }
        }

        public new void SetDerivedValues()
        {
            //taken from the old command-line parsing code
            //+
            LogFileName = Path.GetFileName(VcfPath).Replace(".genome.vcf", ".phased.genome.log");

            if (VariantCallingParams.PloidyModel == PloidyModel.Diploid)
                VariantCallingParams.MinimumFrequency = VariantCallingParams.DiploidThresholdingParameters.MinorVF;

            if (VariantCallingParams.MinimumFrequencyFilter < VariantCallingParams.MinimumFrequency)
                VariantCallingParams.MinimumFrequencyFilter = VariantCallingParams.MinimumFrequency;

            if (VariantCallingParams.MinimumVariantQScoreFilter < VariantCallingParams.MinimumVariantQScore)
                VariantCallingParams.MinimumVariantQScoreFilter = VariantCallingParams.MinimumVariantQScore;
            //-

            base.SetDerivedValues();
        }

    }
}