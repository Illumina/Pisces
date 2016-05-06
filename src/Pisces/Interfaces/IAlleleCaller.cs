using System.Collections.Generic;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Processing.Interfaces;

namespace Pisces.Interfaces
{
    public interface IAlleleCaller
    {
        SortedList<int, List<BaseCalledAllele>> Call(ICandidateBatch batchToCall, IAlleleSource source);
        int TotalNumCollapsed { get; }
        int TotalNumCalled { get; }
    }
}
