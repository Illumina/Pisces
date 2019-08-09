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
        public bool Collapse = true;
        public string PriorsPath;
        public bool TrimMnvPriors;
        public float CollapseFreqThreshold = 0f;
        public float CollapseFreqRatioThreshold = 0.5f;
        public bool ExcludeMNVsFromCollapsing = false;
        public bool SkipNonIntervalAlignments = false;  //keep this off. it currently has bugs, speed issues, and no plan to fix it. this line is still here to remind ourselves not to try it again.
	    public List<string> ForcedAllelesFileNames = new List<string>();
        public bool UseStitchedXDInfo = false;
        public uint TrackedAnchorSize = 5;

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

    }
}