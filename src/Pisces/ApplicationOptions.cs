using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Pisces.Logic.Alignment;
using Pisces.Types;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Pisces.Processing;
using Pisces.Calculators;
using System.Reflection;

namespace Pisces
{
    /// <summary>
    ///     Options to the somatic variant caller: Mostly thresholds for various filters.
    ///     The filter cutoffs will NOT be exposed to the customer, but we'll be exploring
    ///     various combinations internally.
    /// </summary>
    // ReSharper disable InconsistentNaming - prevents ReSharper from renaming serializeable members that are sensitive to being changed
    public class ApplicationOptions : BaseApplicationOptions
    {
        public const string DefaultLogFolderName = "PiscesLogs";
        public const string LogFileNameBase = "PiscesLog.txt";

        public string LogFileName
        {
            get
            {
                if (InsideSubProcess)
                    return System.Diagnostics.Process.GetCurrentProcess().Id + "_" + LogFileNameBase;
                return LogFileNameBase;
            }
        }

        #region Serializeable Types and Members

        public string[] CommandLineArguments;

        public int AppliedNoiseLevel = -1;
        public int MinimumBaseCallQuality = 20;
        public int MaximumVariantQScore = 100;
        public int MinimumGenotypeQScore = 0;
        public int MaximumGenotypeQScore = 100;
        public int FilteredVariantQScore = 30;
        public int MinimumVariantQScore = 20; // We don't bother reporting variants with a lower Q-score.
        public int MinimumDepth = 10;
        public int MinimumMapQuality = 1;
        public float MinimumFrequency = 0.01f;
        public float StrandBiasAcceptanceCriteria = 0.5f; // Flag variants as filtered if they have greater strand bias than this
        public double StrandBiasScoreMinimumToWriteToVCF = -100; // just so we dont have to write negative infinities into vcfs and then they get tagged as "poorly formed"  
        public double StrandBiasScoreMaximumToWriteToVCF = 0;
        public bool AllowMultipleVcfLinesPerLoci = true;
        public bool FilterOutVariantsPresentOnlyOneStrand = false;
        public string[] IntervalPaths;
        public bool OutputBiasFiles = false;
        public bool OutputgVCFFiles = false;
        public bool OnlyUseProperPairs = false;
        public int NoiseModelHalfWindow = 1;
        public bool DebugMode = false;
        public bool CallMNVs = false;
        public bool ThreadByChr = false;
        public bool ReportNoCalls = false;
        public bool ReportRcCounts = false;
        public int MaxSizeMNV = 3;
        public int MaxGapBetweenMNV = 1;
        public bool UseMNVReallocation = true;
        public NoiseModel NoiseModel = NoiseModel.Flat;
        public StrandBiasModel StrandBiasModel = StrandBiasModel.Extended;
        public PloidyModel PloidyModel = PloidyModel.Somatic;
        public CoverageMethod CoverageMethod = CoverageMethod.Approximate;
        public DiploidThresholdingParameters DiploidThresholdingParameters = new DiploidThresholdingParameters();
        public string MonoPath; //only needed if running on Linux cluster, and we plan to spawn processes
        public bool Collapse = true;
        public string PriorsPath;
        public bool TrimMnvPriors;
        public float FilteredVariantFrequency;
        public int? LowGenotypeQualityFilter;
        public int? IndelRepeatFilter;
        public int? LowDepthFilter;
        public int? RMxNFilterMaxLengthRepeat = 5;
        public int? RMxNFilterMinRepetitions = 9;
        public float RMxNFilterFrequencyLimit = 0.20f; //this was recommended by Kristina K after empirical testing 
        public float CollapseFreqThreshold = 0f;
        public float CollapseFreqRatioThreshold = 0.5f;
        public bool SkipNonIntervalAlignments = false;  //keep this off. it currently has bugs, speed issues, and no plan to fix it)


        public string LogFolder {
            get
            {
                if (BAMPaths == null || BAMPaths.Length == 0)
                    throw new Exception("Unable to start logging: cannot determine log folder");

                return OutputFolder ?? Path.Combine(Path.GetDirectoryName(BAMPaths[0]), DefaultLogFolderName);
            }
        }
      
        #endregion
        // ReSharper restore InconsistentNaming

