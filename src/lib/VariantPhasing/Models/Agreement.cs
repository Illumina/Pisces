using System;

namespace VariantPhasing.Models
{
    public class Agreement : IComparable<Agreement>
    {
        public int NumAgreement;
        public int NumDisagreement;
        public int Score { get { return NumAgreement - NumDisagreement; }}

        public Agreement()
        {
            NumAgreement = 0;
            NumDisagreement = 0;
        }

        public Agreement(VeadGroup vg1, VeadGroup vg2) : this()
        {
            var read1 = vg1.RepresentativeVead;
            var read2 = vg2.RepresentativeVead;
            for (var i = 0; i < read1.SiteResults.Length; i++)
            {
                var vs1 = read1.SiteResults[i];
                var vs2 = read2.SiteResults[i];

                if ((vs1.VcfAlternateAllele == "N") || (vs2.VcfAlternateAllele == "N"))
                    continue;

                if ((vs1.VcfAlternateAllele == vs2.VcfAlternateAllele) && (vs1.VcfReferenceAllele == vs2.VcfReferenceAllele))
                    NumAgreement++;

                else
                    NumDisagreement++;
            }
            
        }

        public int CompareTo(Agreement other)
        {
            return (Score.CompareTo(other.Score));
        }

        public void AddAgreement(Agreement a2)
        {
            NumAgreement += a2.NumAgreement;
            NumDisagreement += a2.NumDisagreement;
        }
    }

}
