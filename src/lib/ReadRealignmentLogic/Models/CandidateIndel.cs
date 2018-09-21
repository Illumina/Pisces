using Pisces.Domain.Models.Alleles;

namespace ReadRealignmentLogic.Models
{
    public class CandidateIndel : CandidateAllele
    {

        public CandidateIndel(CandidateAllele baseCandidateAllele)
            : base(baseCandidateAllele.Chromosome, baseCandidateAllele.ReferencePosition, baseCandidateAllele.ReferenceAllele, baseCandidateAllele.AlternateAllele, baseCandidateAllele.Type)
        {
            SupportByDirection = baseCandidateAllele.SupportByDirection;
            IsKnown = baseCandidateAllele.IsKnown;
        }
    }
}
