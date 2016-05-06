namespace Pisces.Domain.Types
{
    public enum FilterType
    {
        StrandBias,
        LowVariantQscore,  //the confidence a variant exists
        LowDepth,
        LowVariantFrequency,
        LowGenotypeQuality, //the confidence in a particular genotype (not for Somatic)
        IndelRepeatLength,
        MultiAllelicSite,
        RMxN
    }
}
