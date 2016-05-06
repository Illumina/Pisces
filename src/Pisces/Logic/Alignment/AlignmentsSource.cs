using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Interfaces;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Processing.Utility;

namespace Pisces.Logic.Alignment
{
    public class AlignmentSource : IAlignmentSource
    {
        private readonly IAlignmentExtractor _alignmentExtractor;
        private readonly IAlignmentMateFinder _mateFinder;
        private readonly IAlignmentStitcher _stitcher;
        private readonly AlignmentSourceConfig _config;

        private int _unstitchablePairs;
        private int _totalReadsReturned;
        private int _totalSkipped;

        private int _lastReadPosition;
        private Read _read = new Read();

        public AlignmentSource(IAlignmentExtractor alignmentExtractor, 
            IAlignmentMateFinder mateFinder, 
            IAlignmentStitcher stitcher,
            AlignmentSourceConfig config) // add intervals
        {
            _alignmentExtractor = alignmentExtractor;
            _mateFinder = mateFinder;
            _stitcher = stitcher;
            _config = config;
        }

        public AlignmentSet GetNextAlignmentSet()
        {
            while (true) 
            {
                if (!_alignmentExtractor.GetNextAlignment(_read))
                {
                    var message = string.Format("Totals: {0} reads processed, {1} reads skipped", _totalReadsReturned, _totalSkipped);

                    if (_stitcher != null)
                        message += string.Format(", {0} read pairs unstitchable", _unstitchablePairs);

                    if (_mateFinder != null)
                        message += string.Format(", {0} reads unpairable", _mateFinder.ReadsSkipped);

                    Logger.WriteToLog(message);
                    return null; // no more reads
                }

                _lastReadPosition = _read.Position;
                if (ShouldSkipRead(_read))
                {
                    _totalSkipped ++;
                    continue;
                }

                if (!_read.IsProperPair || _mateFinder == null) 
                {
                    _totalReadsReturned ++;
                    return new AlignmentSet(_read, null, true);
                }

                var origMate = _mateFinder.GetMate(_read);

                if (origMate != null)
                {
                    var alignmentSet = new AlignmentSet(origMate, _read);

                    if (_stitcher != null)
                    {
                        if (!_stitcher.TryStitch(alignmentSet))
                        {
                            _unstitchablePairs++;

                            var readsToProcess = HandleUnstitchableReads(alignmentSet);
                            alignmentSet.ReadsForProcessing.AddRange(readsToProcess);
                        }
                    }
                    else
                    {
                        alignmentSet.ReadsForProcessing.Add(alignmentSet.PartnerRead1);
                        alignmentSet.ReadsForProcessing.Add(alignmentSet.PartnerRead2);
                    }

                    _totalReadsReturned += 2;

                    return alignmentSet;
                }
            }
        }

        private List<Read> HandleUnstitchableReads(AlignmentSet alignmentSet)
        {
            switch (_config.UnstitchableStrategy)
            {
                case UnstitchableStrategy.TakeStrongerRead:
                {
                    //Take read with higher average quality, or read1 if equal.
                    var strongerRead = alignmentSet.PartnerRead2.Qualities.Average(q => q) > alignmentSet.PartnerRead1.Qualities.Average(q => q) ?
                        alignmentSet.PartnerRead2 : alignmentSet.PartnerRead1;
                    return new List<Read> {strongerRead};
                }
                case UnstitchableStrategy.TakeBothReads:
                    return new List<Read> {alignmentSet.PartnerRead1, alignmentSet.PartnerRead2};
                case UnstitchableStrategy.TakeNoReads:
                    return new List<Read>();
                default:
                    return new List<Read> { alignmentSet.PartnerRead1, alignmentSet.PartnerRead2 };
            }
        }

        private bool ShouldSkipRead(Read read)
        {
            return (!read.IsMapped ||
                    !read.IsPrimaryAlignment ||
                    (_config.OnlyUseProperPairs && !read.IsProperPair) ||
                    read.IsPcrDuplicate ||
                    read.MapQuality < _config.MinimumMapQuality ||
                    !read.HasCigar);
        }

        public int? LastClearedPosition
        {
            get { return (_mateFinder != null && _mateFinder.LastClearedPosition.HasValue) ? _mateFinder.LastClearedPosition : _lastReadPosition - 1; }
        }
    }

    public class AlignmentSourceConfig
    {
        public bool OnlyUseProperPairs { get; set; }
        public int MinimumMapQuality { get; set; }
        public UnstitchableStrategy UnstitchableStrategy { get; set; }
    }

    public enum UnstitchableStrategy
    {
        TakeBothReads,
        TakeStrongerRead,
        TakeNoReads
    }
}