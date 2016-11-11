using System;
using System.IO;
using System.Text;
using Pisces.Domain.Models.Alleles;
using VariantPhasing.Types;

namespace VariantPhasing.Models
{
    public class VariantSite : IComparable<VariantSite>
    {
        public string VcfReferenceAllele;
        public string VcfAlternateAllele;
        public string ReferenceName;
        public int VcfReferencePosition; //1-based
        public CalledAllele OriginalAlleleFromVcf;

        public int TrueFirstBaseOfDiff
        {
            get { return IsInsertionOrDeletion() ? VcfReferencePosition + 1 : VcfReferencePosition; }
        }

        public string TrueRefAllele
        {
            get { return IsInsertionOrDeletion() ? VcfReferenceAllele.Substring(1) : VcfReferenceAllele; }
        }

        public string TrueAltAllele
        {
            get { return IsInsertionOrDeletion() ? VcfAlternateAllele.Substring(1) : VcfAlternateAllele; }
        }

        public bool HasRefAndAltData
        {
            get { return (HasRefData() && HasAltData()); }
        }

        public bool HasNoData
        {
            get { return ((VcfReferenceAllele == "N") && (VcfAlternateAllele == "N")); }
        }

        public VariantSite() { }

        public VariantSite(int refPosition)
        {
            VcfReferencePosition = refPosition;
            VcfAlternateAllele = "N";
            VcfReferenceAllele = "N";
        }


        public VariantSite(CalledAllele variant)
        {
            ReferenceName = variant.Chromosome;
            VcfReferencePosition = variant.Coordinate;
            VcfReferenceAllele = variant.Reference;
            VcfAlternateAllele = variant.Alternate;
            OriginalAlleleFromVcf = variant;
        }

        private bool IsInsertionOrDeletion()
        {
            return (VcfReferenceAllele.Length != VcfAlternateAllele.Length);
        }

        public bool HasRefData()
        {
            return (VcfReferenceAllele != "N");
        }

        public bool HasAltData()
        {
            return (VcfAlternateAllele != "N");
        }

        public override string ToString()
        {
            return VcfReferenceAllele + ">" + VcfAlternateAllele;
        }

        public VariantSite DeepCopy()
        {
            var vs = new VariantSite
            {
                VcfReferenceAllele = VcfReferenceAllele,
                VcfAlternateAllele = VcfAlternateAllele,
                ReferenceName = ReferenceName,
                VcfReferencePosition = VcfReferencePosition,
                OriginalAlleleFromVcf = OriginalAlleleFromVcf,
            };
            return vs;
        }

        public static string ArrayToPositions(VariantSite[] vss)
        {
            var sb = new StringBuilder();
            foreach (var t in vss)
            {
                sb.Append(t.VcfReferencePosition + " ");
            }
            return sb.ToString();
        }

        public static string ArrayToString(VariantSite[] vss)
        {
            var sb = new StringBuilder();
            foreach (var t in vss)
            {
                sb.Append(t.ToString() + " ");
            }
            return sb.ToString();
        }

        public SomaticVariantType GetVariantType()
        {
            var nextVariantType = SomaticVariantType.SNP; //lumpSNP and PhasedSNP together for this.
            if (VcfReferenceAllele.Length > VcfAlternateAllele.Length)
                nextVariantType = SomaticVariantType.Deletion;
            else if (VcfReferenceAllele.Length < VcfAlternateAllele.Length)
                nextVariantType = SomaticVariantType.Insertion;

            return nextVariantType;
        }

        int IComparable<VariantSite>.CompareTo(VariantSite vs)
        {
            if (TrueFirstBaseOfDiff < vs.TrueFirstBaseOfDiff)
                return -1;
            if (TrueFirstBaseOfDiff > vs.TrueFirstBaseOfDiff)
                return 1;
            return 0;
        }
    }


}
