namespace Gemini.Utility
{
    public class IndelFilteringOptions
    {
        public int FoundThreshold = 3;
        public uint MinAnchor = 1;
        public int BinSize = 0;
        public bool RemoveLowQualityVariantsWithinBin
        {
            get { return BinSize > 0; }
        }

        public int StrictFoundThreshold = 0;
        public int StrictAnchorThreshold = 0;
        public int MaxMess = 20;

    }
}