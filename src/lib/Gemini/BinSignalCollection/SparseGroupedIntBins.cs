namespace Gemini.BinSignalCollection
{
    public class SparseGroupedIntBins : SparseGroupedBins<int>
    {
        public SparseGroupedIntBins(int numBins, int groupSize = 100) : base(numBins, groupSize)
        {
        }

        protected override void AddHitInternal(int[] group, int indexWithinGroup)
        {
            group[indexWithinGroup]++;
        }

        protected override int ReturnDefault()
        {
            return 0;
        }

        protected override void MergeHits(int i, int otherHit)
        {
            IncrementHit(i, otherHit);
        }
    }
}