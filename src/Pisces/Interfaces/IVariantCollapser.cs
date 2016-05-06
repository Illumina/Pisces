using System;
using System.Collections.Generic;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Interfaces
{
    public interface IVariantCollapser
    {
        List<CandidateAllele> Collapse(List<CandidateAllele> candidates, IAlleleSource source, int? maxClearedPosition);
        int TotalNumCollapsed { get; }
    }
}
