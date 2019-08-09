using System;
using Gemini.ClassificationAndEvidenceCollection;

namespace Gemini.BinSignalCollection
{
    public class BinEvidence : IBinEvidence
    {
        private readonly int _refId;
        private readonly bool _collectDepth;
        private readonly bool _avoidLikelySnvs; // use this when adding back single mismatch feature
        private readonly int _siteWidth;
        private readonly int _regionStart;
        //private uint[] _singleMismatchHits; // un-comment when adding back single mismatch feature
        protected IBins<int> _indelHits;
        protected IBins<int> _messyHits;
        protected IBins<int> _fwdOnlyMessyHits;
        protected IBins<int> _revOnlyMessyHits;
        protected IBins<int> _mapqMessyHits;
        protected IBins<int> _singleMismatchHits;

        public BinEvidence(int refId, bool collectDepth, int numBins, bool avoidLikelySnvs, int siteWidth, int regionStart, bool trackDirectionalMess = false, bool trackMapqMess = false)
        {
            _refId = refId;
            _collectDepth = collectDepth;
            NumBins = numBins;
            _avoidLikelySnvs = avoidLikelySnvs;
            _siteWidth = siteWidth;
            _regionStart = regionStart;

            // The below constants are in place to set a balance between opening up too-large groups for truly sparse data, and opening up too many small groups for globally sparse but locally dense data
            // The values of the constants themselves are not really scientific and could be further honed. There is no anlaytical consequence, just performance.
            const int groupSizeForCommonlyHitCategory = 500;
            const int groupSizeForSparselyHitCategory = 50;
            _messyHits = new SparseGroupedIntBins(NumBins, groupSizeForCommonlyHitCategory);
            _indelHits = new SparseGroupedIntBins(NumBins, groupSizeForSparselyHitCategory);

            if (trackDirectionalMess)
            {
                _fwdOnlyMessyHits = new SparseGroupedIntBins(NumBins, groupSizeForSparselyHitCategory);
                _revOnlyMessyHits = new SparseGroupedIntBins(NumBins, groupSizeForSparselyHitCategory);
            }
            else
            {
                _fwdOnlyMessyHits = new DummyBins<int>();
                _revOnlyMessyHits = new DummyBins<int>();
            }

            if (trackMapqMess)
            {
                _mapqMessyHits = new SparseGroupedIntBins(NumBins, groupSizeForSparselyHitCategory);
            }
            else
            {
                _mapqMessyHits = new DummyBins<int>();
            }

            AllHits = new DenseBins(NumBins);
            _singleMismatchHits = new SparseGroupedIntBins(NumBins, groupSizeForCommonlyHitCategory);
            //_singleMismatchHits = new DenseBins(NumBins); // TODO reinstate when we add single mismatch feature back
            StartPosition = regionStart;

        }

        public void CombineBinEvidence(IBinEvidence evidence, int binOffset = 0, int startBinInOther = 0, int endBinInOther = int.MaxValue)
        {
            var binEvidence = evidence as BinEvidence;
            if (binEvidence == null)
            {
                throw new ArgumentException($"Not able to combine bin evidence between two different types.");
            }
            _indelHits.Merge(binEvidence._indelHits, binOffset, startBinInOther, endBinInOther);
            _messyHits.Merge(binEvidence._messyHits, binOffset, startBinInOther, endBinInOther);
            _singleMismatchHits.Merge(binEvidence._singleMismatchHits, binOffset, startBinInOther, endBinInOther);
            AllHits.Merge(binEvidence.AllHits, binOffset, startBinInOther, endBinInOther);
            _revOnlyMessyHits.Merge(binEvidence._revOnlyMessyHits, binOffset, startBinInOther, endBinInOther);
            _mapqMessyHits.Merge(binEvidence._mapqMessyHits, binOffset, startBinInOther, endBinInOther);
            _fwdOnlyMessyHits.Merge(binEvidence._fwdOnlyMessyHits, binOffset, startBinInOther, endBinInOther);
        }


        private IBins<int> AllHits;
        public int StartPosition { get; }
        public int NumBins { get; }

        public int GetBinId(int position)
        {
            return (position - _regionStart) / _siteWidth;
        }

        public int GetBinStart(int binId)
        {
            var binStart = _regionStart + (binId * _siteWidth);
            return binStart;
        }