        public static void PrintVersionToConsole()
        {
            var currentAssembly = Assembly.GetExecutingAssembly().GetName();
            Console.WriteLine(currentAssembly.Name + " " + currentAssembly.Version);
            Console.WriteLine(UsageInfoHelper.GetWebsite());
            Console.WriteLine();
        }

    public new static void PrintUsageInfo()
        {
            PrintVersionToConsole();

            Console.WriteLine("Options:");
			Console.WriteLine(" -ver/-v : Print version.");
			Console.WriteLine(" -MinVariantQScore/-MinVQ : MinimumVariantQScore to report variant");
            Console.WriteLine(" -MinBaseCallQuality/-MinBQ : MinimumBaseCallQuality to use a base of the read");
            Console.WriteLine(" -BamPaths/-Bam : BAMPath(s), single value or comma delimited list");
            Console.WriteLine(" -MinDepth/-MinDP : Minimum depth to call a variant");
            Console.WriteLine(" -MinimumFrequency/-MinVF : MinimumFrequency to call a variant");
            Console.WriteLine(" -EnableSingleStrandFilter/-SSFilter : Flag variants as filtered if coverage limited to one strand");
            Console.WriteLine(" -VariantQualityFilter/-VQFilter : FilteredVariantQScore to report variant as filtered");
            Console.WriteLine(" -MinVariantFrequencyFilter/-VFFilter : FilteredVariantFrequency to report variant as filtered");
            Console.WriteLine(" -RepeatFilter : FilteredIndelRepeats to report variant as filtered");
            Console.WriteLine(" -MinDepthFilter/-MinDPFilter : FilteredLowDepth to report variant as filtered");
            Console.WriteLine(" -IntervalPaths/-I : IntervalPath(s), single value or comma delimited list corresponding to BAMPath(s). At most one value should be provided if BAM folder is specified");
            Console.WriteLine(" -MinMapQuality/-MinMQ : MinimumMapQuality required to use a read");
            Console.WriteLine(" -GenomePaths/-G : GenomePath(s), single value or comma delimited list corresponding to BAMPath(s). Must be single value if BAM folder is specified");
            Console.WriteLine(" -OutputSBFiles : Output strand bias files, 'true' or 'false'");
            Console.WriteLine(" -OnlyUseProperPairs/-PP : Only use proper pairs, 'true' or 'false'");
            Console.WriteLine(" -MaxVariantQScore/-MaxVQ : MaximumVariantQScore to cap output variant Qscores");
            Console.WriteLine(" -MaxAcceptableStrandBiasFilter/-SBFilter : Strand bias cutoff");
            Console.WriteLine(" -MaxNumThreads/-t : ThreadCount");
            Console.WriteLine(" -ThreadByChr : Thread by chr. More memory intensive.  This will temporarily create output per chr.");
            Console.WriteLine(" -gVCF : Output gVCF files, 'true' or 'false'");
            Console.WriteLine(" -CallMNVs : Call MNVs (a.k.a. phased SNPs) 'true' or 'false'");
            Console.WriteLine(" -MaxMNVLength : Max length phased SNPs that can be called");
            Console.WriteLine(" -MaxRefGapInMNV or -MaxGapBetweenMNV : Max allowed gap between phased SNPs that can be called");
            Console.WriteLine(" -ReportNoCalls : 'true' or 'false'. default, false");
            Console.WriteLine(" -Collapse : Whether or not to collapse variants together, 'true' or 'false'. default, false ");
            Console.WriteLine(" -PriorsPath : PriorsPath for vcf file containing known variants, used with -collapse to preferentially reconcile variants");
            Console.WriteLine(" -TrimMnvPriors : Whether or not to trim preceeding base from MNVs in priors file.  Note: COSMIC convention is to include preceeding base for MNV.  Default is false.");
            Console.WriteLine(" -ReportRcCounts : When BAM files contain X1 and X2 tags, output read counts for duplex-stitched, duplex-nonstitched, simplex-stitched, and simplex-nonstitched.  'true' or 'false'. default, false");
            Console.WriteLine(" -Ploidy : 'somatic' or 'diploid'. default, somatic.");
            Console.WriteLine(" -DiploidGenotypeParameters : A,B,C. default 0.20,0.70,0.80");
            Console.WriteLine(" -RMxNFilter : M,N,F. Comma-separated list of integers indicating max length of the repeat section (M), the minimum number of repetitions of that repeat (N), to be applied if the variant frequency is less than (F). Default is R5x9,F=20.");
            Console.WriteLine(" -CoverageMethod : 'approximate' or 'exact'. Exact is more precise but requires more memory (minimum 8 GB).  Default approximate");
            Console.WriteLine(" -CollapseFreqThreshold : When collapsing, minimum frequency required for target variants. Default '0'");
            Console.WriteLine(" -CollapseFreqRatioThreshold : When collapsing, minimum ratio required of target variant frequency to collapsible variant frequency. Default '0.5f'");
			Console.WriteLine(" -NoiseModel : Window/Flat. Default Flat ");
			BaseApplicationOptions.PrintUsageInfo();
            Console.WriteLine("");
            Console.WriteLine("Note, all options are case insensitive.");

        }

