namespace Pisces.Domain.Types
{
    public enum Genotype
    {
        HeterozygousAlt1Alt2, // 1/2  <- used by Diploid ploidy model only
        Alt12LikeNoCall,// ./.         <- used by Diploid ploidy model only
        HeterozygousAltRef, // 0/1
        HomozygousAlt, // 1/1
        HomozygousRef, // 0/0
        RefLikeNoCall, // ./.
        AltLikeNoCall// ./.
    }
}
