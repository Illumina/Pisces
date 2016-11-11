namespace Stitcher
{
    public interface IStitcherProcessor
    {
        void Process(string inputBam, string outFolder, StitcherOptions stitcherOptions);
    }
}