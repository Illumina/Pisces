using System;
using System.IO;
using System.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using CallSomaticVariants.Logic.Processing;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;
using Constants = CallSomaticVariants.Types.Constants;

namespace CallSomaticVariants
{
    /// <summary>
    ///     Options to the somatic variant caller: Mostly thresholds for various filters.
    ///     The filter cutoffs will NOT be exposed to the customer, but we'll be exploring
    ///     various combinations internally.
    /// </summary>
    // ReSharper disable InconsistentNaming - prevents ReSharper from renaming serializeable members that are sensitive to being changed
    public class ApplicationOptions
    {
        private const char _delimiter = ',';
        public const string DefaultLogFolderName = "VariantCallingLogs";
        public const string LogFileName = "SomaticVariantCallerLog.txt";

        #region Serializeable Types and Members

        public string CommandLineArguments;

        public int AppliedNoiseLevel = -1;
        public int MinimumBaseCallQuality = 20;
        public int MaximumVariantQScore = 100;
        public int FilteredVariantQScore = 30;
        public int MinimumVariantQScore = 20; // We don't bother reporting variants with a lower Q-score.
        public int MinimumCoverage = 10;
        public int MinimumMapQuality = 1;
        public float MinimumFrequency = 0.01f;
        public float StrandBiasAcceptanceCriteria = 0.5f; // Flag variants as filtered if they have greater strand bias than this
        public double StrandBiasScoreMinimumToWriteToVCF = -100; // just so we dont have to write negative infinities into vcfs and then they get tagged as "poorly formed"  
        public double StrandBiasScoreMaximumToWriteToVCF = 0;
        public bool FilterOutVariantsPresentOnlyOneStrand = false;
        public string[] GenomePaths;
        public string[] BAMPaths;
        public string BAMFolder;
        public string[] IntervalPaths;
        public string OutputFolder;
        public bool OutputBiasFiles = false;
        public bool OutputgVCFFiles = false;
        public bool OnlyUseProperPairs = false;
        public int MaxNumThreads = 10;
        public bool ThreadByChr = false; //more memory intensive
        public int NoiseModelHalfWindow = 1;
        public bool DebugMode = false;
        public bool CallMNVs = false;
        public bool ReportNoCalls = false;
        public int MaxSizeMNV = 15;
        public int MaxGapBetweenMNV = 10;
        public bool UseMNVReallocation = true;
        public bool StitchReads = false;
        public BamQCOptions DoBamQC = BamQCOptions.VarCallOnly;
        public NoiseModel NoiseModel = NoiseModel.Flat;
        public GenotypeModel GTModel = GenotypeModel.Symmetrical;
        public StrandBiasModel StrandBiasModel = StrandBiasModel.Extended;
        public string MonoPath; //only needed if running on Linux cluster, and we plan to spawn processes
        public bool RequireXCTagToStitch = true;
        public bool UseXCStitcher = true;

        public string LogFolder {
            get
            {
                if (BAMPaths == null || BAMPaths.Length == 0)
                    throw new ApplicationException("Unable to start logging: cannot determine log folder");

                return OutputFolder ?? Path.Combine(Path.GetDirectoryName(BAMPaths[0]), DefaultLogFolderName);
            }
        }

        #endregion
        // ReSharper restore InconsistentNaming

