using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Alignment.Domain;
using Alignment.IO.Sequencing;
using Alignment.Domain.Sequencing;

namespace Alignment.Logic
{
    public abstract class AlignmentPairFilter : IAlignmentPairFilter
    {
        public int LastRefId;
        private readonly bool _removeFailedPairs;
        protected Action<string> OnLog;
        private readonly Dictionary<string, ReadPair> _readsWaitingForMate;
        private int _readsProcessed;
        private int _readsSkipped;
        private int _pairsSkipped;
        private int _pairsProcessed;
        private int _pairsPaired;
        private readonly HashSet<string> _blacklistedReads = new HashSet<string>();
        private readonly HashSet<string> _whitelistedReads = new HashSet<string>();
        private ReadPairEvaluator _pairEvaluator;

        protected AlignmentPairFilter(bool removeFailedPairs = true, ReadPairEvaluator pairEvaluator = null)
        {
            _removeFailedPairs = removeFailedPairs;
            OnLog = Console.WriteLine;
            _readsWaitingForMate = new Dictionary<string, ReadPair>();
            _pairEvaluator = pairEvaluator ?? new ReadPairEvaluator();
        }

        public ReadPair TryPair(BamAlignment alignment)
        {
            if (_readsProcessed % 100000 == 0)
            {
                OnLog(string.Format("At {6}:{7}. Processed {0} reads and {4} pairs so far. {1} skipped, {2} pairs skipped, {5} pairs paired, {3} currently waiting for mate",
                    _readsProcessed, _readsSkipped, _pairsSkipped, _readsWaitingForMate.Keys.Count, _pairsProcessed, _pairsPaired, alignment.RefID, alignment.Position));
            }

            _readsProcessed++;

            LastRefId = alignment.RefID;

            // Check if this read should be blacklisted
            if (ShouldBlacklistReadIndexer(alignment))
            {
                _blacklistedReads.Add(ReadIndexer(alignment));
            }

            // If this read is in the blacklist, skip the read. If we've already started tracking a mate, remove it from the mate lookup.
            if (_blacklistedReads.Contains(ReadIndexer(alignment)))
            {
                _readsSkipped++;
                if (_readsWaitingForMate.ContainsKey(ReadIndexer(alignment)))
                {
                    _readsWaitingForMate.Remove(ReadIndexer(alignment));
                }
                return null;
            }

            if (ShouldSkipRead(alignment))
            {
                _readsSkipped++;
                return null;
            }

            if (!_readsWaitingForMate.ContainsKey(ReadIndexer(alignment)))
            {
                _readsWaitingForMate.Add(ReadIndexer(alignment), new ReadPair(new BamAlignment(alignment), ReadIndexer(alignment), GetReadNumber(alignment)));
                return null;
            }
            else
            {
                var readPair = _readsWaitingForMate[ReadIndexer(alignment)];
                readPair.AddAlignment(alignment, GetReadNumber(alignment));

                if (!readPair.IsComplete() || _pairEvaluator.TreatReadPairAsIncomplete(readPair)) return null;

                _pairsProcessed++;

                // Check pair and either skip or return pair
                var shouldSkip = ShouldSkipPair(readPair);

                if (!shouldSkip || _removeFailedPairs)
                {
                    _readsWaitingForMate.Remove(ReadIndexer(alignment)); // If succeeded, no longer waiting for mate; if failed and we're being strict on failures, remove
                }
                if (ShouldWhitelistReadIndexer(alignment, shouldSkip))
                {
                    _whitelistedReads.Add(ReadIndexer(alignment));
                }

                if (shouldSkip) _pairsSkipped++;
                else _pairsPaired++;

                return shouldSkip ? null : readPair;
            }
        }

        public virtual bool ReachedFlushingCheckpoint(BamAlignment alignment)
        {
            return false;
        }

        protected virtual bool ShouldFlushUnpairedRead(BamAlignment alignment)
        {
            return false;
        }

