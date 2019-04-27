namespace Gemini.Utility
{
    public class GeminiOptions
    {
        public int GenomeContextSize = 1000;
        public string IndelsCsvName = "Indels.csv";
        public bool CheckSA;
        public bool Debug { get; set; }
        public bool StitchOnly { get; set; }
        public string GenomePath { get; set; }
        public string SamtoolsPath { get; set; }
        public bool TrustSoftclips { get; set; }
        public bool SkipStitching { get; set; }
        public bool KeepBothSideSoftclips { get; set; }
        public bool SkipAndRemoveDups { get; set; } = true;
        public bool KeepProbeSoftclip { get; set; }
        public bool KeepUnmergedBams { get; set; }
        public bool IsWeirdSamtools { get; set; }
        public bool UseHygeaComparer { get; set; }
        public bool AllowRescoringOrigZero { get; set; }
        public bool IndexPerChrom { get; set; }
        public bool SortPerChrom { get; set; } = true;
        public bool SoftclipUnknownIndels { get; set; }
        public bool RemaskMessySoftclips { get; set; }
        public bool SkipEvidenceCollection { get; set; }
        public string FinalIndelsOverride { get; set; }
        public int ReadCacheSize { get; set; } = 100000;
        public int RegionSize { get; set; } = 10000000;
        public int MessySiteThreshold { get; set; } = 1;
        public int MessySiteWidth { get; set; } = 500;
        public bool CollectDepth { get; set; } = true;
        public double ImperfectFreqThreshold { get; set; } = 0.03;
        public double IndelRegionFreqThreshold { get; set; } = 0.01;
        public int RegionDepthThreshold { get; set; } = 5;
        public bool ForceHighLikelihoodRealigners { get; set; }
        public bool RequirePositiveOutcomeForSnowball { get; set; } = true;
        public bool RecalculateUsableSitesAfterSnowball { get; set; } = true;
        public bool AvoidLikelySnvs { get; set; } = false;
        public bool LogRegionsAndRealignments { get; set; } = false;
        public bool LightDebug { get; set; }
        public int NumConcurrentRegions { get; set; } = 1;
        public float DirectionalMessThreshold { get; set; } = 0.2f;
        public bool TreatAbnormalOrientationAsImproper { get; set; }
        public int MessyMapq { get; set; }
        public int NumSoftclipsToBeConsideredMessy { get; set; } = 8;
        public int NumMismatchesToBeConsideredMessy { get; set; } = 3;
        public bool SilenceSuspiciousMdReads { get; set; }
        public bool SilenceDirectionalMessReads { get; set; }
        public bool SilenceMessyMapMessReads { get; set; }
    }
}
