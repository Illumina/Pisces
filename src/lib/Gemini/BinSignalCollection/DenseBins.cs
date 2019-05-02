using System;

namespace Gemini.BinSignalCollection
{
    public class DenseBins : IBins<int>
    {
        private readonly int[] _binArray;
        private readonly int _numBins;
        public DenseBins(int numBins)
        {
            _binArray = new int[numBins];
            _numBins = numBins;
        }

        public bool IncrementHit(int i, int count)
        {
            if (i >= 0 && i < _numBins)
            {
                _binArray[i]+= count;
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool AddHit(int i)
        {
            if (i >= 0 && i < _numBins)
            {
                _binArray[i]++;
                return true;
            }
            else
            {
                return false;
            }
        }

        public int GetHit(int i, bool strict = false)
        {
            if (i < _numBins && i >= 0)
            {
                return _binArray[i];
            }
            else
            {
                if (strict)
                {
                    throw new ArgumentException($"Requested bin number is outside of range ({i} vs {_numBins}).");
                }
                else
                {
                    return 0;
                }
            }
        }

        public void Merge(IBins<int> otherBins, int binOffset, int startBinInOther, int endBinInOther)
        {
            var startBinInThis = startBinInOther - binOffset;
            var endBinInThis = Math.Min(_numBins, endBinInOther - binOffset);

            for (int i = startBinInThis; i <= endBinInThis; i++)
            {
                var binIdInOtherBins = i + binOffset;

                // Note, this keeps checking even if we've gone past the range of the other guy
                var otherHit = otherBins.GetHit(binIdInOtherBins, false);
                if (otherHit > 0)
                {
                    IncrementHit(i, otherHit);
                }
            }
        }
    }
}