using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Common.IO.Utility;
using System.Reflection;
using Newtonsoft.Json;

namespace Pisces.Domain.Options
{
    /// <summary>
    ///     Options to the somatic variant caller: Mostly thresholds for various filters.
    ///     The filter cutoffs will NOT be exposed to the customer, but we'll be exploring
    ///     various combinations internally.
    /// </summary>
    // ReSharper disable InconsistentNaming - prevents ReSharper from renaming serializeable members that are sensitive to being changed
    public class PiscesApplicationOptions : BamProcessorOptions
    {
        public const string DefaultLogFolderName = "PiscesLogs";
        public const int RegionSize = 1000;

        public string LogFileName
        {
            get
            {
                //TODO, refactor this out. Verify thread by chr still working as expected.
                if (InsideSubProcess)
                {
                    var identifier = Thread.CurrentThread.Name + Thread.CurrentThread.ManagedThreadId;
                    
                    if (string.IsNullOrEmpty(identifier))
                        throw (new Exception("InsideSubProcess not yet supported for this processor framework"));

                    return identifier + "_" + LogFileNameBase;
                }
                return LogFileNameBase;
            }
        }

        #region Serializeable Types and Members

        public string[] CommandLineArguments;
        public string LogFileNameBase = "PiscesLog.txt";
        public VcfWritingParameters VcfWritingParameters = new VcfWritingParameters();
        public VariantCallingParameters VariantCallingParameters = new VariantCallingParameters();
        public BamFilterParameters BamFilterParameters = new BamFilterParameters();

        public string[] IntervalPaths;
        public bool OutputBiasFiles = false;
        public int NoiseModelHalfWindow = 1;
        public bool DebugMode = false;
        public bool CallMNVs = false;
        public bool ThreadByChr = false;
        public int MaxSizeMNV = 3;
        public int MaxGapBetweenMNV = 1;
        public bool UseMNVReallocation = true;
        public CoverageMethod CoverageMethod = CoverageMethod.Approximate;
        public string MonoPath; //only needed if running on Linux cluster, and we plan to spawn processes
        public bool Collapse = true;
        public string PriorsPath;
        public bool TrimMnvPriors;
        public float CollapseFreqThreshold = 0f;
        public float CollapseFreqRatioThreshold = 0.5f;
        public bool ExcludeMNVsFromCollapsing = false;
        public bool SkipNonIntervalAlignments = false;  //keep this off. it currently has bugs, speed issues, and no plan to fix it)
	    public List<string> ForcedAllelesFileNames = new List<string>();

        public string LogFolder
        {
            get
            {
                if (BAMPaths == null || BAMPaths.Length == 0)
                    throw new ArgumentException("Unable to start logging: cannot determine log folder. BamPaths are used to determine default log path, and none were supplied.");

                var firstBamFolder = Path.GetDirectoryName(BAMPaths[0]);

                if (string.IsNullOrEmpty(OutputFolder))
                {
                    if (string.IsNullOrEmpty(firstBamFolder)) //the rare case when the input bam is "mybam.bam" nad has no parent folder
                        return DefaultLogFolderName;
                    else
                        return Path.Combine(firstBamFolder, DefaultLogFolderName); //no output folder was given
                }
                else //an output folder was given
                {
                    return Path.Combine(OutputFolder, DefaultLogFolderName);

                }
            }
        }

        #endregion
        // ReSharper restore InconsistentNaming

