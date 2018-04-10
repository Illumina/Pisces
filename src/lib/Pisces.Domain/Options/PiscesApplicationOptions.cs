using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Common.IO.Utility;

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

                if (string.IsNullOrEmpty(OutputDirectory))
                {
                    if (string.IsNullOrEmpty(firstBamFolder)) //the rare case when the input bam is "mybam.bam" nad has no parent folder
                        return DefaultLogFolderName;
                    else
                        return Path.Combine(firstBamFolder, DefaultLogFolderName); //no output folder was given
                }
                else //an output folder was given
                {
                    return Path.Combine(OutputDirectory, DefaultLogFolderName);

                }
            }
        }

        #endregion
        // ReSharper restore InconsistentNaming


        public void SetDerivedParameters()
        {
          
            int processorCoreCount = Environment.ProcessorCount;
            if (MaxNumThreads > 0)
                MaxNumThreads = Math.Min(processorCoreCount, MaxNumThreads);

            VariantCallingParameters.SetDerivedParameters(BamFilterParameters);
            VcfWritingParameters.SetDerivedParameters(VariantCallingParameters);
        }

  

        public void ValidateAndSetDerivedValues()
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

            if (!string.IsNullOrEmpty(OutputDirectory) && bamPathsSpecified && (BAMPaths.Length > 1))
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
                            throw new ArgumentException(string.Format("VCF file name collision. Cannot process two different bams with the same name {0} into the same output folder {1}.", fileA, OutputDirectory));
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
            //will throw if invalid
            return(ValidateBamProcessorPaths(BAMPaths, GenomePaths, IntervalPaths));
        }
         
    }
}