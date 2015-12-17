using System;
using System.Linq;
using CallSomaticVariants.Infrastructure;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;
using SequencingFiles;

namespace CallSomaticVariants.Logic.Alignment
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
                    Logger.WriteToLog("Totals: {0} reads processed, {1} reads skipped, {2} read pairs unstitchable.", _totalReadsReturned, _totalSkipped, _unstitchablePairs);
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

                    try
                    {

                        if (_stitcher != null)
                            _stitcher.TryStitch(alignmentSet);
                        else
                        {
                            alignmentSet.ReadsForProcessing.Add(alignmentSet.PartnerRead1);
                            alignmentSet.ReadsForProcessing.Add(alignmentSet.PartnerRead2);
                        }

                        _totalReadsReturned += 2;

                        return alignmentSet;
                    }
                    catch (ReadsNotStitchableException ex)
                    {
                        _unstitchablePairs++;
                        
                        //For now go back to processing reads separately if they are unstitchable
                        alignmentSet.ReadsForProcessing.Add(alignmentSet.PartnerRead1);
                        alignmentSet.ReadsForProcessing.Add(alignmentSet.PartnerRead2);
                        _totalReadsReturned += 2;
                        return alignmentSet;

                        //if (Constants.DebugMode)
                        //    Logger.WriteToLog(
                        //        string.Format("Unable to stitch reads '{0}' and '{1}'.  Will skip pair.  Error: {2}",
                        //            origMate, alignment, ex.Message));

                    }
                }
            }
        }

        private bool ShouldSkipRead(Read read)
        {
            return (!read.IsMapped ||
                    !read.IsPrimaryAlignment ||
                    (_config.OnlyUseProperPairs && !read.IsProperPair) ||
                    read.IsPcrDuplicate ||
                    read.MapQuality < _config.MinimumMapQuality ||
                    read.CigarData == null || 
                    read.CigarData.Count == 0);
        }

        public int? LastClearedPosition
        {
            get { return (_mateFinder != null && _mateFinder.LastClearedPosition.HasValue) ? _mateFinder.LastClearedPosition : _lastReadPosition - 1; }
        }

        public string ChromosomeFilter { get { return _alignmentExtractor.ChromosomeFilter; }}
    }

    public class AlignmentSourceConfig
    {
        public bool OnlyUseProperPairs { get; set; }
        public int MinimumMapQuality { get; set; }
    }   
}