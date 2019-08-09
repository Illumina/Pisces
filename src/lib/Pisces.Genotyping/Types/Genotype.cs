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
        AltLikeNoCall,// ./.
        RefAndNoCall, // 0/.
        AltAndNoCall,// 1/.
		HemizygousRef, // 0
		HemizygousAlt, // 1
		HemizygousNoCall, // .
		Others // */*
    }

    public enum SimplifiedDiploidGenotype
    {
        HomozygousRef, // 0/0
        HeterozygousAltRef, // 0/1   
        HomozygousAlt, // 1/1
    }
}
