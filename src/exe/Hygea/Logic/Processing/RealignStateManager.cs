using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Processing.Interfaces;
using Pisces.Processing.Models;
using Pisces.Processing.RegionState;
using MyRegionState = Pisces.Processing.RegionState.RegionState;
using RealignIndels.Models;

namespace RealignIndels.Logic
{
    public class RealignStateManager : RegionStateManager
    {
        private int _lastMaxClearedPosition;

        public RealignStateManager(int minBasecallQuality = 20, int realignWindowSize = 1000) 
            : base(minBasecallQuality: minBasecallQuality, blockSize: realignWindowSize)
        {

        }

        public override ICandidateBatch GetCandidatesToProcess(int? upToPosition, ChrReference chrReference = null,HashSet<Tuple<string,int,string,string>> forcedGtAlleles=null)
        {
            try
            {
                // only create a real batch if we haved moved onto another block 
                if (upToPosition.HasValue && GetBlockKey(upToPosition.Value) == _lastUpToBlockKey)
                {
                    return null;
                }

                var batch = new CandidateBatch { MaxClearedPosition = upToPosition.HasValue ? -1 : (int?)null };

                var blockKeys = upToPosition.HasValue
                    ? _regionLookup.Keys.Where(k => k * _regionSize <= upToPosition).ToArray()
                    : _regionLookup.Keys.ToArray();

                var blocksToRealign = new List<MyRegionState>();

                Array.Sort(blockKeys); // need to sort the keys so we can bounce out as soon as we hit a held block

                foreach (var key in blockKeys)
                {
                    // add candidates from everyone
                    var block = _regionLookup[key];
                    batch.Add(block.GetAllCandidates(false, chrReference, null));                    

                    // only realign blocks that havent been cleared and are one window away from upToPosition
                    if (block.StartPosition > _lastMaxClearedPosition && 
                        (upToPosition.HasValue && block.EndPosition + _regionSize < upToPosition))
                    {
                        batch.BlockKeys.Add(key);
                        blocksToRealign.Add(block);
                    }
                }

                if (blocksToRealign.Any())
                {
                    batch.ClearedRegions = new List<Region>(blocksToRealign.Select(b => b as Region));
                    batch.MaxClearedPosition = blocksToRealign.Max(b => b.EndPosition);
                }

                return batch;
            }
            finally
            {
                _lastUpToBlockKey = upToPosition.HasValue ? GetBlockKey(upToPosition.Value) : -1;
                // doesnt matter what we set to for last round
            }
        }

        public HashSet<Tuple<string, string, string>> GetCandidateGroups(int? upToPosition)
        {            
            var candidateIndelGroups = new HashSet<Tuple<string, string, string>>();

            var blockKeys = upToPosition.HasValue
                ? _regionLookup.Keys.Where(k => k * _regionSize <= upToPosition).ToArray()
                : _regionLookup.Keys.ToArray();

            Array.Sort(blockKeys); // need to sort the keys so we can bounce out as soon as we hit a held block

            foreach (var key in blockKeys)
            {
                var block = _regionLookup[key];
                if (block.GetBlockCandidateGroup() != null)
                {
                    foreach (var blockCandidateGroup in block.GetBlockCandidateGroup())
                    {
                        candidateIndelGroups.Add(blockCandidateGroup);
                    }
                }                    
            }
            return candidateIndelGroups;                                         
        }

        public override void DoneProcessing(ICandidateBatch batch)
        {
            var candidateBatch = batch as CandidateBatch;

            if (batch.MaxClearedPosition.HasValue)
                _lastMaxClearedPosition = batch.MaxClearedPosition.Value;

            if (candidateBatch != null && candidateBatch.BlockKeys.Any())
            {
                var maxBlockKey = candidateBatch.BlockKeys.Max();

                var keysToRemove = new List<int>();
                foreach (var key in _regionLookup.Keys)
                {
                    if (key < maxBlockKey)  // want to only clear blocks outside of 1-block window for next potential batch
                        keysToRemove.Add(key);
                }

                foreach (var keyToRemove in keysToRemove)
                {
                    var blockToRemove = _regionLookup[keyToRemove];
                    _reusableBlocks.Push(blockToRemove); // save for reuse later

                    _regionLookup.Remove(keyToRemove);

                    if (_lastAccessedBlock != null && _lastAccessedBlock.Equals(blockToRemove))
                        _lastAccessedBlock = null;
                }
            }
        }
    }
}
