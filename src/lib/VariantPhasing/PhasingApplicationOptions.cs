using System;
using System.IO;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Common.IO;
using Newtonsoft.Json;

namespace VariantPhasing
{

    public class PhasingApplicationOptions : VcfProcessorOptions
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

        public string LogFolder
        {
            get
            {
                return Path.Combine(OutFolder, DefaultLogFolderName);
            }
        }


        public override bool ParseCommandLine(string[] arguments)
        {

            var usedOptions = BamFilterParams.Parse(arguments);
            usedOptions.AddRange(VariantCallingParams.Parse(arguments));
            usedOptions.AddRange(VcfWritingParams.Parse(arguments));

            var lastArgumentField = string.Empty;
            CommandLineArguments = string.Join(" ", arguments);

            try
            {
                var argumentIndex = 0;
                while (argumentIndex < arguments.Length)
                {
                    if (string.IsNullOrEmpty(arguments[argumentIndex]))
                    {
                        argumentIndex++;
                        continue;
                    }
                    string value = null;
                    if (argumentIndex < arguments.Length - 1) value = arguments[argumentIndex + 1];

                    lastArgumentField = arguments[argumentIndex].ToLower();

                    switch (lastArgumentField)
                    {
                        case "-out":
                            OutFolder = value;
                            break;
                        case "-bam":
                            BamPath = value;
                            break;
                        case "-vcf":
                            VcfPath = value;
                            break;
                        case "-dist":
                            PhasableVariantCriteria.PhasingDistance = int.Parse(value);
                            break;
                        case "-v":
                        case "-ver":
                            var currentVersion = FileUtilities.LocalAssemblyVersion<PhasingApplicationOptions>();
                            Console.WriteLine("Version:\t" + currentVersion);
                            return false;
                        case "-h":
                        case "-help":
                            //PrintUsageInfo(); we dealt with this earlier
                            break;
                        case "-passingvariantsonly":
                            PhasableVariantCriteria.PassingVariantsOnly = bool.Parse(value);
                            break;
                        case "-hetvariantsonly":
                            PhasableVariantCriteria.HetVariantsOnly = bool.Parse(value);
                            break;
                        case "-allowclustermerging":
                            ClusteringParams.AllowClusterMerging = bool.Parse(value);
                            break;
                        case "-allowworstfitremoval":
                            ClusteringParams.AllowWorstFitRemoval = bool.Parse(value);
                            break;
                        case "-debug":
                            Debug = bool.Parse(value);
                            break;
                        case "-maxnbhdstoprocess":
                            PhasableVariantCriteria.MaxNumNbhdsToProcess = int.Parse(value);
                            break;
                        case "-t":
                        case "-maxnumthreads":
                            NumThreads = int.Parse(value);
                            break;
                        case "-clusterconstraint":
                            ClusteringParams.ClusterConstraint = int.Parse(value);
                            break;
                        case "-chr":
                            PhasableVariantCriteria.ChrToProcessArray = OptionHelpers.ListOfParamsToStringArray(value);
                            break;
                        case "-nbhd":
                            PhasableVariantCriteria.FilteredNbhdToProcess = value;
                            break;
                        default:
                            if (!(usedOptions.Contains(lastArgumentField)))
                            {
                                //lets be leniant here. Sometimes we are simply chaining paramters.

                                //PrintUsageInfo();
                                //throw new ArgumentException(string.Format("Unknown argument '{0}'", lastArgumentField));
                                //Console.WriteLine("Argument {0} not used." , lastArgumentField);
                                Common.IO.Utility.Logger.WriteWarningToLog("Argument {0} not used.", lastArgumentField);
                            }
                            break;
                    }

                    argumentIndex += 2;
                }

                if (VcfPath == null)
                    throw new ArgumentException(string.Format("VCF path is not set."));

                if (BamPath == null)
                    throw new ArgumentException(string.Format("BAM path is not set."));

                LogFileName = Path.GetFileName(VcfPath).Replace(".genome.vcf", ".phased.genome.log");

                if (VariantCallingParams.PloidyModel == PloidyModel.Diploid)
                    VariantCallingParams.MinimumFrequency = VariantCallingParams.DiploidThresholdingParameters.MinorVF;

                if (VariantCallingParams.MinimumFrequencyFilter < VariantCallingParams.MinimumFrequency)
                    VariantCallingParams.MinimumFrequencyFilter = VariantCallingParams.MinimumFrequency;

                if (VariantCallingParams.MinimumVariantQScoreFilter < VariantCallingParams.MinimumVariantQScore)
                    VariantCallingParams.MinimumVariantQScoreFilter = VariantCallingParams.MinimumVariantQScore;

            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(string.Format("Unable to parse argument {0}: {1}", lastArgumentField, ex.Message));
            }

            return true;
        }
    }
}