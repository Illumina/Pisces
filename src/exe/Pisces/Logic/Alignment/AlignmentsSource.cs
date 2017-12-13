using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Interfaces;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Common.IO.Utility;

namespace Pisces.Logic.Alignment
{
    public class AlignmentSource : IAlignmentSource
    {
        private readonly IAlignmentExtractor _alignmentExtractor;
        private readonly IAlignmentMateFinder _mateFinder;
        private readonly AlignmentSourceConfig _config;

        private int _totalReadsReturned;
        private int _totalSkipped;

        private int _lastReadPosition;
        private Read _read = new Read();

        public bool SourceIsStitched
        {
            get
            {
                return _alignmentExtractor.SourceIsStitched;
            }
        }

        public bool SourceIsCollapsed
        {
            get
            {
                return _alignmentExtractor.SourceIsCollapsed; 

            }
        }


        public List<string> OrderedReferenceNames
        {
            get
            {
                return _alignmentExtractor.SourceReferenceList;
            }
        }
        public AlignmentSource(IAlignmentExtractor alignmentExtractor, 
            IAlignmentMateFinder mateFinder, 
            AlignmentSourceConfig config) // add intervals
        {
            _alignmentExtractor = alignmentExtractor;
            _mateFinder = mateFinder;
            _config = config;
        }

        public Read GetNextRead()
        {
            while (true) 
            {
                if (!_alignmentExtractor.GetNextAlignment(_read))
                {
                    var message = string.Format("Totals: {0} reads processed, {1} reads skipped", _totalReadsReturned, _totalSkipped);

                    if (_mateFinder != null)
                        message += string.Format(", {0} reads unpairable", _mateFinder.ReadsUnpairable);

                    Logger.WriteToLog(message);
                    return null; // no more reads
                }

                _lastReadPosition = _read.Position;
                if (ShouldSkipRead(_read))
                {
                    _totalSkipped ++;
                    continue;
                }

                _totalReadsReturned++;
                return _read;
            }
        }

        private bool ShouldSkipRead(Read read)
        {
            return (!read.IsMapped ||
                    !read.IsPrimaryAlignment ||
                    (_config.OnlyUseProperPairs && !read.IsProperPair) ||
                    (_config.SkipDuplicates && read.IsPcrDuplicate) ||
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
        public bool SkipDuplicates { get; set; }
        public bool OnlyUseProperPairs { get; set; }
        public int MinimumMapQuality { get; set; }
    }

}