        public void AddMessEvidence(bool isMessy, PairResult pairResult, bool isIndel, bool isSingleMismatch, bool isForwardOnlyMessy, bool isReverseOnlyMessy, bool isMapqMessy)
        {
            if (!_collectDepth && !isMessy && !isIndel && !isForwardOnlyMessy && !isMapqMessy && !isReverseOnlyMessy && !isSingleMismatch)
            {
                return;
            }

            foreach (var aln in pairResult.Alignments)
            {
                if (aln.RefID != _refId)
                {
                    continue;
                }

                var lastBinSpannedByRead = GetBinId(aln.EndPosition);
                var firstBin = GetBinId(aln.Position);

                if (lastBinSpannedByRead + 1 > NumBins - 1)
                {
                    Console.WriteLine("LAST BIN SPANNED IS GREATER THAN ALLHITS!!!");
                }

                for (int i = firstBin; i <= Math.Min(lastBinSpannedByRead, NumBins - 1); i++)
                {
                    try
                    {
                        AllHits.AddHit(i);

                        if (isMessy)
                        {
                            AddMessyHit(i);
                            if (isForwardOnlyMessy)
                            {
                                _fwdOnlyMessyHits.AddHit(i);
                            }
                            else if (isReverseOnlyMessy)
                            {
                                _revOnlyMessyHits.AddHit(i);
                            }
                            else if (isMapqMessy)
                            {
                                _mapqMessyHits.AddHit(i);
                            }

                        }

                        if (isIndel)
                        {
                            AddIndelHit(i);
                        }

                        if (isSingleMismatch)
                        {
                            AddSingleMismatchHit(i);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"FAILED ON {i} ({aln.Position}, {_regionStart})..." + e.Message);
                        throw new Exception($"FAILED ON {i} ({aln.Position}, {_regionStart})", e);
                    }
                }
            }
        }


        public int GetForwardMessyRegionHit(int i)
        {
            return _fwdOnlyMessyHits.GetHit(i);
        }
        public int GetReverseMessyRegionHit(int i)
        {
            return _revOnlyMessyHits.GetHit(i);
        }


        private void AddSingleMismatchHit(int i)
        {
            _singleMismatchHits.AddHit(i);
        }

        public void IncrementSingleMismatchHitForPosition(int i, int count)
        {
            var binId = (GetBinId(i));
            _singleMismatchHits.IncrementHit(binId, count);
        }

        public void IncrementHitForPosition(int i, int count)
        {
            var binId = (GetBinId(i));
            AllHits.IncrementHit(binId, count);
        }

        public void IncrementIndelHitForPosition(int i, int count)
        {
            var binId = (GetBinId(i));
            for (int j = 0; j < count; j++)
            {
                _indelHits.AddHit(binId);
            }
        }

        public void IncrementMessyHitForPosition(int i, int count)
        {
            var binId = (GetBinId(i));
            for (int j = 0; j < count; j++)
            {
                _messyHits.AddHit(binId);
            }
        }

  

        private void AddIndelHit(int i)
        {
            _indelHits.AddHit(i);
        }

        private void AddMessyHit(int i)
        {
            _messyHits.AddHit(i);
        }

        public int GetMessyHit(int i)
        {
            return _messyHits.GetHit(i);
        }

        public void SetSingleMismatchHits(uint[] origSingleMismatchHits)
        {
            // TODO un-comment when adding back single mismatch feature
            //_singleMismatchHits = origSingleMismatchHits;
        }

        /// <summary>
        /// Given an input hit array, add the hits to the existing AllHits.
        /// </summary>
        /// <param name="origHits"></param>
        public void AddAllHits(uint[] origHits)
        {
            for (int i = 0; i < origHits.Length; i++)
            {
                AllHits.IncrementHit(i, (int)origHits[i]);
            }
        }

        public int GetSingleMismatchHit(int i)
        {
            // TODO un-comment when adding back single mismatch feature
            // TODO fix
            //if (i >= 0 && i <= _singleMismatchHits.Length)
            //{
            //    return _singleMismatchHits[i];
            //}

            return 0;
        }

        public int GetIndelHit(int i)
        {
            return _indelHits.GetHit(i);
        }

        public int GetMapqMessyHit(int i)
        {
            return _mapqMessyHits.GetHit(i);
        }
   

        public int GetAllHits(int i)
        {
            return AllHits.GetHit(i);
        }

        public bool AddHit(int i)
        {
            return AllHits.AddHit(i);
        }

    }
}