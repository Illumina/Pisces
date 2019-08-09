namespace Gemini.BinSignalCollection
{
    public interface IBins<T>
    {
        bool IncrementHit(int i, int count);

        bool AddHit(int i);
        T GetHit(int i, bool strict = false);
        void Merge(IBins<T> otherBins, int binOffset, int startBinInOther, int endBinInOther);
    }
}