using System;
using Alignment.Domain;
using Alignment.Logic;
using Alignment.Domain.Sequencing;
using StitchingLogic;

namespace Stitcher
{
    public class StitcherPairFilter : AlignmentPairFilter
    {
        private readonly bool _skipDuplicates;
        private readonly bool _filterForProperPairs;
        private readonly IDuplicateIdentifier _dupIdentifier;
        private readonly int _minMapQuality;
        private readonly int _maxPairGap;
        private readonly ReadStatusCounter _statusCounter;
        private readonly bool _shouldSkipFusions;

        public StitcherPairFilter(bool skipDuplicates, bool filterForProperPairs, IDuplicateIdentifier dupIdentifier, ReadStatusCounter statusCounter,
            bool shouldSkipFusions = true, int minMapQuality = 0, int maxPairGap = 500) : base(true, new StitchingReadPairEvaluator(true,true, false), false)
        {
            _skipDuplicates = skipDuplicates;
            _filterForProperPairs = filterForProperPairs;
            _dupIdentifier = dupIdentifier;
            _shouldSkipFusions = shouldSkipFusions;
            _minMapQuality = minMapQuality;
            _maxPairGap = maxPairGap;
            _statusCounter = statusCounter;
        }

        protected override bool ShouldSkipRead(BamAlignment alignment)
        {
            if (!alignment.IsMapped())
            {
                _statusCounter.AddDebugStatusCount("Skipped not mapped");
                return true;
            }
            if (alignment.IsSupplementaryAlignment())
            {
                _statusCounter.AddDebugStatusCount("Skipped supplementary");
                return true;
            }
            if (_filterForProperPairs && !alignment.IsProperPair())
            {
                _statusCounter.AddDebugStatusCount("Skipped improper pair");
                return true;
            }
            if (alignment.MapQuality < _minMapQuality)
            {
                _statusCounter.AddDebugStatusCount("Skipped low map quality");
                return true;
            }

            return false;
        }

        private bool MayOverlapMate(BamAlignment alignment)
        {
            if (!alignment.IsMateMapped()) return false;
            if (!alignment.IsMapped()) return false;
            if (alignment.RefID != alignment.MateRefID) return false;
            if (Math.Abs(alignment.Position - alignment.MatePosition) > _maxPairGap) return false;

            return true;
        }

        protected override bool ShouldSkipPair(ReadPair pair)
        {
            // Given that we have a mated pair, whether we want to skip or pass them on to stitching.
            return false;
        }

        private bool ReadIsDuplicate(BamAlignment alignment)
        {
            if (_dupIdentifier.IsDuplicate(alignment))
            {
                _statusCounter.AddStatusCount("Duplicates");
                return _skipDuplicates;
            }
            return false;
        }

        protected override bool ShouldBlacklistReadIndexer(BamAlignment alignment)
        {
            // Only check if read is duplicate once (otherwise de novo dup finder will falsely mark dup because it has seen this read before!)
            // Blacklist rather than just skipping because if one mate is duplicate, we presume the other one is too.
            // Note: This breaks down is if we have a fusion read and the first mate we see is not a duplicate and the second mate is. In our case, 
            // (if we are not trying to mate fusions) we will flush the first mate to bam without knowing that the second mate is a dup.
            // This is a highly unlikely degenerate case.
            var isDuplicate = ReadIsDuplicate(alignment);
            if (isDuplicate)
            {
                _statusCounter.AddStatusCount("Blacklisted Duplicates");
            }
            return isDuplicate;
        }

        protected override bool ShouldFlushUnpairedRead(BamAlignment alignment)
        {
            // We only flush unpaired ("waiting") alignments once we change chromosomes 
            // (unless we want to keep these lying around to find fusion mates).
            var shouldFlush = _shouldSkipFusions;

            if (shouldFlush)
            {
                _statusCounter.AddStatusCount("Flushed Unpaired At End of Chromosome");
            }

            return shouldFlush;
        }

        public override bool ReachedFlushingCheckpoint(BamAlignment alignment)
        {
            // When we get to the end of a chromosome, flush the waiting reads from that chromosome
            // TODO in future of duplicate checking, may want to flush our tracked dups at each chromosome juncture
            return alignment.RefID != LastRefId;
        }
        
    }
}