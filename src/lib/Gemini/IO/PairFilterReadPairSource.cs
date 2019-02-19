using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO.Sequencing;
using Alignment.Logic;
using Gemini.Interfaces;
using StitchingLogic;

namespace Gemini.IO
{
    public class PairFilterReadPairSource : IDataSource<ReadPair>
    {
        private int _readCount;
        private readonly IBamReader _bamReader;
        private readonly ReadStatusCounter _readStatuses;
        private readonly bool _skipAndRemoveDuplicates;
        private readonly bool _applyChrFilter;
        private readonly int _refId;
        private IAlignmentPairFilter _filter;
        private Queue<ReadPair> _unpaired = null;
        private bool _hasPassedChrom = false;

        public PairFilterReadPairSource(IBamReader bamReader, ReadStatusCounter readStatuses, bool skipAndRemoveDuplicates, IAlignmentPairFilter filter, int? refId = null)
        {
            _bamReader = bamReader;
            _readStatuses = readStatuses;
            _skipAndRemoveDuplicates = skipAndRemoveDuplicates;
            if (refId != null)
            {
                _applyChrFilter = true;
                _refId = refId.Value;
            }

            _filter = filter;
        }

        private PairStatus SingleReadStatus(BamAlignment alignment)
        {
            if (alignment.IsPrimaryAlignment() && (alignment.RefID != alignment.MateRefID && alignment.IsPaired())) return PairStatus.SplitChromosomes; // Stitched reads will have split ref ids too but not the same thing
            //if (alignment.MapQuality < _options.FilterMinMapQuality) return PairStatus.SplitQuality;
            if (alignment.IsPrimaryAlignment() && (!alignment.IsMateMapped() || !alignment.IsMapped())) return PairStatus.MateUnmapped;
            if (alignment.IsDuplicate()) return PairStatus.Duplicate;

            return PairStatus.Unknown;
        }

        public ReadPair GetNextEntryUntilNull()
        {
            var bamAlignment = new BamAlignment();

            while (!_hasPassedChrom)
            {
                var hasMoreReads = _bamReader.GetNextAlignment(ref bamAlignment, false);
                _readCount++;

                if (!hasMoreReads || _applyChrFilter && bamAlignment.RefID > _refId)
                {
                    if (_unpaired == null)
                    {
                        var unpaired = _filter.GetFlushableUnpairedReads();
                        _unpaired = new Queue<ReadPair>(unpaired.Select(r => new ReadPair(r)));
                    }

                    if (_applyChrFilter && bamAlignment.RefID > _refId)
                    {
                        _hasPassedChrom = true;
                    }
                    break;
                }

                if (_applyChrFilter && bamAlignment.RefID < _refId)
                {
                    _readStatuses.AddStatusCount("Chr skipping: alignment too low");
                    continue;
                }
                if (bamAlignment.IsDuplicate() && _skipAndRemoveDuplicates)
                {
                    _readStatuses.AddStatusCount("Skipping altogether: duplicate");
                    continue;
                }

                var status = SingleReadStatus(bamAlignment);
                if (status != PairStatus.Unknown)
                {
                    _readStatuses.AddStatusCount("Not even trying to stitch: " + status);
                    return new ReadPair(bamAlignment)
                    {
                        PairStatus = status
                    };
                }
                
                var filteredReadPair = _filter.TryPair(bamAlignment);

                if (filteredReadPair != null)
                {
                    filteredReadPair.PairStatus = PairStatus.Paired;
                    return filteredReadPair;
                }
            }

            while (true)
            {
                if (!_unpaired.Any())
                {
                    break;
                }

                return _unpaired.Dequeue();
            }


            return null;

        }

        public void Dispose()
        {
            // TODO
            return;
        }
    }
}