        static public void PrintUsageInfo()
        {
            Console.WriteLine("Options:");
            Console.WriteLine(" -a MinimumVariantQScore to report variant");
            Console.WriteLine(" -b MinimumBaseCallQuality to use a base of the read");
            Console.WriteLine(" -B BAMPath(s), single value or comma delimited list");
            Console.WriteLine(" -BAMFolder BAM parent folder");
            Console.WriteLine(" -c MinimumCoverage to call a variant");
            Console.WriteLine(" -f MinimumFrequency to call a variant");
            Console.WriteLine(" -fo Flag variants as filtered if coverage limited to one strand");
            Console.WriteLine(" -F FilteredVariantQScore to report variant as filtered");
            Console.WriteLine(" -i IntervalPath(s), single value or comma delimited list corresponding to BAMPath(s). At most one value should be provided if BAM folder is specified");
            Console.WriteLine(" -m MinimumMapQuality required to use a read");
            Console.WriteLine(" -g GenomePath(s), single value or comma delimited list corresponding to BAMPath(s). Must be single value if BAM folder is specified");
            Console.WriteLine(" -n NoiseModel, 'window' or 'flat'");
            Console.WriteLine(" -o Output strand bias files, 'true' or 'false'");
            Console.WriteLine(" -p only use proper pairs, 'true' or 'false'");
            Console.WriteLine(" -q MaximumVariantQScore to cap output variant Qscores");
            Console.WriteLine(" -s Strand bias cutoff");
            Console.WriteLine(" -StitchPairedReads Stitch overlapping region of paired reads, dev use only, 'true' or 'false'");
            Console.WriteLine(" -t ThreadCount");
            Console.WriteLine(" -ThreadByChr thread by chr, as well as by sample. more memory intensive.");
            Console.WriteLine(" -gVCF Output gVCF files, 'true' or 'false'");
            Console.WriteLine(" -PhaseSNPs call phased SNPs, 'true' or 'false'");
            Console.WriteLine(" -MaxPhaseSNPLength max length phased SNPs that can be called");
            Console.WriteLine(" -MaxGapPhasedSNP max allowed gap between phased SNPs that can be called");
            Console.WriteLine(" -OutFolder 'myoutpath'");
            Console.WriteLine(" -LogFolder 'mylogpath'");
            Console.WriteLine(" -ReportNoCalls 'true' or 'false'. default, false");
            Console.WriteLine(" -requireXC RequireXCTagToStitch,, dev use only, 'true' or 'false'. default, true");
            Console.WriteLine(" -xcStitcher UseXCStitcher, dev use only, 'true' or 'false'. default, true ");
        }

        public static ApplicationOptions ParseCommandLine(string[] arguments)
        {
            var options = new ApplicationOptions();
            options.UpdateOptions(arguments);
            options.Validate();
            if (!string.IsNullOrWhiteSpace(options.BAMFolder))
                options.PopulateBAMPaths();

            int processorCoreCount = Environment.ProcessorCount;
            if (options.MaxNumThreads > 0)
                options.MaxNumThreads = Math.Min(processorCoreCount, options.MaxNumThreads);

            return options;
        }

        public void Validate()
        {
            var bamPathsSpecified = BAMPaths != null && BAMPaths.Length > 0;
            var bamFolderSpecified = !string.IsNullOrWhiteSpace(BAMFolder);

            //Either BAMPath(s) or BAMFolder should be specified.
            if (!bamPathsSpecified && !bamFolderSpecified)
                throw new ArgumentException("Specify either BAMPath(s) or BAMFolder");

            if (bamPathsSpecified && bamFolderSpecified)
                throw new ArgumentException("Specify either BAMPath(s) or BAMFolder");

            if (GenomePaths == null || GenomePaths.Length == 0)
                throw new ArgumentException("No GenomePaths specified.");

            if (bamPathsSpecified)
            {
                if (BAMPaths.Length > 96)
                    throw new ArgumentException("Number of BAMPaths specified exceeds maximum of 96.");

                if (BAMPaths.Distinct().Count() != BAMPaths.Count())
                    throw new ArgumentException("Duplicate BAMPaths detected.");

                if (BAMPaths.Length != GenomePaths.Length && GenomePaths.Length > 1)
                    throw new ArgumentException(
                        "Multiple GenomePaths specified, but number does not correspond with number of BAMPaths.");

                if (IntervalPaths != null && BAMPaths.Length != IntervalPaths.Length && IntervalPaths.Length > 1)
                    throw new ArgumentException(
                        "Multiple IntervalPaths specified, but number does not correspond with number of BAMPaths.");

                // check files and directories exist
                foreach (var bamPath in BAMPaths)
                    if (!File.Exists(bamPath))
                        throw new ArgumentException(string.Format("BAMPath '{0}' does not exist.", bamPath));

                if (ThreadByChr && BAMPaths.Length > 1)
                    throw new ArgumentException("Threading by chromosome is only supported for single BAMPath input.");
            }

            if (bamFolderSpecified)
            {
                if (!Directory.Exists(BAMFolder))
                    throw new ArgumentException(string.Format("BAMFolder {0} not found", BAMFolder));

                if (GenomePaths.Length > 1)
                    throw new ArgumentException("Only 1 Genome Path should be specified when BAMFolder is specified.");

                if (IntervalPaths != null && IntervalPaths.Length > 1)
                    throw new ArgumentException("At most 1 Interval Path should be specified when BAMFolder is specified.");

                if (ThreadByChr)
                    throw new ArgumentException("Threading by chromosome is not supported when BAMFolder is specified.");
            }

            foreach (var genomePath in GenomePaths)
                if (!Directory.Exists(genomePath))
                    throw new ArgumentException(string.Format("GenomePath '{0}' does not exist.", genomePath));

            if (IntervalPaths != null)
                foreach (var intervalPath in IntervalPaths)
                    if (!File.Exists(intervalPath))
                        throw new ArgumentException(string.Format("IntervalPath '{0}' does not exist.", intervalPath));

            VerifyRange(MinimumVariantQScore, 0, 100, "MinimumVariantQscore");
            VerifyRange(MaximumVariantQScore, 0, 100, "MaximumVariantQScore");
            if (MaximumVariantQScore < MinimumVariantQScore)
                throw new ArgumentException("MinimumVariantQScore must be less than or equal to MaximumVariantQScore.");
            VerifyRange(MinimumBaseCallQuality, 0, null, "MinimumBaseCallQuality");
            VerifyRange(MinimumFrequency, 0f, 1f, "MinimumFrequency");
            VerifyRange(FilteredVariantQScore, MinimumVariantQScore, MaximumVariantQScore, "FilteredVariantQScore");
            if (AppliedNoiseLevel != -1)
                VerifyRange(AppliedNoiseLevel, 0, null, "AppliedNoiseLevel");
            if (CallMNVs)
            {
                VerifyRange(MaxSizeMNV, 1, Constants.RegionSize, "MaxPhaseSNPLength");
                VerifyRange(MaxGapBetweenMNV, 0, null, "MaxGapPhasedSNP");
            }
            VerifyRange(MinimumMapQuality, 0, null, "MinimumMapQuality");
            VerifyRange(StrandBiasAcceptanceCriteria, 0f, null, "Strand bias cutoff");
            VerifyRange(MaxNumThreads, 1, null, "MaxNumThreads");

        }

