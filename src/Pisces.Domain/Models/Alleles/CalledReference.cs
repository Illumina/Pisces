using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
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
