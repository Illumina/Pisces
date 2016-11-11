using Alignment.Domain;

namespace Alignment.Logic
{
    public class ReadPairEvaluator
    {
        /// <summary>
        /// Whether a provided read pair should be treated the same way an incomplete read pair is. Incomplete read pairs always return null from TryPair, so they keep hanging around.
        /// </summary>
        /// <param name="pair"></param>
        /// <returns></returns>
        public virtual bool TreatReadPairAsIncomplete(ReadPair readPair)
        {
            return false;
        }
    }
}