        public static ApplicationOptions ParseCommandLine(string[] arguments)
        {
            var options = new ApplicationOptions();
	        if (options.UpdateOptions(arguments) == null) return null;
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
            }

            if (bamFolderSpecified)
            {
                if (!Directory.Exists(BAMFolder))
                    throw new ArgumentException(string.Format("BAMFolder {0} not found", BAMFolder));

                if (GenomePaths.Length > 1)
                    throw new ArgumentException("Only 1 Genome Path should be specified when BAMFolder is specified.");

                if (IntervalPaths != null && IntervalPaths.Length > 1)
                    throw new ArgumentException("At most 1 Interval Path should be specified when BAMFolder is specified.");
            }

            foreach (var genomePath in GenomePaths)
                if (!Directory.Exists(genomePath))
                    throw new ArgumentException(string.Format("GenomePath '{0}' does not exist.", genomePath));

            if (IntervalPaths != null)
                foreach (var intervalPath in IntervalPaths)
                    if (!File.Exists(intervalPath))
                        throw new ArgumentException(string.Format("IntervalPath '{0}' does not exist.", intervalPath));

            ValidationHelper.VerifyRange(MinimumVariantQScore, 0, int.MaxValue, "MinimumVariantQscore");
            ValidationHelper.VerifyRange(MaximumVariantQScore, 0, int.MaxValue, "MaximumVariantQScore");
            if (MaximumVariantQScore < MinimumVariantQScore)
                throw new ArgumentException("MinimumVariantQScore must be less than or equal to MaximumVariantQScore.");
           
            ValidationHelper.VerifyRange(MinimumBaseCallQuality, 0, int.MaxValue, "MinimumBaseCallQuality");
            ValidationHelper.VerifyRange(MinimumFrequency, 0f, 1f, "MinimumFrequency");
            ValidationHelper.VerifyRange(FilteredVariantQScore, MinimumVariantQScore, MaximumVariantQScore, "FilteredVariantQScore");

            ValidationHelper.VerifyRange((float)FilteredVariantFrequency, 0, 1f, "FilteredVariantFrequency");

            if (LowGenotypeQualityFilter != null)
                ValidationHelper.VerifyRange((float)LowGenotypeQualityFilter, 0, int.MaxValue, "FilteredLowGenomeQuality");
            if (IndelRepeatFilter != null)
                ValidationHelper.VerifyRange((int)IndelRepeatFilter, 0, 10, "FilteredIndelRepeats");
            if (LowDepthFilter != null)
                ValidationHelper.VerifyRange((int)LowDepthFilter, MinimumDepth, int.MaxValue, "FilteredLowDepth");

            if ((LowDepthFilter == null) || (LowDepthFilter < MinimumDepth))
            {
                LowDepthFilter = MinimumDepth;
            }

