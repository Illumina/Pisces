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
        private const char _delimiter = ',';
        public const string DefaultLogFolderName = "PiscesLogs";
        public const string LogFileName = "PiscesLog.txt";

        #region Serializeable Types and Members

        public string CommandLineArguments;

        public int AppliedNoiseLevel = -1;
        public int MinimumBaseCallQuality = 20;
        public int MaximumVariantQScore = 100;
        public int MinimumGenotypeQScore = 0;
        public int MaximumGenotypeQScore = 100;
        public int FilteredVariantQScore = 30;
        public int MinimumVariantQScore = 20; // We don't bother reporting variants with a lower Q-score.
        public int MinimumCoverage = 10;
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
        public int MaxNumThreads = 20;
        public int NoiseModelHalfWindow = 1;
        public bool DebugMode = false;
        public bool CallMNVs = false;
        public bool ThreadByChr = false;
        public bool ReportNoCalls = false;
        public bool ReportRcCounts = false;
        public int MaxSizeMNV = 3;
        public int MaxGapBetweenMNV = 1;
        public bool UseMNVReallocation = true;
        public bool StitchReads = false;
        public NoiseModel NoiseModel = NoiseModel.Flat;
        public GenotypeModel GTModel = GenotypeModel.Thresholding; //possibly move to symmetrical when its implemented
        public StrandBiasModel StrandBiasModel = StrandBiasModel.Extended;
        public PloidyModel PloidyModel = PloidyModel.Somatic;
        public CoverageMethod CoverageMethod = CoverageMethod.Approximate;
        public GenotypeCalculator.DiploidThresholdingParameters DiploidThresholdingParameters = new GenotypeCalculator.DiploidThresholdingParameters();
        public string MonoPath; //only needed if running on Linux cluster, and we plan to spawn processes
        public bool UseXCStitcher = false;
        public bool NifyDisagreements = false;
        public bool Collapse = false;
        public string PriorsPath;
        public bool TrimMnvPriors;
        public float? FilteredVariantFrequency;
        public int? LowGenotypeQualityFilter;
        public int? IndelRepeatFilter;
        public int? LowDepthFilter;
        public int? RMxNFilterMaxLengthRepeat = 5;
        public int? RMxNFilterMinRepetitions = 9;
        public bool InsideSubProcess;
        public bool MultiProcess = true;
        public float CollapseFreqThreshold = 0f;
        public float CollapseFreqRatioThreshold = 0.5f;
        public bool SkipNonIntervalAlignments = false;

        public string LogFolder {
            get
            {
                if (BAMPaths == null || BAMPaths.Length == 0)
                    throw new Exception("Unable to start logging: cannot determine log folder");

                return OutputFolder ?? Path.Combine(Path.GetDirectoryName(BAMPaths[0]), DefaultLogFolderName);
            }
        }

        public UnstitchableStrategy UnstitchableStrategy = UnstitchableStrategy.TakeStrongerRead;

        public int MaxFragmentSize = 1000;

        #endregion
        // ReSharper restore InconsistentNaming

        public static void PrintUsageInfo()
        {
            Console.WriteLine("Options:");
            Console.WriteLine(" -MinVariantQScore/-a : MinimumVariantQScore to report variant");
            Console.WriteLine(" -MinBaseCallQuality/-b : MinimumBaseCallQuality to use a base of the read");
            Console.WriteLine(" -BamPaths/-B : BAMPath(s), single value or comma delimited list");
            Console.WriteLine(" -BAMFolder : BAM parent folder");
            Console.WriteLine(" -MinCoverage/-c : MinimumCoverage to call a variant");
            Console.WriteLine(" -MinimumFrequency/-f : MinimumFrequency to call a variant");
            Console.WriteLine(" -EnableSingleStrandFilter/-fo : Flag variants as filtered if coverage limited to one strand");
            Console.WriteLine(" -VariantQualityFilter/-F : FilteredVariantQScore to report variant as filtered");
            Console.WriteLine(" -MinVariantFrequencyFilter/-v FilteredVariantFrequency to report variant as filtered");
            Console.WriteLine(" -GTModel/-gtq LowGenotypeQualityFilter value, to report variant as filtered");
            Console.WriteLine(" -RepeatFilter FilteredIndelRepeats to report variant as filtered");
            Console.WriteLine(" -MinDepthFilter/-ld FilteredLowDepth to report variant as filtered");
            Console.WriteLine(" -IntervalPaths/-i : IntervalPath(s), single value or comma delimited list corresponding to BAMPath(s). At most one value should be provided if BAM folder is specified");
            Console.WriteLine(" -MinMapQuality/-m : MinimumMapQuality required to use a read");
            Console.WriteLine(" -GenomePaths/-g : GenomePath(s), single value or comma delimited list corresponding to BAMPath(s). Must be single value if BAM folder is specified");
            Console.WriteLine(" -OutputSBFiles/-o : Output strand bias files, 'true' or 'false'");
            Console.WriteLine(" -OnlyUseProperPairs/-p : Only use proper pairs, 'true' or 'false'");
            Console.WriteLine(" -MaxVariantQScore/-q : MaximumVariantQScore to cap output variant Qscores");
            Console.WriteLine(" -MaxAcceptableStrandBiasFilter/-s : Strand bias cutoff");
            Console.WriteLine(" -StitchPairedReads : Stitch overlapping region of paired reads, 'true' or 'false'. default, false");
            Console.WriteLine(" -MaxNumThreads/-t : ThreadCount");
            Console.WriteLine(" -ThreadByChr : Thread by chr. More memory intensive.  This will temporarily create output per chr.");
            Console.WriteLine(" -gVCF : Output gVCF files, 'true' or 'false'");
            Console.WriteLine(" -CallMNVs or -PhaseSNPs : Call phased SNPs, 'true' or 'false'");
            Console.WriteLine(" -MaxMNVLength or -MaxPhaseSNPLength : Max length phased SNPs that can be called");
            Console.WriteLine(" -MaxGapBetweenMNV or -MaxGapPhasedSNP : Max allowed gap between phased SNPs that can be called");
            Console.WriteLine(" -OutFolder : 'myoutpath'");
            Console.WriteLine(" -ReportNoCalls : 'true' or 'false'. default, false");
            Console.WriteLine(" -RequireXC : RequireXCTagToStitch 'true' or 'false'. default, false");
            Console.WriteLine(" -XcStitcher : UseXCStitcher 'true' or 'false'. default, false");
            Console.WriteLine(" -Collapse : Whether or not to collapse variants together, 'true' or 'false'. default, false ");
            Console.WriteLine(" -PriorsPath : PriorsPath for vcf file containing known variants, used with -collapse to preferentially reconcile variants");
            Console.WriteLine(" -TrimMnvPriors : Whether or not to trim preceeding base from MNVs in priors file.  Note: COSMIC convention is to include preceeding base for MNV.  Default is false.");
            Console.WriteLine(" -NifyDisagreements : When stitching is enabled, change any disagreeing bases in the overlap region to 'N', 'true' or 'false'. default, false");
            Console.WriteLine(" -MaxFragmentSize : When stitching is enabled, this is the max fragment size allowed when pairing.  Reads without a mate found within this window are skipped.  Default is 1000");
            Console.WriteLine(" -ChrFilter : Debug option to variant call just the specified chromosome.");
            Console.WriteLine(" -UnstitchableStrategy : When stitching is enabled, the strategy for handling unstitchable reads, 'both' (process both reads separately), 'stronger' (take the read with higher quality), or 'none' (throw out the unstitchable pair). Default 'both'.");
            Console.WriteLine(" -ReportRcCounts : When BAM files contain X1 and X2 tags, output read counts for duplex-stitched, duplex-nonstitched, simplex-stitched, and simplex-nonstitched.  'true' or 'false'. default, false");
            Console.WriteLine(" -Ploidy : 'somatic' or 'diploid'. default, somatic.");
            Console.WriteLine(" -DiploidGenotypeParameters : A,B,C. default 0.20,0.70,0.80");
            Console.WriteLine(" -RMxNFilter : M,N. Comma-separated pair of integers indicating max length of the repeat section we will look for (M) and minimum number of repetitions of that repeat (N). Default is R5x9.");
            Console.WriteLine(" -CoverageMethod : 'approximate' or 'exact'. Exact is more precise but requires more memory (minimum 8 GB).  Default approximate");
            Console.WriteLine(" -MultiProcess : When threading by chr, launch separate processes to parallelize. Default true");
            Console.WriteLine(" -CollapseFreqThreshold : When collapsing, minimum frequency required for target variants. Default '0'");
            Console.WriteLine(" -CollapseFreqRatioThreshold : When collapsing, minimum ratio required of target variant frequency to collapsible variant frequency. Default '0.5f'");
            Console.WriteLine(" -SkipNonIntervalAlignments : When using intervals, extract from the bam file only those alignments that could overlap with the intervals. Default false");
            Console.WriteLine("");
            Console.WriteLine("Note, where -option1/-option2 is specified, -option2 is to be depracated.");

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

            if (FilteredVariantFrequency != null)
                ValidationHelper.VerifyRange((float)FilteredVariantFrequency, 0, 1f, "FilteredVariantFrequency");
            if (LowGenotypeQualityFilter != null)
                ValidationHelper.VerifyRange((float)LowGenotypeQualityFilter, 0, int.MaxValue, "FilteredLowGenomeQuality");
            if (IndelRepeatFilter != null)
                ValidationHelper.VerifyRange((int)IndelRepeatFilter, 0, 10, "FilteredIndelRepeats");
            if (LowDepthFilter != null)
                ValidationHelper.VerifyRange((int)LowDepthFilter, MinimumCoverage, int.MaxValue, "FilteredLowDepth");

            if ((LowDepthFilter == null) || (LowDepthFilter < MinimumCoverage))
            {
                LowDepthFilter = MinimumCoverage;
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

            if ((LowGenotypeQualityFilter != null) && (PloidyModel == PloidyModel.Somatic))
            {
                throw new ArgumentException(string.Format("Genotype Quality Filter is never supported in conjunction with somatic calling."));       
            }

            if (PloidyModel == PloidyModel.Diploid)
            {
                MinimumFrequency = DiploidThresholdingParameters.MinorVF;
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

                    lastArgumentField = arguments[argumentIndex];

                    switch (lastArgumentField)
                    {
                        case "-a":
                        case "-MinVariantQScore":
                            MinimumVariantQScore = int.Parse(value);
                            break;
                        case "-b":
                        case "-MinBaseCallQuality":
                            MinimumBaseCallQuality = int.Parse(value);
                            break;
                        case "-B":
                        case "-BamPaths":
                            BAMPaths = value.Split(_delimiter);
                            break;
                        case "-BAMFolder":
                            BAMFolder = value;
                            break;
                        case "-c":
                        case "-MinCoverage":
                            MinimumCoverage = int.Parse(value);
                            break;
                        case "-d":
                            DebugMode = bool.Parse(value);
                            break;
                        case "-debug":
                            DebugMode = bool.Parse(value);
                            break;
                        case "-f":
                        case "-MinimumFrequency":
                        case "-minimumfrequency":
                            MinimumFrequency = float.Parse(value);
                            break;
                        case "-F":
                        case "-variantqualityfilter":
                        case "-VariantQualityFilter":
                            FilteredVariantQScore = int.Parse(value);
                            break;
                        case "-v":
                        case "-MinVariantFrequencyFilter":
                            FilteredVariantFrequency = float.Parse(value);
                            break;
                        case "-gtq":
                        case "-genotypequalityfilter":
                        case "-GenotypeQualityFilter":
                            LowGenotypeQualityFilter = int.Parse(value);
                            break;
                        case "-repeatfilter":
                        case "-RepeatFilter":
                            IndelRepeatFilter = int.Parse(value);
                            break;
                        case "-ld":
                        case "-MinDepthFilter ":
                            LowDepthFilter = int.Parse(value);
                            break;
                        case "-fo":
                        case "-EnableSingleStrandFilter":
                            FilterOutVariantsPresentOnlyOneStrand = bool.Parse(value);
                            break;
                        case "-g":
                        case "-GenomePaths":
                            GenomePaths = value.Split(_delimiter);
                            break;
                        case "-NL":
                        case "-NoiseLevelForQModel":
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
                        case "-MaxPhasedSNPLength":
                            MaxSizeMNV = int.Parse(value);
                            break;
                        case "-MaxGapBetweenMNV":
                        case "-MaxGapPhasedSNP":
                        case "-MaxRefGapInMnv":
                            MaxGapBetweenMNV = int.Parse(value);
                            break;
                        case "-i":
                        case "-IntervalPaths":
                            IntervalPaths = value.Split(_delimiter);
                            break;
                        case "-m":
                        case "-MinMapQuality":
                            MinimumMapQuality = int.Parse(value);
                            break;
                        case "-ploidy":
                        case "-Ploidy":
                            if (value.ToLower().Contains("somatic"))
                                PloidyModel = PloidyModel.Somatic;
                            else if (value.ToLower().Contains("diploid"))
                                PloidyModel = PloidyModel.Diploid;
                            else
                                throw new ArgumentException(string.Format("Unknown ploidy model '{0}'", value));
                            break;
                        case "-DiploidGenotypeParameters":
                            var parameters = ParseStringToFloat(value.Split(_delimiter));
                            if (parameters.Length != 3)
                                throw new ArgumentException(string.Format("DiploidGenotypeParamteers argument requires exactly three values."));
                            DiploidThresholdingParameters = new GenotypeCalculator.DiploidThresholdingParameters(parameters);
                            break;
                        case "-gt":
                        case "-GT":
                        case "-GTModel":
                            if (value.ToLower().Contains("none"))
                                GTModel = GenotypeModel.None;
                            else if (value.ToLower().Contains("threshold"))
                                GTModel = GenotypeModel.Thresholding;
                            else if (value.ToLower().Contains("symmetric"))
                                throw new ArgumentException(string.Format("Temporarily depracated '{0}'", value));
                            else
                                throw new ArgumentException(string.Format("Unknown genotype model '{0}'", value));
                            break;
                        case "-CrushVcf":
                        case "-crushvcf":
                            bool crushedallelestyle = bool.Parse(value);
                            AllowMultipleVcfLinesPerLoci = !(crushedallelestyle);
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
                        case "-OutputSBFiles":
                            OutputBiasFiles = bool.Parse(value);
                            break;
                        case "-p":
                        case "-OnlyUseProperPairs":
                            OnlyUseProperPairs = bool.Parse(value);
                            break;
                        case "-q":
                        case "-MaxVariantQScore":
                            MaximumVariantQScore = int.Parse(value);
                            break;
                        case "-MaxGenotypeQScore":
                            MaximumGenotypeQScore = int.Parse(value);
                            break;
                        case "-MinGenotypeQScore":
                            MinimumGenotypeQScore = int.Parse(value);
                            break;
                        case "-s":
                        case "-MaxAcceptableStrandBiasFilter":
                            StrandBiasAcceptanceCriteria = float.Parse(value);
                            break;
                        case "-StitchPairedReads":
                            StitchReads = bool.Parse(value);
                            break;
                        case "-t":
                        case "-MaxNumThreads":
                            MaxNumThreads = int.Parse(value);
                            break;
                        case "-ThreadByChr":
                            ThreadByChr = bool.Parse(value);
                            break;
                        case "-ReportNoCalls":
                            ReportNoCalls = bool.Parse(value);
                            break;
                        case "-XcStitcher":
                            UseXCStitcher = bool.Parse(value);
                            break;
                        case "-OutFolder":
                            OutputFolder = value;
                            break;
                        case "-Collapse":
                            Collapse = bool.Parse(value);
                            break;
                        case "-CollapseFreqThreshold":
                            CollapseFreqThreshold = float.Parse(value);
                            break;
                        case "-CollapseFreqRatioThreshold":
                            CollapseFreqRatioThreshold = float.Parse(value);
                            break;
                        case "-PriorsPath":
                            PriorsPath = value;
                            break;
                        case "-TrimMnvPriors":
                            TrimMnvPriors = bool.Parse(value);
                            break;
                        case "-NifyDisagreements":
                            NifyDisagreements = bool.Parse(value);
                            break;
                        case "-MaxFragmentSize":
                            MaxFragmentSize = Int32.Parse(value);
                            break;
                        case "-ChrFilter":
                            ChromosomeFilter = value;
                            break;
                        case "-UnstitchableStrategy":
                            if (value.ToLower().Contains("both"))
                                UnstitchableStrategy = UnstitchableStrategy.TakeBothReads;
                            else if (value.ToLower().Contains("stronger"))
                                UnstitchableStrategy = UnstitchableStrategy.TakeStrongerRead;
                            else if (value.ToLower().Contains("none"))
                                UnstitchableStrategy = UnstitchableStrategy.TakeNoReads;
                            else
                                throw new ArgumentException(string.Format("Unknown unstitchable strategy '{0}'", value));
                            break;
                        case "-CoverageMethod":
                            if (value.ToLower() == "approximate")
                                CoverageMethod = CoverageMethod.Approximate;
                            else if (value.ToLower() == "exact")
                                CoverageMethod = CoverageMethod.Exact;
                            else
                                throw new ArgumentException(string.Format("Unknown coverage method '{0}'", value));
                            break;
                        case "-ReportRcCounts":
                            ReportRcCounts = bool.Parse(value);
                            break;
                        case "-InsideSubProcess":
                            InsideSubProcess = bool.Parse(value);
                            break;
                        case "-MultiProcess":
                            MultiProcess = bool.Parse(value);
                            break;
                        case "-Mono":
                        case "-mono":
                            MonoPath = value;
                            break;
                        case "-RMxNFilter":
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
                            if (rmxnThresholds.Length != 2)
                                throw new ArgumentException(string.Format("RMxNFilter argument requires exactly two values."));
                            RMxNFilterMaxLengthRepeat = (int)rmxnThresholds[0];
                            RMxNFilterMinRepetitions = (int)rmxnThresholds[1];
                            break;
                        case "-SkipNonIntervalAlignments":
                            SkipNonIntervalAlignments = bool.Parse(value);
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