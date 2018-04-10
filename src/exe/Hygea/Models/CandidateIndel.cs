using System.Collections.Generic;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace RealignIndels.Models
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