        public static void PrintVersionToConsole()
        {
            var entryAssembly = Assembly.GetEntryAssembly().GetName();
            Console.WriteLine(entryAssembly.Name + " " + entryAssembly.Version);
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
            Console.WriteLine(" -TargetLODFrequency/-TargetVF : Target Frequency to call a variant. Ie, to target a 5% allele frequency, we must call down to 2.6%, to capture that 5% allele 95% of the time. This parameter is used by the Somatic Genotyping Model");
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
            Console.WriteLine(" -ReportRcCounts : Report collapsed read count, When BAM files contain XW and XV tags, output read counts for duplex-stitched, duplex-nonstitched, simplex-stitched, and simplex-nonstitched.  'true' or 'false'. default, false");
            Console.WriteLine(" -ReportTsCounts : Report collapsed read count by different template strands, Conditional on ReportRcCounts, output read counts for duplex-stitched, duplex-nonstitched, simplex-forward-stitched, simplex-forward-nonstitched, simplex-reverse-stitched, simplex-reverse-nonstitched.  'true' or 'false'. default, false");
            Console.WriteLine(" -Ploidy : 'somatic' or 'diploid'. default, somatic.");
            Console.WriteLine(" -DiploidGenotypeParameters : A,B,C. default 0.20,0.70,0.80");
            Console.WriteLine(" -RMxNFilter : M,N,F. Comma-separated list of integers indicating max length of the repeat section (M), the minimum number of repetitions of that repeat (N), to be applied if the variant frequency is less than (F). Default is R5x9,F=20.");
            Console.WriteLine(" -CoverageMethod : 'approximate' or 'exact'. Exact is more precise but requires more memory (minimum 8 GB).  Default approximate");
            Console.WriteLine(" -CollapseFreqThreshold : When collapsing, minimum frequency required for target variants. Default '0'");
            Console.WriteLine(" -CollapseFreqRatioThreshold : When collapsing, minimum ratio required of target variant frequency to collapsible variant frequency. Default '0.5f'");
            Console.WriteLine(" -NoiseModel : Window/Flat. Default Flat ");
			Console.WriteLine(" -ForcedAlleles : vcf path for alleles that are forced to report");
			BamProcessorOptions.PrintUsageInfo();
            Console.WriteLine("");
            Console.WriteLine("Note, all options are case insensitive.");

        }

        public static PiscesApplicationOptions ParseCommandLine(string[] arguments)
        {
            var options = new PiscesApplicationOptions();
            if (options.UpdateOptions(arguments) == null) return null;

            options.Validate();

            return options;
        }

        public void SetDerivedParameters()
        {
            if (!string.IsNullOrWhiteSpace(BAMFolder))
                PopulateBAMPaths();

            int processorCoreCount = Environment.ProcessorCount;
            if (MaxNumThreads > 0)
                MaxNumThreads = Math.Min(processorCoreCount, MaxNumThreads);

            VariantCallingParameters.SetDerivedParameters(BamFilterParameters);
            VcfWritingParameters.SetDerivedParameters(VariantCallingParameters);
        }

        public void Validate()
        {
            bool bamPathsSpecified = ValidateInputPaths();

            SetDerivedParameters();
            BamFilterParameters.Validate();
            VariantCallingParameters.Validate();

            if (CallMNVs)
            {
                ValidationHelper.VerifyRange(MaxSizeMNV, 1, RegionSize, "MaxPhaseSNPLength");
                ValidationHelper.VerifyRange(MaxGapBetweenMNV, 0, int.MaxValue, "MaxGapPhasedSNP");
            }
            ValidationHelper.VerifyRange(MaxNumThreads, 1, int.MaxValue, "MaxNumThreads");
            ValidationHelper.VerifyRange(CollapseFreqThreshold, 0f, float.MaxValue, "CollapseFreqThreshold");
            ValidationHelper.VerifyRange(CollapseFreqRatioThreshold, 0f, float.MaxValue, "CollapseFreqRatioThreshold");

            if (!string.IsNullOrEmpty(PriorsPath))
            {
                if (!File.Exists(PriorsPath))
                    throw new ArgumentException(string.Format("PriorsPath '{0}' does not exist.", PriorsPath));
            }



            if (ThreadByChr && !InsideSubProcess && !string.IsNullOrEmpty(ChromosomeFilter))
                throw new ArgumentException("Cannot thread by chromosome when filtering on a particular chromosome.");

            if (!string.IsNullOrEmpty(OutputFolder) && bamPathsSpecified && (BAMPaths.Length > 1))
            {
                //make sure none of the input BAMS have the same name. Or else we will have an output collision.
                for (int i = 0; i < BAMPaths.Length; i++)
                {
                    for (int j = i + 1; j < BAMPaths.Length; j++)
                    {
                        if (i == j)
                            continue;

                        var fileA = Path.GetFileName(BAMPaths[i]);
                        var fileB = Path.GetFileName(BAMPaths[j]);

                        if (fileA == fileB)
                        {
                            throw new ArgumentException(string.Format("VCF file name collision. Cannot process two different bams with the same name {0} into the same output folder {1}.", fileA, OutputFolder));
                        }
                    }
                }
            }

	        if (ForcedAllelesFileNames!=null && ForcedAllelesFileNames.Count > 0 && !VcfWritingParameters.AllowMultipleVcfLinesPerLoci)
	        {
		        throw new ArgumentException("Cannot support forced Alleles when crushing vcf lines, please set -crushvcf false");
	        }
        }


