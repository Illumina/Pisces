using System;
using System.Collections.Generic;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;

namespace Pisces.Processing.Interfaces
{
    public interface IStateManager : IAlleleSource 
    {
        void AddAlleleCounts(Read read);

        ICandidateBatch GetCandidatesToProcess(int? upToPosition, ChrReference chrReference = null, HashSet<Tuple<string, int, string, string>> forcesGtAlleles = null);

        void DoneProcessing(ICandidateBatch batch);
    }
}

