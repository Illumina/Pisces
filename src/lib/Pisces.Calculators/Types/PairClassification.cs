namespace Gemini.Types
{
    public enum PairClassification
    {
        /// <summary>
        /// 
        /// </summary>
        Unknown,
        /// <summary>
        /// High-quality* paired reads that successfully stitch and:
        ///  do not contain any suspicious cigar operations
        ///  do not contain any mismatches as encoded in the NM tag**
        /// </summary>
        PerfectStitched,
        ImperfectStitched,
        Disagree,
        FailStitch,
        Unusable,
        Split,
        UnstitchIndel,
        Unstitchable,
        MessyStitched,
        MessySplit,
        UnusableSplit,
        UnstitchImperfect,
        UnstitchPerfect,
        LongFragment,
        UnstitchMessy,
        UnstitchSingleMismatch,
        SingleMismatchStitched,
        UnstitchableAsSingleton,
        IndelSingleton,
        IndelUnstitchable,
        UnstitchForwardMessy,
        UnstitchReverseMessy,
        Improper,
        IndelImproper,
        UnstitchMessyIndel,
        UnstitchMessySuspiciousRead,
        UnstitchMessyIndelSuspiciousRead,
        UnstitchForwardMessyIndel,
        UnstitchReverseMessyIndel,
        Duplicate,
        UnstitchMessySuspiciousMd,
        Unhandleable
    }
}