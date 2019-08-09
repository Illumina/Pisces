using System;
using System.Linq;
using System.Collections.Generic;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
{
    public class CandidateAllele : BaseAllele
    {
        public int WellAnchoredSupport { get { return WellAnchoredSupportByDirection.Sum(); } }
        public int[] WellAnchoredSupportByDirection { get; set; }
        public int Support { get { return SupportByDirection.Sum(); } }
        public int[] SupportByDirection { get; set; }

        public bool OpenOnRight { get; set; }
        public bool OpenOnLeft { get; set; }
        public bool FullyAnchored { get { return !OpenOnLeft && !OpenOnRight; } }
        public bool IsKnown { get; set; }

        public float Frequency { get; set; }
		public bool IsForcedAllele { get; set; }

        public AmpliconCounts SupportByAmplicon { get; set; }

        public CandidateAllele(string chromosome, int coordinate, string reference, string alternate, AlleleCategory type)
        {
            Type = type;
            Chromosome = chromosome;
            ReferencePosition = coordinate;
            ReferenceAllele = reference;
            AlternateAllele = alternate;

            if (string.IsNullOrEmpty(chromosome))
                throw new ArgumentException("Chromosome is empty.");

            if (coordinate < 0)
                throw new ArgumentException(string.Format("Coordinate {0} is invalid.",coordinate));

            if (string.IsNullOrEmpty(reference))
                throw new ArgumentException("Reference is empty.");

            if (string.IsNullOrEmpty(alternate))
                throw new ArgumentException("Alternate is empty.");

            SupportByDirection = new int[Constants.NumDirectionTypes];
            WellAnchoredSupportByDirection = new int[Constants.NumDirectionTypes];
            ReadCollapsedCountsMut = new int[Constants.NumReadCollapsedTypes];
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
                   && otherVariant.ReferencePosition == ReferencePosition
                   && otherVariant.AlternateAllele.Equals(AlternateAllele)
                   && otherVariant.Type == Type
                   && otherVariant.Chromosome.Equals(Chromosome)
                   && otherVariant.ReferenceAllele.Equals(ReferenceAllele);    
        }


        public override int GetHashCode()
        {
            //method just to get rid of compiler warning
            throw new NotImplementedException();
        }
        public void AddSupport(CandidateAllele fromAllele)
        {
            for (var i = 0; i < SupportByDirection.Length; i++)
                SupportByDirection[i] += fromAllele.SupportByDirection[i];

            for (var i = 0; i < WellAnchoredSupportByDirection.Length; i++)
                WellAnchoredSupportByDirection[i] += fromAllele.WellAnchoredSupportByDirection[i];

            if (fromAllele.SupportByAmplicon.AmpliconNames != null && fromAllele.SupportByAmplicon.AmpliconNames.Length > 0)
            {
                if (SupportByAmplicon.AmpliconNames == null || SupportByAmplicon.AmpliconNames.Length == 0)
                {
                    SupportByAmplicon = AmpliconCounts.GetEmptyAmpliconCounts();
                }

                for (var i = 0; i < fromAllele.SupportByAmplicon.AmpliconNames.Length; i++)
                {
                    var fromAlleleCount = fromAllele.SupportByAmplicon.CountsForAmplicon[i];
                    var fromAlleleName = fromAllele.SupportByAmplicon.AmpliconNames[i];
                    var indexData = SupportByAmplicon.GetAmpliconNameIndex(fromAlleleName);

                    if (indexData.IndexForAmplicon > -1)
                        SupportByAmplicon.CountsForAmplicon[indexData.IndexForAmplicon] += fromAlleleCount;
                    else
                    {
                        SupportByAmplicon.AmpliconNames[indexData.NextOpenSlot] = fromAlleleName;
                        SupportByAmplicon.CountsForAmplicon[indexData.NextOpenSlot] += fromAlleleCount;
                    }

                }
            }
          
        }

        public CandidateAllele DeepCopy()
        {
            var copy = new CandidateAllele(Chromosome, ReferencePosition, ReferenceAllele, AlternateAllele, Type)
            {
                OpenOnLeft = OpenOnLeft,
                OpenOnRight = OpenOnRight,
                IsKnown = IsKnown
            };

            Array.Copy(SupportByDirection, copy.SupportByDirection, SupportByDirection.Length);
            Array.Copy(WellAnchoredSupportByDirection, copy.WellAnchoredSupportByDirection, WellAnchoredSupportByDirection.Length);
            Array.Copy(ReadCollapsedCountsMut, copy.ReadCollapsedCountsMut, ReadCollapsedCountsMut.Length);
            copy.SupportByAmplicon = SupportByAmplicon.Copy();

            return copy;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1} {2}>{3}", Chromosome, ReferencePosition, ReferenceAllele, AlternateAllele);
        }
    }
}