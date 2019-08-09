using System;
using System.Collections.Generic;

namespace Gemini.BinSignalCollection
{
    public abstract class SparseGroupedBins<T> : IBins<T>
    {
        private readonly int _numBins;
        private readonly int _groupSize;
        private readonly int _numGroups;
        protected List<T[]> Groups;
        private int?[] _groupedBinsIndexes;

        public SparseGroupedBins(int numBins, int groupSize = 100)
        {
            _numBins = numBins;
            _groupSize = groupSize;
            Groups = new List<T[]>();
            _numGroups = (numBins / groupSize) + 1;
            _groupedBinsIndexes = new int?[_numGroups];
        }

        protected abstract void AddHitInternal(T[] group, int indexWithinGroup);

        public bool IncrementHit(int i, int count)
        {
            if (i >= _numBins || i < 0)
            {
                return false;
            }

            var groupIndex = i / _groupSize;
            var groupedBinIndex = _groupedBinsIndexes[groupIndex];
            var indexWithinGroup = i % _groupSize;
            if (groupedBinIndex >= 0)
            {
                var group = Groups[groupedBinIndex.Value];
                for (int j = 0; j < count; j++)
                {
                    AddHitInternal(group, indexWithinGroup);
                }
            }
            else
            {
                var newGroupIndex = Groups.Count;
                Groups.Add(new T[_groupSize]);
                _groupedBinsIndexes[groupIndex] = newGroupIndex;

                for (int j = 0; j < count; j++)
                {
                    AddHitInternal(Groups[newGroupIndex], indexWithinGroup);

                }
            }
            return true;
        }

        public bool AddHit(int i)
        {
            if (i >= _numBins || i < 0)
            {
                return false;
            }

            var groupIndex = i / _groupSize;
            var groupedBinIndex = _groupedBinsIndexes[groupIndex];
            var indexWithinGroup = i % _groupSize;
            if (groupedBinIndex >= 0)
            {
                var group = Groups[groupedBinIndex.Value];
                AddHitInternal(group, indexWithinGroup);
            }
            else
            {
                var newGroupIndex = Groups.Count;
                Groups.Add(new T[_groupSize]);
                _groupedBinsIndexes[groupIndex] = newGroupIndex;
                AddHitInternal(Groups[newGroupIndex], indexWithinGroup);
            }
            return true;
        }

        private T[] GetGroup(int i, bool strict = false)
        {
            if (i >= _numBins || i < 0)
            {
                if (strict)
                {
                    throw new ArgumentException($"Requested bin number is outside of range ({i} vs {_numBins}).");
                }
                return null;
            }
            var groupIndex = i / _groupSize;
            if (groupIndex >= _groupedBinsIndexes.Length || groupIndex < 0)
            {
                if (strict)
                {
                    throw new ArgumentException($"Requested bin number is outside of range ({i} vs {_numBins}).");
                }
                return null;
            }
            var groupedBinIndex = _groupedBinsIndexes[groupIndex];
            if (groupedBinIndex >= 0 && groupedBinIndex < Groups.Count)
            {
                var group = Groups[groupedBinIndex.Value];
                return group;
            }

            if (strict)
            {
                throw new ArgumentException($"Requested bin number is outside of range ({i} vs {_numBins}).");
            }
            return null;

        }

        protected abstract T ReturnDefault();

        public T GetHit(int i, bool strict = false)
        {
            var group = GetGroup(i, strict);
            if (group == null)
            {
                return ReturnDefault();
            }
            else
            {
                var indexWithinGroup = i % _groupSize;
                return group[indexWithinGroup];
            }
        }

        /// <summary>
        /// Increment the hits of this bin with the hits from otherBins. Assumes they are on the same scale,
        /// and will not go past the current bins. If offset not provided, also assumes start positions are the same.
        /// </summary>
        /// <param name="otherBins"></param>
        /// <param name="binOffset"></param>
        public void Merge(IBins<T> otherBins, int binOffset, int startBinInOther, int endBinInOther)
        {
            var defaultResult = ReturnDefault();
            var startBinInThis = startBinInOther - binOffset;
            var endBinInThis = Math.Min(_numBins, endBinInOther - binOffset);
            for (int i = startBinInThis; i <= endBinInThis; i++)
            {
                var binIndexInOther = i + binOffset;

                // Note, this keeps checking even if we've gone past the range of the other guy
                var otherHit = otherBins.GetHit(binIndexInOther);
                if (!otherHit.Equals(defaultResult))
                {
                    MergeHits(i, otherHit);
                }
            }
        }

        protected abstract void MergeHits(int i, T otherHit);
    }
}