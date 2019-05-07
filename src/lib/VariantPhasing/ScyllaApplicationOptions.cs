using System.IO;
using Pisces.Domain.Options;
using Pisces.Domain.Types;

namespace VariantPhasing
{

    public class ScyllaApplicationOptions : VcfConsumerAppOptions
    {
        public string BamPath;
        public string GenomePath;

        public bool Debug = false;
        public int NumThreads = 20;
        public int NumReadTypes = 3;

        public ClusteringParameters ClusteringParams = new ClusteringParameters();
        public PhasableVariantCriteria PhasableVariantCriteria = new PhasableVariantCriteria();
        public SoftClipSupportParameters SoftClipSupportParams = new SoftClipSupportParameters();

        public string InputDirectory
        {
            get
            {
                if (VcfPath == null)
                    return null;

                return Path.GetDirectoryName(VcfPath);
            }

            set
            {
                _inputDirectory = value;
            }
        }
        public new void SetDerivedValues()
        {
            //taken from the old command-line parsing code
            //+
            //LogFileName = Path.GetFileName(VcfPath).Replace(".genome.vcf", ".phased.genome.log");
            _defaultLogFileNameBase = Path.GetFileName(VcfPath).Replace(".genome.vcf", ".phased.genome.log");

            if (VariantCallingParams.PloidyModel == PloidyModel.DiploidByThresholding)
                VariantCallingParams.MinimumFrequency = VariantCallingParams.DiploidSNVThresholdingParameters.MinorVF;

            if (VariantCallingParams.MinimumFrequencyFilter < VariantCallingParams.MinimumFrequency)
                VariantCallingParams.MinimumFrequencyFilter = VariantCallingParams.MinimumFrequency;

            if (VariantCallingParams.MinimumVariantQScoreFilter < VariantCallingParams.MinimumVariantQScore)
                VariantCallingParams.MinimumVariantQScoreFilter = VariantCallingParams.MinimumVariantQScore;

            //Scylla has no algortihms to do this yet. By default it goes on,
            //but for Scylla we should have it off for now.
            VariantCallingParams.AmpliconBiasFilterThreshold = null;

            base.SetDerivedValues();
        }

    }
}