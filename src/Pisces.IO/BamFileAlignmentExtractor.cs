using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SequencingFiles;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Utility;

namespace Pisces.IO
{
    public class BamFileAlignmentExtractor : IAlignmentExtractor
    {
        private BamReader _bamReader;
        private int _bamIndexFilter = -1;
        private BamAlignment _rawAlignment = null;
        private string _bamFilePath;
        private Dictionary<string, List<Region>> _remainingIntervals;
        private bool _shouldCheckJumpForCurrentInterval = true;
        private List<GenomeMetadata.SequenceMetadata> _references; 

        public BamFileAlignmentExtractor(string bamFilePath, string chromosomeFilter = null, Dictionary<string, List<Region>> bamIntervals = null)
        {
            if (!File.Exists(bamFilePath))
                throw new ArgumentException(string.Format("Bam file '{0}' does not exist.", bamFilePath));

            if (!File.Exists(bamFilePath + ".bai"))
                throw new ArgumentException(string.Format("Bai file '{0}.bai' does not exist.", bamFilePath));

            _bamFilePath = bamFilePath;
            _remainingIntervals = bamIntervals == null ? null : Copy(bamIntervals);

            InitializeReader(chromosomeFilter);
        }

        private void InitializeReader(string chromosomeFilter = null)
        {
            _bamReader = new BamReader(_bamFilePath);
            _references = _bamReader.GetReferences().OrderBy(r => r.Index).ToList();

            if (!string.IsNullOrEmpty(chromosomeFilter))
            {
                var chrReference = _references.FirstOrDefault(r => r.Name == chromosomeFilter);
                if (chrReference == null)
                    throw new Exception(string.Format("Cannot set chr filter to '{0}'.  This chr is not in the bam.", chromosomeFilter));

                _bamIndexFilter = chrReference.Index;
            }
            var chrToStart = !string.IsNullOrEmpty(chromosomeFilter)
                ? chromosomeFilter
                : _references.First().Name;

            var position = 0;
            if (_remainingIntervals != null && _remainingIntervals.ContainsKey(chrToStart))
            {
                position = _remainingIntervals[chrToStart][0].StartPosition - 1;
            }
            Jump(chrToStart, position);
        }

        public bool GetNextAlignment(Read read)
        {
            if (_bamReader == null)
                throw new Exception("Already disposed.");

            while (true)
            {
                Region currentInterval = null;

                if (_rawAlignment != null)
                {
                    var currentChrIntervals = GetIntervalsForChr(_rawAlignment.RefID);
                    if (currentChrIntervals != null) // null signals not to apply interval jumping
                        if (!JumpIfNeeded(currentChrIntervals, out currentInterval))
                        {
                            Dispose();
                            return false;
                        }
                }
                else
                {
                    _rawAlignment = new BamAlignment(); // first time pass
                }

                if (!_bamReader.GetNextAlignment(ref _rawAlignment, false) ||
                    ((_bamIndexFilter > -1) && (_rawAlignment.RefID != _bamIndexFilter)))
                {
                    Dispose();
                    return false;
                }
                if (currentInterval == null || _rawAlignment.Position < currentInterval.EndPosition)
                {
                    read.Reset(_references.First(r => r.Index == _rawAlignment.RefID).Name, _rawAlignment);

                    return true;
                }
                // read off the end of the interval - keep looping to jump to the next one or scan to the end
            }
        }

        public bool Jump(string chromosomeName, int positionIndex = 0)
        {
            var chrIndex = _references.First(r => r.Name == chromosomeName).Index;
            return _bamReader.Jump(chrIndex, positionIndex);
        }

        private bool Jump(int chrIndex, int positionIndex = 0)
        {
            return _bamReader.Jump(chrIndex, positionIndex); 
            // note there seems to be an off by 1 issue in bamReader
            // if you ask it to jump to 216, it will filter out all reads ending in 214, but include reads ending in 215
            // you would expect to filter out reads ending in 215.
            // this bug is ok because for interval jumping since we just end up reading a few more reads than we should.
        }

