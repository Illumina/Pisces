using System;
using System.Collections.Generic;
using Common.IO.Utility;
using ReadRealignmentLogic.Models;

namespace Gemini.BinSignalCollection
{
    public class UsableBins
    {
        private readonly IBinConclusions _binConclusions;
        private IBins<bool> _sitesUsable;
        private int _numBins;

        public UsableBins(IBinConclusions binConclusions)
        {
            _binConclusions = binConclusions;
            _numBins = binConclusions.NumBins;
            _sitesUsable = new SparseGroupedBoolBins(_numBins);
        }

        public bool IsPositionUsable(int position)
        {
            return _sitesUsable.GetHit(_binConclusions.GetBinId(position));
        }

        public void FinalizeConclusions(int binsToExtendTo)
        {
            for (int i = 0; i < _numBins; i++)
            {
                if (_binConclusions.GetIsMessyEnough(i) && _binConclusions.GetIndelRegionHit(i) && !_binConclusions.GetProbableTrueSnvRegion(i))
                {
                    SetSiteAndNeighborsToUsable(binsToExtendTo, i);
                }

            }
        }

        private void SetSiteAndNeighborsToUsable(int binsToExtendTo, int i)
        {
            _sitesUsable.AddHit(i);

            for (int j = 0; j < binsToExtendTo; j++)
            {
                var binIndex = i - j;
                if (binIndex >= 0)
                {
                    if (!_binConclusions.GetProbableTrueSnvRegion(binIndex))
                    {
                        _sitesUsable.AddHit(binIndex);
                    }
                }
                else
                {
                    break;
                }
            }

            for (int j = 0; j < binsToExtendTo; j++)
            {
                var binIndex = i + j;
                if (binIndex < _numBins)
                {
                    if (!_binConclusions.GetProbableTrueSnvRegion(binIndex))
                    {
                        _sitesUsable.AddHit(binIndex);
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }

    public class DummyBins<T> : IBins<T> { 
    
        private T _default = default(T);

        public bool IncrementHit(int i, int count)
        {
            return false;
        }

        public bool AddHit(int i)
        {
            return false;
        }

        public T GetHit(int i, bool strict = false)
        {
            return _default;
        }

        public void Merge(IBins<T> otherBins, int binOffset, int startBinInOther, int endBinInOther)
        {
        }
    }
    public class BinConclusions : IBinConclusions
    {
        private readonly IBinEvidence _binEvidence;
        private readonly bool _collectDepth;
        private IBins<bool> _indelRegions;
        private readonly IBins<bool> _probableTrueSnvRegions;
        private readonly IBins<bool> _isMessyEnough;
        private readonly IBins<bool> _fwdMessyStatus;
        private readonly IBins<bool> _revMessyStatus;
        private readonly IBins<bool> _mapqMessyStatus;


        public BinConclusions(IBinEvidence binEvidence, bool collectDepth, bool trackDirectionalMess = false, bool trackMapqMess = false)
        {
            _binEvidence = binEvidence;

            _collectDepth = collectDepth;
            var numBins = _binEvidence.NumBins;
            _isMessyEnough = new SparseGroupedBoolBins(numBins);
            _indelRegions = new SparseGroupedBoolBins(numBins);

            if (trackDirectionalMess)
            {
                _fwdMessyStatus = new SparseGroupedBoolBins(numBins);
                _revMessyStatus = new SparseGroupedBoolBins(numBins);
            }
            else
            {
                _fwdMessyStatus = new DummyBins<bool>();
                _revMessyStatus = new DummyBins<bool>();
            }

            if (trackMapqMess)
            {
                _mapqMessyStatus = new SparseGroupedBoolBins(numBins);
            }
            else
            {
                _mapqMessyStatus = new DummyBins<bool>();
            }

            //if (_avoidLikelySnvs)
            {
                _probableTrueSnvRegions = new SparseGroupedBoolBins(numBins, 10);
            }


        }

        public int NumBins
        {
            get { return _binEvidence.NumBins;}
        }

        public void ProcessRegions(int messySiteThreshold, double imperfectFreqThreshold, int regionDepthThreshold,
            double indelRegionFreqThreshold, int binsToExtendTo, float directionalMessThreshold)
        {
            for (int i = 0; i < _binEvidence.NumBins; i++)
            {
                if (_collectDepth && _binEvidence.GetAllHits(i) == 0)
                {
                    continue;
                }

                if (!_collectDepth && _binEvidence.GetMessyHit(i) == 0 && _binEvidence.GetIndelHit(i) == 0)
                {
                    continue;
                }

                var messyHit = _binEvidence.GetMessyHit(i);

                var messyRegionHit = _binEvidence.GetMessyHit(i);
                UpdateDirectionalMessStatus(directionalMessThreshold, messyRegionHit, i);

                UpdateMapqMessStatus(directionalMessThreshold, i, messyRegionHit);

                var isMessy = messyHit >= messySiteThreshold;

                var isProbableSnv = false;
                if (_collectDepth)
                {
                    var allHitsHit = (float)_binEvidence.GetAllHits(i);
                    var pctMessy = messyHit / allHitsHit;
                    var pctIndel = _binEvidence.GetIndelHit(i) / allHitsHit;

                    isMessy = pctMessy + pctIndel >= imperfectFreqThreshold && pctIndel >= indelRegionFreqThreshold &&
                              allHitsHit >= regionDepthThreshold;

                    isProbableSnv = false; // TODO need to reconsider the below.
                    //isProbableSnv = (_singleMismatchHits[i] / (float)_allHits[i]) > 0.35;
                }


                if (isMessy && !isProbableSnv)
                {
                    SetIsMessyEnoughForSiteAndNeighborsIfNotSnv(binsToExtendTo, i);
                }

                const int snvBinsToExtendTo = 3;
                if (isProbableSnv)
                {
                    //probableTrueSnvRegions[i] = true;
                    AddProbableSnvHit(i);
                    for (int j = 0; j < snvBinsToExtendTo; j++)
                    {
                        var binIndex = i - j;
                        if (!AddProbableSnvHit(binIndex))
                        {
                            break;
                        }
                        //if (binIndex >= 0)
                        //{
                        //    probableTrueSnvRegions[binIndex] = true;
                        //}
                        //else
                        //{
                        //    break;
                        //}
                    }

                    for (int j = 0; j < snvBinsToExtendTo; j++)
                    {
                        var binIndex = i + j;
                        if (!AddProbableSnvHit(binIndex))
                        {
                            break;
                        }
                        //if (binIndex < probableTrueSnvRegions.Length)
                        //{
                        //    probableTrueSnvRegions[binIndex] = true;
                        //}
                        //else
                        //{
                        //    break;
                        //}
                    }
                }
            }
        }

        private void SetIsMessyEnoughForSiteAndNeighborsIfNotSnv(int binsToExtendTo, int i)
        {
            _isMessyEnough.AddHit(i);
            for (int j = 0; j < binsToExtendTo; j++)
            {
                var binIndex = i - j;
                if (binIndex >= 0)
                {
                    if (!GetProbableTrueSnvRegion(binIndex))
                    {
                        _isMessyEnough.AddHit(binIndex);
                    }
                }
                else
                {
                    break;
                }
            }

            for (int j = 0; j < binsToExtendTo; j++)
            {
                var binIndex = i + j;
                if (!GetProbableTrueSnvRegion(binIndex))
                {
                    if (!_isMessyEnough.AddHit(binIndex))
                    {
                        break;
                    }
                }
            }
        }

        public int GetBinId(int position)
        {
            return _binEvidence.GetBinId(position);
        }

        public void AddIndelEvidence(List<HashableIndel> finalizedIndelsForChrom, int binsToExtendTo)
        {
            foreach (var indel in finalizedIndelsForChrom)
            {
                var bin = _binEvidence.GetBinId(indel.ReferencePosition);
                var succeeded = SetIndelRegionTrue(bin);

                if (!succeeded)
                {
                    throw new Exception(
                        $"Not able to add indel evidence for indel {indel.ReferencePosition} in bin {bin} (have {_binEvidence.NumBins} bins.");
                }

                for (int j = 0; j < binsToExtendTo; j++)
                {
                    var binIndex = bin - j;
                    if (binIndex >= 0)
                    {
                        var succeeded2 = SetIndelRegionTrue(binIndex);
                        if (!succeeded2)
                        {
                            Logger.WriteToLog(
                                $"Not able to add side indel evidence for indel {indel.ReferencePosition} in bin {binIndex} (orig bin {bin}) (have {_binEvidence.NumBins} bins.");
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                for (int j = 0; j < binsToExtendTo; j++)
                {
                    var binIndex = bin + j;
                    var succeeded2 = SetIndelRegionTrue(binIndex);
                    if (!succeeded2)
                    {
                        Logger.WriteToLog(
                            $"Not able to add side indel evidence for indel {indel.ReferencePosition} in bin {binIndex} (orig bin {bin}) (have {_binEvidence.NumBins} bins.");
                        break;
                    }
                }
            }
        }
        public bool GetFwdMessyStatus(int i)
        {
            return _fwdMessyStatus.GetHit(i);
        }

        public bool GetRevMessyStatus(int i)
        {
            return _revMessyStatus.GetHit(i);
        }

        public bool GetMapqMessyStatus(int i)
        {
            return _mapqMessyStatus.GetHit(i);
        }

        public void ResetIndelRegions()
        {
            _indelRegions = new SparseGroupedBoolBins(_binEvidence.NumBins);
        }
        public bool SetIndelRegionTrue(int i)
        {
            return _indelRegions.AddHit(i);
        }
        public bool GetIndelRegionHit(int i)
        {
            return _indelRegions.GetHit(i);
        }
        private bool GetProbableSnvHit(int i)
        {
            return _probableTrueSnvRegions.GetHit(i);
        }

        private bool AddProbableSnvHit(int i)
        {
            return _probableTrueSnvRegions.AddHit(i);
        }



        private void UpdateMapqMessStatus(float directionalMessThreshold, int i, int messyRegionHit)
        {
            if ((_binEvidence.GetMapqMessyHit(i) / (float) messyRegionHit) > directionalMessThreshold)
            {
                SetSiteAndNeighborsToMapqMessy(i);
            }
        }

        private void SetSiteAndNeighborsToMapqMessy(int i)
        {
            var x = _mapqMessyStatus;
            var toExtendTo = 1;

            AddHitForSiteAndNeighbors(i, x, toExtendTo);
        }

        private void AddHitForSiteAndNeighbors(int i, IBins<bool> x, int toExtendTo)
        {
            x.AddHit(i);
            for (int j = 0; j <= toExtendTo; j++)
            {
                var binIndex = i - j;
                if (binIndex >= 0)
                {
                    x.AddHit(binIndex);
                }
                else
                {
                    break;
                }
            }

            for (int j = 0; j <= toExtendTo; j++)
            {
                var binIndex = i + j;
                if (binIndex < _binEvidence.NumBins)
                {
                    x.AddHit(binIndex);
                }
                else
                {
                    break;
                }
            }
        }

        private void UpdateDirectionalMessStatus(float directionalMessThreshold, int messyRegionHit, int i)
        {
            if (messyRegionHit > 3)
            {
                if ((_binEvidence.GetForwardMessyRegionHit(i) / (float) messyRegionHit) > directionalMessThreshold)
                {
                    AddHitForSiteAndNeighbors(i, _fwdMessyStatus, 1);
                }

                if ((_binEvidence.GetReverseMessyRegionHit(i) / (float) messyRegionHit) > directionalMessThreshold)
                {
                    AddHitForSiteAndNeighbors(i, _revMessyStatus, 1);
                }
            }
        }

        public bool GetProbableTrueSnvRegion(int i)
        {
            return _probableTrueSnvRegions.GetHit(i);
        }

        public bool GetIsMessyEnough(int i)
        {
            return _isMessyEnough.GetHit(i);
        }
    }
}