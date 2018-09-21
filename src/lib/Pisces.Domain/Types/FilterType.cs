namespace Pisces.Domain.Types
{
    public enum FilterType
    {
        StrandBias,
        PoolBias,
        LowVariantQscore,  //the confidence a variant exists
        LowDepth,
        LowVariantFrequency,
        LowGenotypeQuality, //the confidence in a particular genotype 
        IndelRepeatLength,
        MultiAllelicSite,
        RMxN,
		ForcedReport,
        OffTarget,
        NoCall
    }
}
