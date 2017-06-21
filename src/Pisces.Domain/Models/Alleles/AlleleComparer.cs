using System;
using System.Collections.Generic;

namespace Pisces.Domain.Models.Alleles
{
    public class AlleleComparer : IComparer<CalledAllele>
    {
        private readonly bool _chrMFirst;

        public AlleleComparer(bool chrMFirst = true)
        {
            _chrMFirst = chrMFirst;
        }

        public int Compare(CalledAllele x, CalledAllele y)
        {
            return OrderVariants(x, y, _chrMFirst);
        }

        public static int OrderVariants(CalledAllele a, CalledAllele b, bool mFirst)
        {
            return OrderAlleles(a, b, mFirst);
        }



        public static int OrderAlleles(CalledAllele a, CalledAllele b, bool mFirst)
        {
            //return -1 if A comes first

            if ((a == null) && (b == null))
            {
                throw new ArgumentException("Allele 'a' and 'b' are null and cannot be ordered. Check the backlog is not empty.");
            }

            if (a == null)
                return 1;
            if (b == null)
                return -1;



            if (a.Chromosome != b.Chromosome)
            {
                if (!(a.Chromosome.Contains("chr")) || !(b.Chromosome.Contains("chr")))
                    throw new ArgumentException("Chromosome name in input allele 'a' or 'b' .vcf is not supported.  Cannot order variants.");

                try
                {
                    int chrNumA;
                    int chrNumB;

                    var aisInt = Int32.TryParse(a.Chromosome.Replace("chr", ""), out chrNumA);
                    var bIsInt = Int32.TryParse(b.Chromosome.Replace("chr", ""), out chrNumB);
                    var aIsChrM = a.Chromosome.ToLower() == "chrm";
                    var bIsChrM = b.Chromosome.ToLower() == "chrm";

                    //for simple chr[1,2,3...] numbered, just order numerically 
                    if (aisInt && bIsInt)
                    {
                        if (chrNumA < chrNumB) return -1;
                        if (chrNumA > chrNumB) return 1;
                    }

                    if (mFirst)
                    {
                        if (aIsChrM && bIsChrM) return 0; //equal
                        if (aIsChrM) return -1; //A goes first
                        if (bIsChrM) return 1;  //B goes first
                    }

                    //order chr1 before chrX,Y,M
                    if (aisInt && !bIsInt) return -1; //A goes first
                    if (!aisInt && bIsInt) return 1;  //B goes first

                    //these chrs are alphanumeric.  Order should be X,Y,M .
                    //And lets try not to crash on alien dna like chrW and chrFromMars
                    if (!aisInt)
                    {
                        //we only go down this path if M is not first.
                        if (aIsChrM && bIsChrM) return 0; //equal
                        if (aIsChrM) return 1; //B goes first
                        if (bIsChrM) return -1;  //A goes first

                        //order remaining stuff {x,y,y2,HEDGeHOG } alphanumerically.
                        return (String.Compare(a.Chromosome, b.Chromosome));
                    }
                }
                catch
                {
                    throw new ArgumentException(String.Format("Cannot order variants with chr names {0} and {1}.", a.ReferencePosition, b.ReferencePosition));
                }
            }

            if (a.ReferencePosition < b.ReferencePosition) return -1;
            return a.ReferencePosition > b.ReferencePosition ? 1 : 0;
        }
    }
}
