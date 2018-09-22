using System.IO;
using Pisces.Domain.Options;
using Pisces.Domain.Types;

namespace VariantPhasing
{

    public class ScyllaApplicationOptions : VcfConsumerAppOptions
    {
       
        public string VcfPath;
        public string BamPath;
      
        public bool Debug = false;
        public int NumThreads = 20;
        public int NumReadTypes = 3;

        public ClusteringParameters ClusteringParams = new ClusteringParameters();
        public PhasableVariantCriteria PhasableVariantCriteria = new PhasableVariantCriteria();


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

            if (VariantCallingParams.PloidyModel == PloidyModel.Diploid)
                VariantCallingParams.MinimumFrequency = VariantCallingParams.DiploidSNVThresholdingParameters.MinorVF;

            if (VariantCallingParams.MinimumFrequencyFilter < VariantCallingParams.MinimumFrequency)
                VariantCallingParams.MinimumFrequencyFilter = VariantCallingParams.MinimumFrequency;

            if (VariantCallingParams.MinimumVariantQScoreFilter < VariantCallingParams.MinimumVariantQScore)
                VariantCallingParams.MinimumVariantQScoreFilter = VariantCallingParams.MinimumVariantQScore;
            //-

            base.SetDerivedValues();
        }

    }
}