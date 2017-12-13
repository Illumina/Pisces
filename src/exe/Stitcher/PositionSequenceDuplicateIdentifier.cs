using System.Collections.Generic;
using Alignment.Domain.Sequencing;

namespace Stitcher
{
    public class PositionSequenceDuplicateIdentifier : IDuplicateIdentifier
    {
        private readonly Dictionary<string, HashSet<string>> _positionSequences;
        public PositionSequenceDuplicateIdentifier()
        {
            _positionSequences = new Dictionary<string, HashSet<string>>();
        }

        public bool IsDuplicate(BamAlignment alignment)
        {
            // Note: a potential issue (if we disqualify pair if *either* mate is dup) is if we have 2 pairs that are dups 
            // (call them Pair1 & Pair2), and they come in this order in the BAM: 
            // Pair1.Read1, Pair2.Read1, Pair2.Read2, Pair1.Read2. 
            // In that case, both pairs would have been eliminated (Pair2.Read1 is a dup, so Pair2 is blacklisted, and Pair1.Read2 is a dup, so Pair1 is blacklisted).
            // But actually -- if Pair2 was already blacklisted, we would not have encountered Pair2.Read2...

            var positionKey = alignment.RefID + ":" + alignment.Position;
            if (!_positionSequences.ContainsKey(positionKey))
            {
                _positionSequences.Add(positionKey, new HashSet<string>());
            }

            if (!_positionSequences[positionKey].Contains(alignment.CigarData + ":" + alignment.Bases))
            {
                _positionSequences[positionKey].Add(alignment.CigarData + ":" + alignment.Bases);
                return false;
            }

            return true;
        }
    }
}