using System;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
{
    public class CalledVariant : BaseCalledAllele
    {
        public int ReferenceSupport { get; set; }

        public float RefFrequency
        {
            get { return TotalCoverage == 0 ? 0f : Math.Min((float)ReferenceSupport / TotalCoverage, 1); }
        }

        public CalledVariant(AlleleCategory type)
        {
            Genotype = Genotype.HeterozygousAltRef;
            Type = type;
        }
    }
}
