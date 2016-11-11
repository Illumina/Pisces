using System.IO;
using System.Xml.Serialization;
using Pisces.Domain.Types;
using Pisces.Calculators;
using Pisces.IO;

namespace VariantPhasing
{
    
    public class VcfWritingParameters
    {
        public bool OutputGvcfFile = true;
        public bool MitochondrialChrComesFirst = false; // how we order variants in the output vcf (replace some code in VcfNbhd.cs)
        public bool AllowMultipleVcfLinesPerLoci = true; //to crush or not to crush
    }

    public class VariantCallingParameters
    {
        public float MinimumFrequency = 0.01f;
        public float MinimumFrequencyFilter = 0.01f;
        public int MaximumVariantQScore = 100;
        public int MinimumVariantQScore = 20;
        public int MinimumVariantQScoreFilter = 30;
        public int MaximumGenotpyeQScore = 100;
        public int MinimumGenotpyeQScore = 0;
        public int MinimumCoverage = 10;
        public PloidyModel PloidyModel = PloidyModel.Somatic;
        public DiploidThresholdingParameters DiploidThresholdingParameters = new DiploidThresholdingParameters();
    }

    public class BamFilterParameters
    {
        public int MinimumMapQuality = 1;
        public int MinimumBaseCallQuality = 20;
        public int MinNumberVariantsInRead = 1;
        public bool RemoveDuplicates = true;
    }


    public class PhasableVariantCriteria
    {
        public bool PassingVariantsOnly = true;
        public bool HetVariantsOnly = true;
        public int PhasingDistance = 50;
        public string[] ChrToProcessArray = { };
        public string FilteredNbhdToProcess = null;  //debugging option, to given nbhds
        public int MaxNumNbhdsToProcess = -1;  //debugging option, to restrict num of nbhds we will go through
    }

    public class ClusteringParameters
    {
        public bool AllowClusterMerging = true;
        public bool AllowWorstFitRemoval = true;
        public int MinNumberAgreements = 1;  //to join a cluster. must have his num agreements.
        public int MaxNumberDisagreements = 0; //cannot have more than this num disagreements
        public int MaxNumNewClustersPerSite = 100;
        public int ClusterConstraint = -1;
    }

    public class ApplicationOptions
    {
        public string CommandLineArguments;
        public string DefaultLogFolderName = "PhasingLogs";
        public string LogFileName = "VariantPhaserLog.txt";

        public string VcfPath;// = @"D:\.\NAmix-PanCancer-65C-rep3_S6.vcf";
        public string BamPath;// = @"D:\.\NAmix-PanCancer-65C-rep3_S6.bam";
        public string OutFolder;
 
    
        public bool Debug = false;
        public int NumThreads = 20;
        public int NumReadTypes = 3;

        public ClusteringParameters ClusteringParams = new ClusteringParameters();
        public PhasableVariantCriteria PhasableVariantCriteria = new PhasableVariantCriteria();
        public VcfWritingParameters VcfWritingParams = new VcfWritingParameters();
        public VariantCallingParameters VariantCallingParams = new VariantCallingParameters();
        public BamFilterParameters BamFilterParams = new BamFilterParameters();

        public string LogFolder
        {
            get
            {
                return Path.Combine(OutFolder, DefaultLogFolderName);
            }
        }

        public enum StrandBiasModelEnum
        {
            Poisson,  //mirrors the variant Q scoring model
            Extended  //extends the Poisson model with the Binomial Theorem where Posson is undefined.
        };

        public VcfWriterConfig GetWriterConfig()
        {
            var writerConfig = new VcfWriterConfig();
            writerConfig.AllowMultipleVcfLinesPerLoci = VcfWritingParams.AllowMultipleVcfLinesPerLoci;
            writerConfig.DepthFilterThreshold = VariantCallingParams.MinimumCoverage;
            writerConfig.FrequencyFilterThreshold = VariantCallingParams.MinimumFrequencyFilter;
            writerConfig.VariantQualityFilterThreshold = VariantCallingParams.MinimumVariantQScoreFilter;
            writerConfig.MinFrequencyThreshold = VariantCallingParams.MinimumFrequency;
            writerConfig.ShouldOutputStrandBiasAndNoiseLevel = true;
            writerConfig.EstimatedBaseCallQuality = BamFilterParams.MinimumBaseCallQuality;
            writerConfig.PloidyModel = VariantCallingParams.PloidyModel;
            return writerConfig;
        }

		public void Save(string filepath)
		{
			var serializer = new XmlSerializer(typeof(ApplicationOptions));
			var outputWriter = new StreamWriter(filepath);
			serializer.Serialize(outputWriter, this);
			outputWriter.Close();
		}
	}
}
