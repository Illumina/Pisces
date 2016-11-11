using System;
using System.Linq;
using System.Collections.Generic;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
{
    public class CandidateAllele : BaseAllele
    {
        public int Support { get { return SupportByDirection.Sum(); } }
        public int[] SupportByDirection { get; set; }

        public bool OpenOnRight { get; set; }
        public bool OpenOnLeft { get; set; }
        public bool FullyAnchored { get { return !OpenOnLeft && !OpenOnRight; } }
        public bool IsKnown { get; set; }

        public float Frequency { get; set; }

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

            SupportByDirection = new int[Constants.NumDirectionTypes];
            ReadCollapsedCounts = new int[Constants.NumReadCollapsedTypes];
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
                   && otherVariant.Coordinate == Coordinate
                   && otherVariant.Alternate.Equals(Alternate)
                   && otherVariant.Type == Type
                   && otherVariant.Chromosome.Equals(Chromosome)
                   && otherVariant.Reference.Equals(Reference);    
        }

        public void AddSupport(CandidateAllele fromAllele)
        {
            for (var i = 0; i < SupportByDirection.Length; i++)
                SupportByDirection[i] += fromAllele.SupportByDirection[i];
        }

        public CandidateAllele DeepCopy()
        {
            var copy = new CandidateAllele(Chromosome, Coordinate, Reference, Alternate, Type)
            {
                OpenOnLeft = OpenOnLeft,
                OpenOnRight = OpenOnRight,
                IsKnown = IsKnown
            };
            Array.Copy(SupportByDirection, copy.SupportByDirection, SupportByDirection.Length);
            Array.Copy(ReadCollapsedCounts, copy.ReadCollapsedCounts, ReadCollapsedCounts.Length);

            return copy;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1} {2}>{3}", Chromosome, Coordinate, Reference, Alternate);
        }
    }
}