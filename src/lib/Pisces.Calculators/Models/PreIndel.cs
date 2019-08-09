using Pisces.Domain.Models.Alleles;

namespace Gemini.Models
{
    public class PreIndel : CandidateAllele
    {
        public int LeftAnchor;
        public int RightAnchor;
        public int Mess;
        public int Score;
        public int AverageQualityRounded;
        public bool InMulti;
        public string OtherIndel;
        public int Observations;
        public bool FromSoftclip;

        public PreIndel(CandidateAllele baseCandidateAllele)
            : base(baseCandidateAllele.Chromosome, baseCandidateAllele.ReferencePosition, baseCandidateAllele.ReferenceAllele, baseCandidateAllele.AlternateAllele, baseCandidateAllele.Type)
        {
            SupportByDirection = baseCandidateAllele.SupportByDirection;
            IsKnown = baseCandidateAllele.IsKnown;
        }
    }
}