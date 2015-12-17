using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Logic.RegionState
{
    public class RegionStateManager : IStateManager
    {
        private readonly Dictionary<int, RegionState> _regionLookup = new Dictionary<int, RegionState>();
        private readonly int _regionSize = 1000;
        private int _minBasecallQuality;
        private RegionState _lastAccessedBlock;
        private Stack<RegionState> _reusableBlocks = new Stack<RegionState>();
        private int _lastUpToKey;
        private bool _includeRefAlleles;
        private ChrIntervalSet _intervalSet;

        public RegionStateManager(bool includeRefAlleles = false, int minBasecallQuality = 20, ChrIntervalSet intervalSet = null, int blockSize = Constants.RegionSize)
        {
            _regionSize = blockSize;
            _minBasecallQuality = minBasecallQuality;
            _includeRefAlleles = includeRefAlleles;
            _intervalSet = intervalSet;
        }

        public void AddCandidates(IEnumerable<CandidateAllele> candidateVariants)
        {
            foreach (var candidateVariant in candidateVariants)
            {
                var block = GetBlock(candidateVariant.Coordinate);
                block.AddCandidate(candidateVariant);
            }
        }

        public void AddGappedMnvRefCount(Dictionary<int, int> supportLookup)
        {
            foreach (var position in supportLookup.Keys)
            {
                var block = GetBlock(position);
                block.AddGappedMnvRefCount(position, supportLookup[position]);
            }
        }

        public void AddAlleleCounts(AlignmentSet alignmentSet)
        {
            foreach (var alignment in alignmentSet.ReadsForProcessing)
            {
                AddAlleleCounts(alignment);
            }
        }

        private void AddAlleleCounts(Read alignment)
        {
            var lastPosition = alignment.Position - 1;

            var deletionLength = 0;
            var lengthBeforeDeletion = alignment.ReadLength;
            var endsInDeletion = alignment.CigarData.HasOperationAtOpIndex(1,'D',true);
            var endsInDeletionBeforeSoftclip = alignment.CigarData.HasOperationAtOpIndex(2,'D',true) && alignment.CigarData.HasOperationAtOpIndex(1,'S',true);

            if (endsInDeletion || endsInDeletionBeforeSoftclip)
            {
                deletionLength = (int) (endsInDeletionBeforeSoftclip ? alignment.CigarData[alignment.CigarData.Count -2].Length :
                    alignment.CigarData[alignment.CigarData.Count - 1].Length);
                lengthBeforeDeletion = (int) (endsInDeletionBeforeSoftclip ? alignment.ReadLength - alignment.CigarData[alignment.CigarData.Count -1].Length :  alignment.ReadLength);
            }

            for (var i = 0; i < alignment.PositionMap.Length; i++)
            {
                if ((endsInDeletionBeforeSoftclip) && i==lengthBeforeDeletion)
                {
                    for (var j = 1; j < deletionLength + 1; j++) // add any terminal deletion counts
                    {
                        AddAlleleCount((int) j + lastPosition, AlleleType.Deletion, alignment.DirectionMap[i]);
                    }                                        
                }

                var position = alignment.PositionMap[i];

                if (position == -1){
                    continue; // not mapped to reference
                }

                for (var j = lastPosition + 1; j < position; j++) // add any deletion counts
                {
                    AddAlleleCount(j, AlleleType.Deletion, alignment.DirectionMap[i]);
                }                    

                var alleleType = AlleleHelper.GetAlleleType(alignment.Sequence[i]);

                if (alignment.Qualities[i] < _minBasecallQuality)
                    alleleType = AlleleType.N; // record this event as a no call

                AddAlleleCount(position, alleleType, alignment.DirectionMap[i]);

                lastPosition = position;
            }

            if (endsInDeletion)
            {
                for (var j = 1; j < deletionLength + 1; j++) // add any terminal deletion counts
                {
                    AddAlleleCount((int)j + lastPosition, AlleleType.Deletion, alignment.DirectionMap[alignment.DirectionMap.Length-1]);
                }
            }
        }

        private void AddAlleleCount(int position, AlleleType alleleType, DirectionType directionType)
        {
            var block = GetBlock(position);
            block.AddAlleleCount(position, alleleType, directionType);
        }

        public int GetAlleleCount(int position, AlleleType alleleType, DirectionType directionType)
        {
            var region = GetBlock(position, false);

            return region == null ? 0 : region.GetAlleleCount(position, alleleType, directionType);
        }

        public int GetGappedMnvRefCount(int position)
        {
            var region = GetBlock(position, false);

            return region == null ? 0 : region.GetGappedMnvRefCount(position);
        }

        /// <summary>
        /// Only pass back candidates from blocks where the entire block region is less than upToPosition
        /// and there's a fully completed block after it.  The second criteria is to ensure variants that
        /// span blocks have fully completed info in either flanking block.
        /// </summary>
        /// <param name="upToPosition"></param>
        /// /// <param name="chrReference"></param>
        /// /// <param name="intervalSet"></param>
        /// <returns></returns>
        public ICandidateBatch GetCandidatesToProcess(int? upToPosition, ChrReference chrReference = null)
        {
            var batch = new CandidateBatch {MaxClearedPosition = upToPosition.HasValue ? -1 : (int?)null};

            // only create a real batch if we haved moved onto another block 
            if (!upToPosition.HasValue || GetBlockKey(upToPosition.Value) != _lastUpToKey)
            {
                var blockKeys = upToPosition.HasValue
                    ? _regionLookup.Keys.Where(k => (k + 1)*_regionSize <= upToPosition).ToArray()
                    : _regionLookup.Keys.ToArray();

                var blocks = new List<RegionState>();

                Array.Sort(blockKeys); // need to sort the keys so we can bounce out as soon as we hit a held block

                foreach (var key in blockKeys)
                {
                    var block = _regionLookup[key];
                    if (upToPosition != null && block.MaxAlleleEndpoint > upToPosition)
                    {
                        break;
                    }
                    batch.Add(block.GetAllCandidates(_includeRefAlleles, chrReference, _intervalSet));
                    batch.BlockKeys.Add(key);
                    blocks.Add(block);
                }

                if (blocks.Any())
                {
                    batch.ClearedRegions = new List<Region>(blocks.Select(b => b as Region));
                    batch.MaxClearedPosition = blocks.Max(b => b.EndPosition);
                }
            }

            _lastUpToKey = upToPosition.HasValue ? GetBlockKey(upToPosition.Value) : -1;  // doesnt matter what we set to for last round

            return batch;
        }

        public void DoneProcessing(ICandidateBatch batch)
        {
            var candidateBatch = batch as CandidateBatch;

            if (candidateBatch != null)
            {
                foreach (var key in candidateBatch.BlockKeys)
                {
                    var blockToRemove = _regionLookup[key];
                    _reusableBlocks.Push(blockToRemove); // save for reuse later

                    _regionLookup.Remove(key);
                }
            }
        }

        private RegionState GetBlock(int position, bool addIfMissing = true)
        {
            if (position <= 0)
                throw new ArgumentException("Position must be greater than 0.");

            if (_lastAccessedBlock != null && _lastAccessedBlock.ContainsPosition(position))  // performance improvement to remember last block
                return _lastAccessedBlock;

            var blockKey = GetBlockKey(position);

            RegionState block;
            if (!_regionLookup.TryGetValue(blockKey, out block))
            {
                if (!addIfMissing)
                    return null;

                block = CreateOrReuseBlock((blockKey - 1) * _regionSize + 1, blockKey * _regionSize);
                _regionLookup[blockKey] = block;
            }

            _lastAccessedBlock = block;
            return block;
        }

        private int GetBlockKey(int position)
        {
            // 1-1000 -> 1st block
            // 1001-2000 -> 2nd block
            // 2001-3000 -> 3rd block
            return (int)Math.Ceiling((double)position / _regionSize);
        }

        /// <summary>
        /// Get block by either reusable one that is available for reuse, or creating a new one if non available for reuse.
        /// </summary>
        /// <param name="startPosition"></param>
        /// <param name="endPosition"></param>
        /// <returns></returns>
        private RegionState CreateOrReuseBlock(int startPosition, int endPosition)
        {
            RegionState block;

            if (_reusableBlocks.Any())
            {
                block = _reusableBlocks.Pop();
                block.Reset(startPosition, endPosition);
            }
            else
            {
                block = new RegionState(startPosition, endPosition);
            }

            return block;
        }
    }
}
