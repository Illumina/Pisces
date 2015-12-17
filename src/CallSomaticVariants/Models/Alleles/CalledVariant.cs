using System;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Models.Alleles
{
    public class CalledVariant : BaseCalledAllele
    {
        public int ReferenceSupport { get; set; }

        public float RefFrequency
        {
            get { return TotalCoverage == 0 ? 0f : Math.Min((float)ReferenceSupport / TotalCoverage, 1); }
        }

        public int Length
        {
            get
            {
                switch (Type)
                {
                    case AlleleCategory.Mnv:
                        return Alternate.Length;
                    case AlleleCategory.Insertion:
                        return Alternate.Length - 1;
                    case AlleleCategory.Deletion:
                        return Reference.Length - 1;
                    default:
                        return 1; // snv
                }
            }
        }

        public CalledVariant(AlleleCategory type)
        {
            Genotype = Genotype.HeterozygousAlt;
            Type = type;
        }
    }
}