            if (AppliedNoiseLevel != -1)
                ValidationHelper.VerifyRange(AppliedNoiseLevel, 0, int.MaxValue, "AppliedNoiseLevel");
            if (CallMNVs)
            {
                ValidationHelper.VerifyRange(MaxSizeMNV, 1, GlobalConstants.RegionSize, "MaxPhaseSNPLength");
                ValidationHelper.VerifyRange(MaxGapBetweenMNV, 0, int.MaxValue, "MaxGapPhasedSNP");
            }
            ValidationHelper.VerifyRange(MinimumMapQuality, 0, int.MaxValue, "MinimumMapQuality");
            ValidationHelper.VerifyRange(StrandBiasAcceptanceCriteria, 0f, int.MaxValue, "Strand bias cutoff");
            ValidationHelper.VerifyRange(MaxNumThreads, 1, int.MaxValue, "MaxNumThreads");
            ValidationHelper.VerifyRange(CollapseFreqThreshold, 0f, float.MaxValue, "CollapseFreqThreshold");
            ValidationHelper.VerifyRange(CollapseFreqRatioThreshold, 0f, float.MaxValue, "CollapseFreqRatioThreshold");

            if (!string.IsNullOrEmpty(PriorsPath))
            {
                if (!File.Exists(PriorsPath))
                    throw new ArgumentException(string.Format("PriorsPath '{0}' does not exist.", PriorsPath));
            }

            if (PloidyModel == PloidyModel.Diploid)
            {
                MinimumFrequency = DiploidThresholdingParameters.MinorVF;
            }


            if (FilteredVariantFrequency < MinimumFrequency)
            {
                FilteredVariantFrequency = MinimumFrequency;
            }

            if (RMxNFilterMaxLengthRepeat != null || RMxNFilterMinRepetitions != null)
            {
                if (RMxNFilterMaxLengthRepeat == null || RMxNFilterMinRepetitions == null)
                {
                    throw new ArgumentException(string.Format("If specifying RMxN filter thresholds, you must supply both RMxNFilterMaxLengthRepeat and RMxNFilterMinRepetitions."));
                }
                ValidationHelper.VerifyRange((int)RMxNFilterMaxLengthRepeat, 0, 100, "RMxNFilterMaxLengthRepeat");
                ValidationHelper.VerifyRange((int)RMxNFilterMinRepetitions, 0, 100, "RMxNFilterMinRepetitions");
            }

            if (ThreadByChr && !InsideSubProcess && !string.IsNullOrEmpty(ChromosomeFilter))
                throw new ArgumentException("Cannot thread by chromosome when filtering on a particular chromosome.");

            if (!string.IsNullOrEmpty(OutputFolder) && bamPathsSpecified && (BAMPaths.Length>1))
            {
                //make sure none of the input BAMS have the same name. Or else we will have an output collision.
                for(int i=0;i<BAMPaths.Length;i++)
                {
                    for (int j = i + 1; j < BAMPaths.Length; j++)
                    {
                        if (i == j)
                            continue;

                        var fileA = Path.GetFileName(BAMPaths[i]);
                        var fileB = Path.GetFileName(BAMPaths[j]);

                        if (fileA == fileB)
                        {
                            throw new ArgumentException(string.Format( "VCF file name collision. Cannot process two different bams with the same name {0} into the same output folder {1}.", fileA, OutputFolder));
                        }
                    }
                }
            }
        }

        private float[] ParseStringToFloat(string[] stringArray)
        {
            var parameters = new float[stringArray.Length];

            for(int i=0; i<parameters.Length;i++)
            {
                try
                {
                    parameters[i] = float.Parse(stringArray[i]);
                }
                catch
                {
                    throw new ArgumentException(string.Format("Unable to parse float type from " + stringArray[i]
                        + ".  Please check parameters."));
                }
            }

            return parameters;
        }

