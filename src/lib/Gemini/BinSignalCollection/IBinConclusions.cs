using System.Collections.Generic;
using ReadRealignmentLogic.Models;

namespace Gemini.BinSignalCollection
{
    public interface IBinConclusions
    {
        int NumBins { get; }

        void AddIndelEvidence(List<HashableIndel> finalizedIndelsForChrom, int binsToExtendTo);
        int GetBinId(int position);
        bool GetFwdMessyStatus(int i);
        bool GetIndelRegionHit(int i);
        bool GetIsMessyEnough(int i);
        bool GetMapqMessyStatus(int i);
        bool GetProbableTrueSnvRegion(int i);
        bool GetRevMessyStatus(int i);
        void ProcessRegions(int messySiteThreshold, double imperfectFreqThreshold, int regionDepthThreshold, double indelRegionFreqThreshold, int binsToExtendTo, float directionalMessThreshold);
        void ResetIndelRegions();
        bool SetIndelRegionTrue(int i);
    }
}