        private void VerifyRange(int field, int minValue, int? maxValue, string fieldName)
        {
            if (field < minValue || (maxValue.HasValue && field > maxValue))
                throw new ArgumentException(string.Format("{0} must be between {1} and {2}.", fieldName,
                            minValue,
                            maxValue));

            if (field < minValue && !maxValue.HasValue)
                throw new ArgumentException(string.Format("{0} must be greater than {1}.", fieldName, minValue));
        }

        private void VerifyRange(float field, float minValue, float? maxValue, string fieldName)
        {
            if (field < minValue || (maxValue.HasValue && field > maxValue))
                throw new ArgumentException(string.Format("{0} must be between {1} and {2}.", fieldName,
                            minValue,
                            maxValue));

            if (field < minValue && !maxValue.HasValue)
                throw new ArgumentException(string.Format("{0} must be greater than {1}.", fieldName, minValue));
        }

        public ApplicationOptions UpdateOptions(string[] arguments)
        {
            string lastArgumentField = string.Empty;

            try
            {
                int argumentIndex = 0;
                while (argumentIndex < arguments.Length)
                {
                    if (arguments[argumentIndex] == null || arguments[argumentIndex].Length == 0)
                    {
                        argumentIndex++;
                        continue;
                    }
                    string value = null;
                    if (argumentIndex < arguments.Length - 1) value = arguments[argumentIndex + 1];

                    lastArgumentField = arguments[argumentIndex];

                    switch (lastArgumentField)
                    {
                        case "-a":
                            MinimumVariantQScore = int.Parse(value);
                            break;
                        case "-b":
                            MinimumBaseCallQuality = int.Parse(value);
                            break;
                        case "-B":
                            BAMPaths = value.Split(_delimiter);
                            break;
                        case "-BAMFolder":
                            BAMFolder = value;
                            break;
                        case "-c":
                            MinimumCoverage = int.Parse(value);
                            break;
                        case "-d":
                            DebugMode = bool.Parse(value);
                            break;
                        case "-debug":
                            DebugMode = bool.Parse(value);
                            break;
                        case "-f":
                            MinimumFrequency = float.Parse(value);
                            break;
                        case "-F":
                            FilteredVariantQScore = int.Parse(value);
                            break;
                        case "-fo":
                            FilterOutVariantsPresentOnlyOneStrand = bool.Parse(value);
                            break;
                        case "-g":
                            GenomePaths = value.Split(_delimiter);
                            break;
                        case "-NL":
                            AppliedNoiseLevel = int.Parse(value);
                            break;
                        case "-gVCF":
                            OutputgVCFFiles = bool.Parse(value);
                            break;
                        case "-CallMNVs":
                        case "-PhaseSNPs":
                            CallMNVs = bool.Parse(value);
                            break;
                        case "-MaxMNVLength":
                        case "-MaxPhaseSNPLength":
                            MaxSizeMNV = int.Parse(value);
                            break;
                        case "-MaxGapBetweenMNV":
                        case "-MaxGapPhasedSNP":
                            MaxGapBetweenMNV = int.Parse(value);
                            break;
                        case "-i":
                            IntervalPaths = value.Split(_delimiter);
                            break;
                        case "-m":
                            MinimumMapQuality = int.Parse(value);
                            break;
                        case "-GT":
                            if (value.ToLower().Contains("none"))
                                GTModel = GenotypeModel.None;
                            else if (value.ToLower().Contains("threshold"))
                                GTModel = GenotypeModel.Thresholding;
                            else if (value.ToLower().Contains("symmetric"))
                                GTModel = GenotypeModel.Symmetrical;
                            else
                                throw new ArgumentException(string.Format("Unknown genotype model '{0}'", value));
                            break;
                        case "-SBModel":
                            if (value.ToLower().Contains("poisson"))
                                StrandBiasModel = StrandBiasModel.Poisson;
                            else if (value.ToLower().Contains("extended"))
                                StrandBiasModel = StrandBiasModel.Extended;
                            else
                                throw new ArgumentException(string.Format("Unknown strand bias model '{0}'", value));
                            break;
                        case "-o":
                            OutputBiasFiles = bool.Parse(value);
                            break;
                        case "-p":
                            OnlyUseProperPairs = bool.Parse(value);
                            break;
                        case "-q":
                            MaximumVariantQScore = int.Parse(value);
                            break;
                        case "-s":
                            StrandBiasAcceptanceCriteria = float.Parse(value);
                            break;
                        case "-StitchPairedReads":
                            StitchReads = bool.Parse(value);
                            break;
                        case "-t":
                            MaxNumThreads = int.Parse(value);
                            break;
                        case "-ThreadByChr":
                            ThreadByChr = bool.Parse(value);
                            break;
                        case "-ReportNoCalls":
                            ReportNoCalls = bool.Parse(value);
                            break;
                        case "-requireXC":
                            RequireXCTagToStitch = bool.Parse(value);
                            break;
                        case "-xcStitcher":
                            UseXCStitcher = bool.Parse(value);
                            break;
                        case "-OutFolder":
                            OutputFolder = value;
                            break;
                        default:
                            throw new Exception(string.Format("Unknown argument '{0}'", arguments[argumentIndex]));
                    }

                    argumentIndex += 2;
                }

                CommandLineArguments = string.Join(" ", arguments);

                return this;
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(lastArgumentField))
                    throw new Exception("Unable to parse arguments: " + ex.Message);
                
                throw new Exception(string.Format("Unable to parse argument {0}: {1}", lastArgumentField, ex.Message));
            }
        }