        public ApplicationOptions UpdateOptions(string[] arguments)
        {
            string lastArgumentField = string.Empty;

            try
            {
                int argumentIndex = 0;
                while (argumentIndex < arguments.Length)
                {
                    if (string.IsNullOrEmpty(arguments[argumentIndex]))
                    {
                        argumentIndex++;
                        continue;
                    }
                    string value = null;
                    if (argumentIndex < arguments.Length - 1) value = arguments[argumentIndex + 1].Trim();

                    lastArgumentField = arguments[argumentIndex].ToLower();

                    switch (lastArgumentField)
                    {
						case "-v":
						case "-ver":
							PrintVersionToConsole();
							return null;
                        //case "-a": depracated
                        case "-minvq":
                        case "-minvariantqscore":
                            MinimumVariantQScore = int.Parse(value);
                            break;
                        //case "-b": depracated
                        case "-minbq": 
                        case "-minbasecallquality":
                            MinimumBaseCallQuality = int.Parse(value);
                            break;
                        case "-b":
                        case "-bam":
                            BAMPaths = value.Split(_delimiter);
                            break;
                        case "-c":
                        case "-mindp":
                        case "-mindepth":
                        case "-mincoverage": //last release this is available. trying to be nice for backwards compatibility with Isas.
                            MinimumDepth = int.Parse(value);
                            break;
                        case "-d":
                        case "-debug":
                            DebugMode = bool.Parse(value);
                            break;
                        case "-minvf":  //used to be "f"
                        case "-minimumvariantfrequency":
                        case "-minimumfrequency":
                            MinimumFrequency = float.Parse(value);
                            break;
                        case "-vqfilter": //used to be "F"
                        case "-variantqualityfilter":
                            FilteredVariantQScore = int.Parse(value);
                            break;
                        case "-vffilter": //used to be "v"
                        case "-minvariantfrequencyfilter":
                            FilteredVariantFrequency = float.Parse(value);
                            break;
                        case "-gqfilter":
                        case "-genotypequalityfilter":
                            LowGenotypeQualityFilter = int.Parse(value);
                            break;
                        case "-repeatfilter":
                            IndelRepeatFilter = int.Parse(value);
                            break;
                        case "-mindpfilter":
                        case "-mindepthfilter":
                            LowDepthFilter = int.Parse(value);
                            break;
                        case "-ssfilter": //used to be "fo"
                        case "-enablesinglestrandfilter":
                            FilterOutVariantsPresentOnlyOneStrand = bool.Parse(value);
                            break;
                        case "-g":
                        case "-genomepaths":
                            GenomePaths = value.Split(_delimiter);
                            break;
                        case "-nl":
                        case "-noiselevelforqmodel":
                            AppliedNoiseLevel = int.Parse(value);
                            break;
                        case "-gvcf":
                            OutputgVCFFiles = bool.Parse(value);
                            break;
                        case "-callmnvs":
                        //case "-phasesnps": obsolete
                            CallMNVs = bool.Parse(value);
                            break;
                        case "-maxmnvlength":
                            //case "-MaxPhaseSNPLength": obsolete
                            //case "-MaxPhasedSNPLength": obsolete
                            MaxSizeMNV = int.Parse(value);
                            break;
                        case "-maxgapbetweenmnv":
                        case "-maxrefgapinmnv":
                            //case "-MaxGapPhasedSNP":: obsolete
                            MaxGapBetweenMNV = int.Parse(value);
                            break;
                        case "-i":
                        case "-intervalpaths":
                            IntervalPaths = value.Split(_delimiter);
                            break;
                        case "-minmq": //used to be "m"
                        case "-minmapquality":
                            MinimumMapQuality = int.Parse(value);
                            break;
                        case "-ploidy":
                            if (value.ToLower().Contains("somatic"))
                                PloidyModel = PloidyModel.Somatic;
                            else if (value.ToLower().Contains("diploid"))
                                PloidyModel = PloidyModel.Diploid;
                            else
                                throw new ArgumentException(string.Format("Unknown ploidy model '{0}'", value));
                            break;
                        case "-diploidgenotypeparameters":
                            var parameters = ParseStringToFloat(value.Split(_delimiter));
                            if (parameters.Length != 3)
                                throw new ArgumentException(string.Format("DiploidGenotypeParamteers argument requires exactly three values."));
                            DiploidThresholdingParameters = new DiploidThresholdingParameters(parameters);
                            break;                      
                        case "-crushvcf":
                            bool crushedallelestyle = bool.Parse(value);
                            AllowMultipleVcfLinesPerLoci = !(crushedallelestyle);
                            break;
                        case "-sbmodel":
                            if (value.ToLower().Contains("poisson"))
                                StrandBiasModel = StrandBiasModel.Poisson;
                            else if (value.ToLower().Contains("extended"))
                                StrandBiasModel = StrandBiasModel.Extended;
                            else
                                throw new ArgumentException(string.Format("Unknown strand bias model '{0}'", value));
                            break;
                        case "-outputsbfiles":
                            OutputBiasFiles = bool.Parse(value);
                            break;
                        case "-pp":
                        case "-onlyuseproperpairs":
                            OnlyUseProperPairs = bool.Parse(value);
                            break;
                        case "-maxvq":
                        case "-maxvariantqscore":
                            MaximumVariantQScore = int.Parse(value);
                            break;
                        case "-maxgq":
                        case "-maxgenotypeqscore":
                            MaximumGenotypeQScore = int.Parse(value);
                            break;
                        case "-mingq":
                        case "-minqenotypeqscore":
                            MinimumGenotypeQScore = int.Parse(value);
                            break;
                        case "-sbfilter":
                        case "-maxacceptablestrandbiasfilter":
                            StrandBiasAcceptanceCriteria = float.Parse(value);
                            break;
                        case "-stitchpairedreads":
                            throw new ArgumentException("StitchPairedReads option is obsolete.");
                        case "-t":
                            MaxNumThreads = int.Parse(value);
                            break;
                        case "-threadbychr":
                            ThreadByChr = bool.Parse(value);
                            break;
                        case "-reportnocalls":
                            ReportNoCalls = bool.Parse(value);
                            break;
                        case "-xcstitcher":
                            throw new ArgumentException("XCStitcher option is obsolete.");
                        case "-collapse":
                            Collapse = bool.Parse(value);
                            break;
                        case "-collapsefreqthreshold":
                            CollapseFreqThreshold = float.Parse(value);
                            break;
                        case "-collapsefreqratiothreshold":
                            CollapseFreqRatioThreshold = float.Parse(value);
                            break;
                        case "-priorspath":
                            PriorsPath = value;
                            break;
                        case "-trimmnvpriors":
                            TrimMnvPriors = bool.Parse(value);
                            break;
                        case "-nifydisagreements":
                            throw new ArgumentException("NifyDisagreements option is no longer valid: stitching within Pisces is obsolete.");
                        case "-coverageMethod":
                            if (value.ToLower() == "approximate")
                                CoverageMethod = CoverageMethod.Approximate;
                            else if (value.ToLower() == "exact")
                                CoverageMethod = CoverageMethod.Exact;
                            else
                                throw new ArgumentException(string.Format("Unknown coverage method '{0}'", value));
                            break;
                        case "-reportrccounts":
                            ReportRcCounts = bool.Parse(value);
                        break;
                        case "-mono":
                            MonoPath = value;
                            break;
                        case "-rmxnfilter":
                            bool turnOn = true;
                            bool worked = (bool.TryParse(value, out turnOn));
                            if (worked)
                            {
                                if (turnOn)
                                {
                                    // stick with defaults
                                }
                                else
                                {
                                    //turn off
                                    RMxNFilterMaxLengthRepeat = null;
                                    RMxNFilterMinRepetitions = null;
                                }
                                break;
                            }
                            //else, it wasnt a bool...
                            var rmxnThresholds = ParseStringToFloat(value.Split(_delimiter));
                            if ((rmxnThresholds.Length <2) || (rmxnThresholds.Length > 3))
                                throw new ArgumentException(string.Format("RMxNFilter argument requires two or three values."));
                            RMxNFilterMaxLengthRepeat = (int)rmxnThresholds[0];
                            RMxNFilterMinRepetitions = (int)rmxnThresholds[1];

                            if (rmxnThresholds.Length > 2)
                                RMxNFilterFrequencyLimit = (float)rmxnThresholds[2];
                            break;
						case "-noisemodel":
							NoiseModel = value.ToLower() == "window" ? NoiseModel.Window : NoiseModel.Flat;
							break;
						case "-skipnonintervalalignments":
                            throw new Exception(string.Format("'SkipNonIntervalAlignments' option has been depracated until further notice. ", arguments[argumentIndex]));
                            //(it has bugs, speed issues, and no plan to fix it)
                        default:
                            if (!base.UpdateOptions(lastArgumentField, value))
                                throw new Exception(string.Format("Unknown argument '{0}'", arguments[argumentIndex]));
                            break;
                    }
                    argumentIndex += 2;
                }

                CommandLineArguments = arguments;

                return this;
            }
            catch (Exception ex)
            {
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
    }
}