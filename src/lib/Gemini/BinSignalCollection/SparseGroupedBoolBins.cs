namespace Gemini.BinSignalCollection
{
    public class SparseGroupedBoolBins : SparseGroupedBins<bool>
    {
        public SparseGroupedBoolBins(int numBins, int groupSize = 100) : base(numBins, groupSize)
        {
        }

        protected override void AddHitInternal(bool[] group, int indexWithinGroup)
        {
            group[indexWithinGroup] = true;
        }

        protected override bool ReturnDefault()
        {
            return false;
        }

        protected override void MergeHits(int i, bool otherHit)
        {
            if (otherHit == true)
            {
                IncrementHit(i, 1);
            }
        }
    }
}