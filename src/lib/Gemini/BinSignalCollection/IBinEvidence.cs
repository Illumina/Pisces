using Gemini.ClassificationAndEvidenceCollection;

namespace Gemini.BinSignalCollection
{
    public interface IBinEvidence
    {
        int StartPosition { get; }
        int NumBins { get; }
        void CombineBinEvidence(IBinEvidence evidence, int binOffset = 0, int startBinInOther = 0, int endBinInOther = int.MaxValue);
        void AddAllHits(uint[] origHits);
        void AddMessEvidence(bool isMessy, PairResult pairResult, bool isIndel, bool isSingleMismatch, bool isForwardOnlyMessy, bool isReverseOnlyMessy, bool isMapqMessy);
        int GetBinId(int position);
        int GetBinStart(int binId);
        int GetForwardMessyRegionHit(int i);
        int GetIndelHit(int i);
        int GetMessyHit(int i);
        int GetReverseMessyRegionHit(int i);
        int GetSingleMismatchHit(int i);
        void IncrementHitForPosition(int i, int count);
        void IncrementIndelHitForPosition(int i, int count);
        void IncrementMessyHitForPosition(int i, int count);
        void IncrementSingleMismatchHitForPosition(int i, int count);
        void SetSingleMismatchHits(uint[] origSingleMismatchHits);
        int GetMapqMessyHit(int i);
        int GetAllHits(int i);
        bool AddHit(int i);
    }
}