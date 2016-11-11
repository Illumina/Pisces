using System;
using System.Collections.Generic;
using Alignment.Domain;
using Alignment.IO;
using Alignment.Domain.Sequencing;
using Alignment.IO.Sequencing;

namespace Alignment.Logic
{
    public interface IAlignmentPairFilter
    {
        bool ReachedFlushingCheckpoint(BamAlignment bamAlignment);
        IEnumerable<BamAlignment> GetFlushableUnpairedReads();
        ReadPair TryPair(BamAlignment bamAlignment);
        IEnumerable<BamAlignment> GetUnpairedAlignments(bool b);
        bool ReadIsBlacklisted(BamAlignment bamAlignment);
    }
    public class BamRewriter
    {
        private readonly IBamReader _bamReader;
        private readonly IBamWriter _bamWriter;
        private readonly IAlignmentPairFilter _filter;
        private readonly IReadPairHandler _pairHandler;
        private readonly long? _bufferSize;
        private readonly List<BamAlignment> _alignmentBuffer;
        private readonly bool _getUnpaired;
        private readonly string _chrFilter;
        protected Action<string> OnLog;

        public BamRewriter(IBamReader bamReader, IBamWriter bamWriter, IAlignmentPairFilter filter,
            IReadPairHandler pairHandler, long? bufferSize = 100000, bool getUnpaired = false, string chrFilter = null)
        {
            _bamReader = bamReader;
            _bamWriter = bamWriter;
            _filter = filter;
            _pairHandler = pairHandler;
            _bufferSize = bufferSize;
            _getUnpaired = getUnpaired;
            _chrFilter = chrFilter;

            _alignmentBuffer = new List<BamAlignment>();

            OnLog = message => Console.WriteLine(message); 

        }

        public void Execute()
        {
            var bamAlignment = new BamAlignment();
            int? chrFilterRefIndex = null;

            if (_chrFilter != null)
            {
                chrFilterRefIndex = _bamReader.GetReferenceIndex(_chrFilter);
            }

            while (true)
            {
                if (_bufferSize != null && _alignmentBuffer.Count > _bufferSize)
                {
                    FlushToBam();
                }


                var hasMoreReads = _bamReader.GetNextAlignment(ref bamAlignment, false);
                if (!hasMoreReads) break;

                if (chrFilterRefIndex != null)
                {
                    if (bamAlignment.RefID < chrFilterRefIndex.Value)
                    {
                        continue;
                    }
                    if (bamAlignment.RefID > chrFilterRefIndex.Value)
                    {
                        OnLog("Ending BAM reading for " + _chrFilter + ".");
                        break;
                    }
                }

                if (_getUnpaired && _filter.ReachedFlushingCheckpoint(bamAlignment))
                {
                    var unpaired = _filter.GetFlushableUnpairedReads();
                    _alignmentBuffer.AddRange(unpaired);
                }

                var filteredReadPair = _filter.TryPair(bamAlignment);
                if (filteredReadPair != null)
                {
                    _alignmentBuffer.AddRange(_pairHandler.ExtractReads(filteredReadPair));
                }

            }

            if (_getUnpaired)
            {
                var unpaired = _filter.GetFlushableUnpairedReads();
                _alignmentBuffer.AddRange(unpaired);
            }
            FlushToBam();
        }

        private void FlushToBam()
        {
            OnLog(string.Format("Writing {0} alignments to bam file.", _alignmentBuffer.Count));
            var skippedAlignments = 0;

            foreach (var bamAlignment in _alignmentBuffer)
            {
                if (!_filter.ReadIsBlacklisted(bamAlignment))
                {
                    _bamWriter.WriteAlignment(bamAlignment);
                }
                else
                {
                    skippedAlignments++;
                }
            }

            OnLog(string.Format("Buffer flushed. Skipped {0} alignments.", skippedAlignments));

            _alignmentBuffer.Clear();
        }

    }
}