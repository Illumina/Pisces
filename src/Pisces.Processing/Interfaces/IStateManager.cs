using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;

namespace Pisces.Processing.Interfaces
{
    public interface IStateManager : IAlleleSource 
    {
        void AddAlleleCounts(AlignmentSet alignmentSet);
        void AddAlleleCounts(Read read);

        ICandidateBatch GetCandidatesToProcess(int? upToPosition, ChrReference chrReference = null);

        void DoneProcessing(ICandidateBatch batch);
    }
}

