using System;
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
        private readonly IBamReader _bamReader;
        private readonly ReadStatusCounter _readStatuses;
        private readonly bool _skipAndRemoveDuplicates;
        private readonly bool _applyChrFilter;
        private readonly int _refId;
        private readonly IAlignmentPairFilter _filter;
        private Queue<ReadPair> _unpaired = null;
        private bool _hasPassedChrom = false;
        private readonly bool _considerInsertSize;
        private readonly int _expectedFragmentLength;
        private readonly bool _filterForProperPairs;
        private const string TooLow = "Chr skipping: alignment too low";
        private const string Duplicate = "Skipping altogether: duplicate";
        private const string WontPair = "Not even trying to pair";

        public PairFilterReadPairSource(IBamReader bamReader, ReadStatusCounter readStatuses, bool skipAndRemoveDuplicates, IAlignmentPairFilter filter, int? refId = null, 
            int? expectedFragmentLength = null, bool filterForProperPairs = false)
        {
            _bamReader = bamReader;
            _readStatuses = readStatuses;
            _skipAndRemoveDuplicates = skipAndRemoveDuplicates;
            if (refId != null)
            {
                _applyChrFilter = true;
                _refId = refId.Value;
                _bamReader.Jump(_refId, 0);
            }

            _filter = filter;
            _filterForProperPairs = filterForProperPairs;

            if (expectedFragmentLength != null)
            {
                _considerInsertSize = true;
                _expectedFragmentLength = expectedFragmentLength.Value;
            }

        }

        private bool OverlapsMate(BamAlignment alignment)
        {
            return alignment.RefID == alignment.MateRefID && (Math.Abs(alignment.FragmentLength) <= _expectedFragmentLength * 2);
        }

        private PairStatus SingleReadStatus(BamAlignment alignment)
        {
            if ((alignment.RefID != alignment.MateRefID && alignment.IsPaired())) return PairStatus.SplitChromosomes; // Stitched reads will have split ref ids too but not the same thing
                if (((!alignment.IsMateMapped() && alignment.RefID == -1)|| (!alignment.IsMapped() && alignment.MateRefID == -1))) return PairStatus.MateUnmapped;
            if (alignment.IsDuplicate()) return PairStatus.Duplicate;

            if (_considerInsertSize)
            {
                if (alignment.IsPaired() && !OverlapsMate(alignment))
                {
                    return PairStatus.LongFragment;
                }
            }
            return PairStatus.Unknown;
        }

        public ReadPair GetNextEntryUntilNull()
        {
            var bamAlignment = new BamAlignment();

            while (!_hasPassedChrom)
            {
                bool hasMoreReads;

                hasMoreReads = _bamReader.GetNextAlignment(ref bamAlignment, false);
                
                if (!hasMoreReads || _applyChrFilter && bamAlignment.RefID > _refId)
                {
                    FlushUnpaired();

                    if (_applyChrFilter && bamAlignment.RefID > _refId)
                    {
                        _hasPassedChrom = true;
                    }
                    break;
                }

                if (_applyChrFilter && bamAlignment.RefID < _refId)
                {
                    AddStatus(TooLow);
                    continue;
                }

                if (bamAlignment.IsDuplicate() && _skipAndRemoveDuplicates)
                {
                    AddStatus(Duplicate);
                    continue;
                }

                // TODO did we want to handle secondary/supplementaries differently?
                //if (bamAlignment.IsSecondary() || !bamAlignment.IsPrimaryAlignment())
                //{
                //    continue;
                //}

                if (ShouldSkipRead(bamAlignment))
                {
                    continue;
                }

                var status = SingleReadStatus(bamAlignment);

                if (status != PairStatus.Unknown )
                {
                    AddStatus(WontPair);
                    return new ReadPair(bamAlignment)
                    {
                        PairStatus = status
                    };
                }
                
                var filteredReadPair = _filter.TryPair(bamAlignment, status);

                if (filteredReadPair != null)
                {
                    if (filteredReadPair.PairStatus != PairStatus.OffTarget)
                    {
                        filteredReadPair.PairStatus = PairStatus.Paired;
                    }
                    return filteredReadPair;
                }
            }

            while (true)
            {
                if (_unpaired == null || !_unpaired.Any())
                {
                    break;
                }

                return _unpaired.Dequeue();
            }


            return null;

        }

        private bool ShouldSkipRead(BamAlignment alignment)
        {
            if (alignment.IsSupplementaryAlignment() || !alignment.IsPrimaryAlignment())
            {
                return true;
            }

            if (_filterForProperPairs && !alignment.IsProperPair())
            {
                return true;
            }

            return false;
        }


        private void FlushUnpaired()
        {
            if (_unpaired == null)
            {
                var unpaired = _filter.GetFlushableUnpairedReads();
                _unpaired = new Queue<ReadPair>(unpaired.Select(r => new ReadPair(r)));
            }
        }

        public IEnumerable<ReadPair> GetWaitingEntries(int upToPosition = -1)
        {
            return _filter.GetFlushableUnpairedPairs(upToPosition);
        }

        private void AddStatus(string status)
        {
            _readStatuses.AddStatusCount(status);
        }

        public void Dispose()
        {
            // TODO
            return;
        }
    }
}