        private bool ValidateInputPaths()
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
                        throw new ArgumentException(string.Format("BAM file '{0}' does not exist.", bamPath));
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
            return bamPathsSpecified;
        }


        private float[] ParseStringToFloat(string[] stringArray)
        {
            var parameters = new float[stringArray.Length];

            for (int i = 0; i < parameters.Length; i++)
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

        public PiscesApplicationOptions UpdateOptions(string[] arguments)
        {
            var usedOptions = BamFilterParameters.Parse(arguments);
            usedOptions.AddRange(VariantCallingParameters.Parse(arguments));
            usedOptions.AddRange(VcfWritingParameters.Parse(arguments));

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
                        case "-b":
                        case "-bam":
                            BAMPaths = value.Split(Delimiter);
                            break;
                        case "-d":
                        case "-debug":
                            DebugMode = bool.Parse(value);
                            break;
                        case "-g":
                        case "-genomepaths":
                            GenomePaths = value.Split(Delimiter);
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
                            IntervalPaths = value.Split(Delimiter);
                            break;
                        case "-outputsbfiles":
                            OutputBiasFiles = bool.Parse(value);
                            break;
                        case "-stitchpairedreads":
                            throw new ArgumentException("StitchPairedReads option is obsolete.");
                        case "-t":
                            MaxNumThreads = int.Parse(value);
                            break;
                        case "-threadbychr":
                            ThreadByChr = bool.Parse(value);
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
                        case "-collapseexcludemnvs":
                            ExcludeMNVsFromCollapsing = bool.Parse(value);
                            break;
                        case "-priorspath":
                            PriorsPath = value;
                            break;
                        case "-trimmnvpriors":
                            TrimMnvPriors = bool.Parse(value);
                            break;
                        case "-coverageMethod":
                            if (value.ToLower() == "approximate")
                                CoverageMethod = CoverageMethod.Approximate;
                            else if (value.ToLower() == "exact")
                                CoverageMethod = CoverageMethod.Exact;
                            else
                                throw new ArgumentException(string.Format("Unknown coverage method '{0}'", value));
                            break;
                        case "-mono":
                            MonoPath = value;
                            break;
                        case "-baselogname":
                            LogFileNameBase = value;
                            break;
						case "-forcedalleles":
		                    ForcedAllelesFileNames = value.Split(',').ToList();
							break;
		                default:
                            if (!base.UpdateOptions(lastArgumentField, value) && !(usedOptions.Contains(lastArgumentField)))
                                Logger.WriteToLog(string.Format("Unknown argument '{0}'. Continuing without it.", arguments[argumentIndex]));
                            break;
                    }
                    argumentIndex += 2;
                }

                CommandLineArguments = arguments;

                return this;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(string.Format("Unable to parse argument {0}: {1}", lastArgumentField, ex.Message));
            }
        }

        public void Save(string filepath)
        {
            JsonUtil.Save(filepath, this);
        }

        public static PiscesApplicationOptions GetPiscesOptionsFromVcfHeader(List<string> VcfHeaderLines)
        {

            var startString = "##Pisces_cmdline=";
            if (VcfHeaderLines.Count != 0 && VcfHeaderLines.Exists(x => x.StartsWith(startString)))
            {
                try
                {
                    var piscesCmd = VcfHeaderLines.FindLast(x => x.StartsWith(startString)).Replace(startString, "").Replace("\"", "");
                    piscesCmd = piscesCmd.Replace("-v", "-vffilter"); //"v" used to be vf filter, now it returns the version number. Be kind and help the user with this one. If th ey pass "-v" that will shut down all the parsing and output the version.
                    PiscesApplicationOptions piscesOptions = new PiscesApplicationOptions();
                    return (piscesOptions.UpdateOptions(piscesCmd.Split()));
                }
                catch (Exception ex)
                {
                    Logger.WriteToLog("Unable to parse the original Pisces commandline");
                    Logger.WriteExceptionToLog(ex);
                }
            }

            return null;
        }

        public void PopulateBAMPaths()
        {
            BAMPaths = Directory.GetFiles(BAMFolder, "*.bam");
            if (!BAMPaths.Any())
                throw new ArgumentException(string.Format("No BAM files found in {0}", BAMFolder));
        }
    }
}