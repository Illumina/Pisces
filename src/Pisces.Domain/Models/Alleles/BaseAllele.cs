using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
{
    public class BaseAllele : IAllele
    {
        public string Chromosome { get; set; }
        public int Coordinate { get; set; }
        public string Reference { get; set; }
        public string Alternate { get; set; }

        public AlleleCategory Type { get; set; }

        // RC counts - optionally used
        public int[] ReadCollapsedCounts { get; set; }

        public override string ToString()
        {
            return (string.Join("\t", Chromosome, Coordinate, ".", Reference, Alternate));
        }
        public int Length
        {
            get
            {
                switch (Type)
                {
                    case AlleleCategory.Mnv:
                    case AlleleCategory.Snv:
                        return Alternate.Length;
                    case AlleleCategory.Insertion:
                        return Alternate.Length - 1;
                    case AlleleCategory.Deletion:
                        return Reference.Length - 1;
                    case AlleleCategory.Reference:
                        return Reference.Length;
                    default:
                        throw new Exception("Unrecognized allele type: " + Type);
                }
            }
        }
    }
}
