using CallSomaticVariants.Types;

namespace CallSomaticVariants.Models.Alleles
{
    public class CalledReference : BaseCalledAllele
    {
        public CalledReference()
        {
            Genotype = Genotype.HomozygousRef;
            Type = AlleleCategory.Reference;
        }
    }
}