        /// <summary>
        /// Read number may be tracked in the bam alignment, encoded in the name, or tracked by other means. Use this method to discern.
        /// </summary>
        /// <param name="alignment"></param>
        /// <returns></returns>
        protected virtual ReadNumber GetReadNumber(BamAlignment alignment)
        {
            return ReadNumber.NA;
        }

        /// <summary>
        /// String representation of the read used for the mate-pairing and blacklisting lookups. By default, read name
        /// is used (mates have same read name), but implementations may vary depending on situation.
        /// </summary>
        /// <param name="alignment"></param>
        /// <returns></returns>
        protected virtual string ReadIndexer(BamAlignment alignment)
        {
            return alignment.Name;
        }

        /// <summary>
        /// Whether a particular read should be skipped, i.e. is not suitable for pairing, 
        /// though its other related mates may be.
        /// </summary>
        /// <param name="alignment"></param>
        /// <returns></returns>
        protected abstract bool ShouldSkipRead(BamAlignment alignment);

        /// <summary>
        /// Whether a particular pair should be skipped. Given that we have collected a pair of reads that should
        /// not have been skipped on their own, determines whether the characteristics of the pair together make it
        /// worthy of skipping.
        /// </summary>
        /// <param name="pair"></param>
        /// <returns></returns>
        protected abstract bool ShouldSkipPair(ReadPair pair);

        /// <summary>
        /// Whether a particular mate indexer should be blacklisted, i.e. removed from reads looking for mates 
        /// and prevented from being considered in the future. This is stricter than skipping a read or pair.
        /// Whether a read has been blacklisted is also publicly accessible. 
        /// </summary>
        /// <param name="alignment"></param>
        /// <returns></returns>
        protected abstract bool ShouldBlacklistReadIndexer(BamAlignment alignment);

        /// <summary>
        /// Whether a particular mate indexer should be whitelisted, i.e. automatically kept in the future.
        /// Whether a read has been whitelisted is also publicly accessible. 
        /// </summary>
        /// <param name="alignment"></param>
        /// <returns></returns>
        protected virtual bool ShouldWhitelistReadIndexer(BamAlignment alignment, bool wasSkipped)
        {
            return false;
        }

        /// <summary>
        /// Whether a provided read's mate indexer is on the filter's blacklist. That way, if we've cleared a pair but later find out that group is disqualified, we can remove it.
        /// </summary>
        /// <param name="alignment"></param>
        /// <returns></returns>
        public bool ReadIsBlacklisted(BamAlignment alignment)
        {
            return _blacklistedReads.Contains(ReadIndexer(alignment));
        }

        /// <summary>
        /// Whether a provided read's mate indexer is on the filter's whitelist. That way, if we've got a pair that never resolved and we later find out it is qualified, we can clear it.
        /// </summary>
        /// <param name="alignment"></param>
        /// <returns></returns>
        public bool ReadIsWhitelisted(BamAlignment alignment)
        {
            return _whitelistedReads.Contains(ReadIndexer(alignment));
        }

        /// <summary>
        /// Returns a list of BamAlignments that were viable (not skipped, waiting for mates). Must specify whether to remove them from the waiting status.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<BamAlignment> GetUnpairedAlignments(bool clearWaiting)
        {
            var unpaired = _readsWaitingForMate.Values.SelectMany(x => x.GetAlignments()).ToList();
            if (clearWaiting) _readsWaitingForMate.Clear();
            return unpaired;
        }

        public IEnumerable<BamAlignment> GetFlushableUnpairedReads()
        {
            OnLog("At RefId=" + LastRefId + ": Scanning through " + _readsWaitingForMate.Keys.Count + " unmated reads.");

            var readyToFlush = new List<BamAlignment>();

            foreach (var key in _readsWaitingForMate.Keys.ToList())
            {
                var readPair = _readsWaitingForMate[key];
                if (ShouldFlushUnpairedRead(readPair.Read1))
                {
                    _readsWaitingForMate.Remove(key);
                    readyToFlush.AddRange(readPair.GetAlignments());
                }
            }

            OnLog(string.Format("Flushing {0} alignments no longer waiting for pair.", readyToFlush.Count));


            return readyToFlush;
        }

    }
}
