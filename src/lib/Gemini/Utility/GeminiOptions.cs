namespace Gemini.Utility
{
    public class GeminiOptions
    {
        public int GenomeContextSize = 1000;
        public string IndelsCsvName = "Indels.csv";
        public bool Debug { get; set; }
        public bool StitchOnly { get; set; }
        public string GenomePath { get; set; }
        public string SamtoolsPath { get; set; }
        public bool TrustSoftclips { get; set; }
        public bool SkipStitching { get; set; }
        public bool KeepBothSideSoftclips { get; set; }
        public bool DeferStitchIndelReads { get; set; }
        public bool SkipAndRemoveDups { get; set; }
        public int CacheSize = 1000;
        public bool KeepProbeSoftclip { get; set; }
        public bool KeepUnmergedBams { get; set; }
        public bool IsWeirdSamtools { get; set; }
        public bool UseHygeaComparer { get; set; }
        public bool AllowRescoringOrigZero { get; set; }
        public bool IndexPerChrom { get; set; }
        public bool SoftclipUnknownIndels { get; set; }
    }
}