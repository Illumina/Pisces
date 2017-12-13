namespace VariantQualityRecalibration
{

    public enum MutationCategory
    {
        AtoC = 1,       //
        AtoG,          //
        AtoT,          //
        CtoA,          //
        CtoG,          //
        CtoT,          // "C-> U-> T" deamidization
        GtoA,          // deamidization
        GtoC,          //
        GtoT,          //
        TtoA,          //
        TtoC,          //
        TtoG,          //
        Insertion,     //
        Deletion,
        Reference,
        Other
    }
}
