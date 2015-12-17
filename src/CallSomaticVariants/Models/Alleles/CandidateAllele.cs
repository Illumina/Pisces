using System;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Models.Alleles
{
    public class CandidateAllele : IAllele
    {
        public string Chromosome { get; private set; }
        public int Coordinate { get; private set; }
        public string Reference { get; private set; }
        public string Alternate { get; private set; }

        public AlleleCategory Type { get; private set; }

        public int Support { get { return SupportByDirection.Sum(); } }
        public int[] SupportByDirection { get; set; }

        public CandidateAllele(string chromosome, int coordinate, string reference, string alternate, AlleleCategory type)
        {
            Type = type;
            Chromosome = chromosome;
            Coordinate = coordinate;
            Reference = reference;
            Alternate = alternate;

            if (string.IsNullOrEmpty(chromosome))
                throw new ArgumentException("Chromosome is empty.");

            if (coordinate < 0)
                throw new ArgumentException(string.Format("Coordinate {0} is invalid.",coordinate));

            if (string.IsNullOrEmpty(reference))
                throw new ArgumentException("Reference is empty.");

            if (string.IsNullOrEmpty(alternate))
                throw new ArgumentException("Alternate is empty.");
            
            SupportByDirection = new int[] { 0, 0, 0 };
        }

        /// <summary>
        /// Check if other variant is the same.  Assumes other variant is not null to save speed.  
        /// Also assumes strings are same casing which is controlled at higher level
        /// </summary>
        /// <param name="otherVariant"></param>
        /// <returns></returns>
        public override bool Equals(object o)
        {
            var otherVariant = o as CandidateAllele;

            return otherVariant != null 
                   && otherVariant.Type == Type
                   && otherVariant.Reference.Equals(Reference)
                   && otherVariant.Alternate.Equals(Alternate)
                   && otherVariant.Coordinate == Coordinate
                   && otherVariant.Chromosome.Equals(Chromosome);
        }
    }
}