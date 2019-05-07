using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;

namespace Alignment.Logic
{
    public interface IAlignmentPairFilter
    {
        bool ReachedFlushingCheckpoint(BamAlignment bamAlignment);
        IEnumerable<BamAlignment> GetFlushableUnpairedReads();
        ReadPair TryPair(BamAlignment bamAlignment, PairStatus pairStatus = PairStatus.Unknown);
        IEnumerable<BamAlignment> GetUnpairedAlignments(bool b);
        bool ReadIsBlacklisted(BamAlignment bamAlignment);
        IEnumerable<ReadPair> GetFlushableUnpairedPairs(int upToPosition = -1);
    }
}