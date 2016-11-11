namespace StitchingLogic
{
    public class StitchedPosition
    {
        public UnmappedStretch UnmappedPrefix = new UnmappedStretch();
        public StitchedSite MappedSite = new StitchedSite();

        public void Reset()
        {
            UnmappedPrefix.Reset();
            MappedSite.Reset();
        }
    }
}