        public void Save(string filepath)
        {
            var serializer = new XmlSerializer(typeof(ApplicationOptions));
            var outputWriter = new StreamWriter(filepath);
            serializer.Serialize(outputWriter, this);
            outputWriter.Close();
        }

        public void PopulateBAMPaths()
        {
            BAMPaths = Directory.GetFiles(BAMFolder, "*.bam");
            if (!BAMPaths.Any())
                throw new ArgumentException(string.Format("No BAM files found in {0}", BAMFolder));
        }

        //When should we output the extra info to the VCF?  By default, we should not bother.
        //but if we are testing something fancy, or filtering based on SB, it is probably useful. 
        public bool OutputNoiseLevelAndStrandBias()
        {
            return DebugMode || OutputBiasFiles || (StrandBiasAcceptanceCriteria < 1);
        }

        public static ApplicationOptions Load()
        {
            if (File.Exists("SomaticVariantCallerOptions.xml"))
            {
                Console.WriteLine("Loading SomaticVariantCallerOptions.xml");
                return LoadFromFile("SomaticVariantCallerOptions.xml");
            }

            return null;
        }

        public static ApplicationOptions LoadFromFile(string filePath)
        {
            try
            {
                using (var streamReader = new StreamReader(filePath))
                {
                    var xmlSerializer = new XmlSerializer(typeof (ApplicationOptions));

                    var options = (ApplicationOptions) xmlSerializer.Deserialize(streamReader);

                    return options;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception:  " + ex);
                Console.WriteLine("There was a problem loading your config file " + filePath);
                Console.WriteLine("Please check the file exists and is correctly formatted.");
                return null;
            }
        }
    }
}