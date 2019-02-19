using System;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
{
    public class BaseAllele : IAllele
    {
        public string Chromosome { get; set; }
        public int ReferencePosition { get; set; }
        public string ReferenceAllele { get; set; }
        public string AlternateAllele { get; set; }

        public AlleleCategory Type { get; set; }

        // RC counts - optionally used
        public int[] ReadCollapsedCountsMut { get; set; }

        public override string ToString()
        {
            return (string.Join("\t", Chromosome, ReferencePosition, ".", ReferenceAllele, AlternateAllele));
        }

        public int Length
        {
            get
            {
                switch (Type)
                {
                    case AlleleCategory.Mnv:
                    case AlleleCategory.Snv:
                        return AlternateAllele.Length;
                    case AlleleCategory.Insertion:
                        return AlternateAllele.Length - 1;
                    case AlleleCategory.Deletion:
                        return ReferenceAllele.Length - 1;
                    case AlleleCategory.Reference:
                        return ReferenceAllele.Length;
                    default:
                        throw new ArgumentException("Unrecognized allele type: " + Type);
                }
            }
        }

        public void SetType()
        {
            Type = CalculateType();
        }

        private AlleleCategory CalculateType()
        {

            if (!String.IsNullOrEmpty(ReferenceAllele)
                && !String.IsNullOrEmpty(AlternateAllele))
            {
                if (ReferenceAllele == AlternateAllele)
                    return (AlleleCategory.Reference);

                if (AlternateAllele == ".")
                    return (AlleleCategory.Reference);

                if (ReferenceAllele.Length == AlternateAllele.Length)
                {
                    return (AlternateAllele.Length == 1 ? AlleleCategory.Snv : AlleleCategory.Mnv);
                }
                else
                {
                    if (ReferenceAllele.Length == 1)
                        return (AlleleCategory.Insertion);
                    else if (AlternateAllele.Length == 1)
                        return (AlleleCategory.Deletion);
                }
            }

            return AlleleCategory.Unsupported;
        }

    }
}
