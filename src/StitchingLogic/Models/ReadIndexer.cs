namespace StitchingLogic.Models
{
    public class ReadIndexer
    {
        public int Index { get; private set; }
        public char? BaseAtIndex { get; set; }
        public byte? QualityAtIndex { get; set; }

        public ReadIndexer(int index)
        {
            Index = index;
        }

        public void Increment()
        {
            Index++;
        }
    }
}