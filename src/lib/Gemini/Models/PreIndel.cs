using Pisces.Domain.Models.Alleles;

namespace Gemini.Models
{
    public class PreIndel : CandidateAllele
    {
        public uint LeftAnchor;
        public uint RightAnchor;
        public int Mess;
        public int Score;
        public int AverageQualityRounded;
        public bool InMulti;
        public string OtherIndel;

        public PreIndel(CandidateAllele baseCandidateAllele)
            : base(baseCandidateAllele.Chromosome, baseCandidateAllele.ReferencePosition, baseCandidateAllele.ReferenceAllele, baseCandidateAllele.AlternateAllele, baseCandidateAllele.Type)
        {
            SupportByDirection = baseCandidateAllele.SupportByDirection;
            IsKnown = baseCandidateAllele.IsKnown;
        }
    }
}