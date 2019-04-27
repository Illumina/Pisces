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
        public bool IsPassing;

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

        /// <summary>
        /// GB: So AC > AC is not reference? 
        /// TJD: Thats one of the reasons we trim what comes in. We really need to disambiguate these cases.
        /// I believe we never (internally to pisces) pass around an AC -> AC allele.
        /// Although it might come in to pisces from some outside source, and then we would consider it as an MNV
        /// Scylla will emit an AC -> AC call in
        /// very specific scenarios.
        /// </summary>
        public bool IsReference
        {
            get { return ((VcfReferenceAllele == VcfAlternateAllele) && VcfAlternateAllele.Length==1); }
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
            VcfReferencePosition = variant.ReferencePosition;
            VcfReferenceAllele = variant.ReferenceAllele;
            VcfAlternateAllele = variant.AlternateAllele;
            OriginalAlleleFromVcf = variant;
            IsPassing = IsAcceptablePhasingCandidate(variant);
        }

        private bool IsAcceptablePhasingCandidate(CalledAllele allele)
        {
            var filters = allele.Filters;

            return (filters.Count == 0);

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
                IsPassing = IsPassing
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

        public SubsequenceType GetVariantType()
        {
            var nextVariantType = SubsequenceType.MatchOrMismatchSequence; //lumpSNP and PhasedSNP together for this.
            if (VcfReferenceAllele.Length > VcfAlternateAllele.Length)
                nextVariantType = SubsequenceType.DeletionSequence;
            else if (VcfReferenceAllele.Length < VcfAlternateAllele.Length)
                nextVariantType = SubsequenceType.InsertionSquence;

            return nextVariantType;
        }

        int IComparable<VariantSite>.CompareTo(VariantSite vs)
        {
            //order by true first base.
            if (TrueFirstBaseOfDiff < vs.TrueFirstBaseOfDiff)
                return -1;
            if (TrueFirstBaseOfDiff > vs.TrueFirstBaseOfDiff)
                return 1;

            /*
            //always search for the longest alt allele first
            // (if we are looking for VS  "ACT-> TTT" or "AC-> TT" in a read,
            // we should check the alternate allele TTTTT for TTT before we look for TT.)
           

            if (TrueAltAllele.Length > vs.TrueAltAllele.Length)
                return -1;
            if (TrueAltAllele.Length < vs.TrueAltAllele.Length)
                return 1;
                */
            return 0;
        }
    }


}