        public bool JumpIfNeeded(List<Region> chrIntervals)
        {
            Region r;
            return JumpIfNeeded(chrIntervals, out r);
        }

        private bool JumpIfNeeded(List<Region> chrIntervals, out Region currentRegion)
        {
            var completedIntervals = new List<Region>();

            for (var i = 0; i < chrIntervals.Count; i++)
            {
                var interval = chrIntervals[i];
                if ((_rawAlignment.Position + 1) > interval.EndPosition)  // bam alignment is 0-based, interval is 1-based
                {
                    completedIntervals.Add(interval);
                    _shouldCheckJumpForCurrentInterval = true;
                }
                else
                {
                    break;
                }
            }

            foreach (var completedInterval in completedIntervals)
                chrIntervals.Remove(completedInterval);

            // if done with intervals, jump to next chromosome
            if (!chrIntervals.Any())
            {
                currentRegion = null;
                if (_rawAlignment.RefID == _references.Count - 1)
                    return false;

                return Jump(_rawAlignment.RefID + 1);
            }
            currentRegion = chrIntervals[0];

            // if far from next interval, jump forward.  leave small buffer so we dont accidentally re-read already read alignments if there's not much gap inbetween 
            if (_shouldCheckJumpForCurrentInterval)
            {
                _shouldCheckJumpForCurrentInterval = false;

                var currentPosition = _bamReader.Tell();
                var targetInterval = currentRegion;
                var previousInterval = completedIntervals.LastOrDefault();
                const int buffer = 100;
                var refMaxIndex = (int) (_references.First(r => r.Index == _rawAlignment.RefID).Length - 1);
                var jumpToThreshold = Math.Min(Math.Max(0, targetInterval.StartPosition - buffer), refMaxIndex);

                if ((_rawAlignment.GetEndPosition() - _rawAlignment.CigarData.GetSuffixClip()) < jumpToThreshold)
                {
                    var success = Jump(_rawAlignment.RefID, Math.Min(Math.Max(0, targetInterval.StartPosition - 1), refMaxIndex));

                    if (!success)
                        return false;  // weren't able to jump because there's no data there

                    var jumpPosition = _bamReader.Tell();
                    if (jumpPosition < currentPosition && previousInterval != null)
                    {
                        // this was a jump backward, so we've already processed reads from here.
                        // this can happen if there are reads that span multiple regions. For example, a cigar string 
                        // like 82M2996D52M7S (real world example!)
                        while (_bamReader.GetNextAlignment(ref _rawAlignment, true) && _rawAlignment.Position < previousInterval.EndPosition)
                        {
                            jumpPosition = _bamReader.Tell();
                        }
                        _bamReader.Seek(jumpPosition); // seek back so we don't lose the read
                    }
                }
            }

            return true;
        }

        public void Reset()
        {
            if (_bamReader != null)
                Dispose();

            InitializeReader();
        }

        public void Dispose()
        {
            try
            {
                if (_bamReader != null)
                {
                    _bamReader.Dispose();
                    _bamReader = null;
                }
            }
            catch (Exception)
            {
                // swallow it
            }
        }

        private List<Region> GetIntervalsForChr(int chrIndex)
        {
            if (_remainingIntervals != null)
            {
                var chrName = _bamReader.GetReferenceNameByID(chrIndex);

                if (!_remainingIntervals.ContainsKey(chrName))
                    return new List<Region>() {new Region(1, 1)}; // return tiny interval of 1 so we essentially skip the chr

                return _remainingIntervals[chrName];
            }

            return null; // return null to signal that we shouldn't apply any interval optimization
        }

        private Dictionary<string, List<Region>> Copy(Dictionary<string, List<Region>> bamIntervals)
        {
            var copied = new Dictionary<string, List<Region>>();

            foreach (var lookup in bamIntervals)
            {
                var copiedList = new List<Region>();
                copied[lookup.Key] = copiedList;

                foreach(var region in lookup.Value)
                    copiedList.Add(region);
            }

            return copied;
        }
    }
}