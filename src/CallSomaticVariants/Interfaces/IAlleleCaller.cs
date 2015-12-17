using System.Collections.Generic;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;

namespace CallSomaticVariants.Interfaces
{
    public interface IAlleleCaller
    {
        IEnumerable<BaseCalledAllele> Call(ICandidateBatch batchToCall, IStateManager source);
    }
}
