 using System;
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using Pisces.Domain.Utility;
    using Pisces.Domain.Options;
    using Common.IO;
    using CommandLine.NDesk.Options;

namespace RealignIndels
{
    public class HygeaOptions : BamProcessorOptions
    {
        public const string DefaultLogFolderName = "Logs";
        public const string LogFileNameBase = "IndelRealignmentLog.txt";
        public float IndelFreqCutoff = 0.01f;
        public int MinimumBaseCallQuality = 10;
        public int RealignWindowSize = 250;
        public string PriorsPath;
        public int MaxIndelSize = 50;
        public bool TryThree = false;
        public int AnchorSizeThreshold = 25;
        public bool SkipDuplicates = false;
        public bool SkipAndRemoveDuplicates = true;
        public bool RemaskSoftclips = true;
        public bool MaskPartialInsertion = false;
        public bool AllowRescoringOrigZero = true;
        public int MaxRealignShift = 250;
        public bool TryRealignSoftclippedReads = true;


        public bool Debug { get; set; }

        public int IndelLengthCoefficient = 0;

        public int AnchorLengthCoefficient = 0;

        public int SoftclipCoefficient = -1;

        public int IndelCoefficient = -1;

        public int MismatchCoefficient = -2;

        public string LogFileName
        {
            get
            {
                if (InsideSubProcess)
                    return System.Diagnostics.Process.GetCurrentProcess().Id + "_" + LogFileNameBase;
                return LogFileNameBase;
            }
        }





        public string LogFolder
        {
            get
            {

                return OutputDirectory ?? Path.Combine(Path.GetDirectoryName(BAMPaths[0]), DefaultLogFolderName);
            }
        }

        public bool UseAlignmentScorer { get; set; }

        public void Validate()
        {
            
            ValidationHelper.VerifyRange(MinimumBaseCallQuality, 0, null, "minBaseQuality");
            ValidationHelper.VerifyRange(IndelFreqCutoff, 0f, 1f, "minDenovoFreq");
            ValidationHelper.VerifyRange(MaxNumThreads, 1, null, "maxNumThreads");
            ValidationHelper.VerifyRange(MaxIndelSize, 1, 100, "maxIndelSize");

            if (!string.IsNullOrEmpty(PriorsPath))
            {
                if (!File.Exists(PriorsPath))
                    throw new ArgumentException(string.Format("priorsFile '{0}' does not exist.", PriorsPath));
            }
        }
 

    

    }
}