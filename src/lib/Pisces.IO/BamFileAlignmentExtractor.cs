using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Alignment.IO.Sequencing;
using Common.IO.Sequencing;

namespace Pisces.IO
{
    public class BamFileAlignmentExtractor : IAlignmentExtractor
    {
        private BamReader _bamReader;
        private int _bamIndexFilter = -1;
        private BamAlignment _rawAlignment = null;
        private string _bamFilePath;
        private List<GenomeMetadata.SequenceMetadata> _references;
        private bool _bamIsStitched;

        public bool SourceIsCollapsed { get; private set; }

        public bool SourceIsStitched
        {
            get
            {
                return _bamIsStitched;
            }
        }

        public List<string> SourceReferenceList
        {
            get
            {
                return _references.Select(x => x.Name).ToList();
            }
        }

        public BamFileAlignmentExtractor(string bamFilePath, string chromosomeFilter = null)
        {
            if (!File.Exists(bamFilePath))
                throw new ArgumentException(string.Format("Bam file '{0}' does not exist.", bamFilePath));

            if (!File.Exists(bamFilePath + ".bai"))
                throw new ArgumentException(string.Format("Bai file '{0}.bai' does not exist.", bamFilePath));

            _bamFilePath = bamFilePath;
            InitializeReader(chromosomeFilter);
        }

        //check that order of the reference sequences (ie, chrs) in the bam do not violate the order of the 
        //reference sequences in in the genome (we believe the genome).
        public bool SequenceOrderingIsNotConsistent(List<string> chrsToProcess)
        {
            int lastIndex = 0;
            if (chrsToProcess == null)
                return false;

            foreach (var genomeSequence in chrsToProcess)
            {
                int foundIndex = SourceReferenceList.IndexOf(genomeSequence);

                if (foundIndex == -1)
                {
                    //We were asked to process a chr not in our genome.
                    //This probably not a good thing, but we will will catch that later if its goign to be a problem.
                    //Right now we are just going to complain if its strictly an ordering issue.
                    continue;
                }

                if (foundIndex < lastIndex)
                {
                    return true;
                   // throw new Exception("Reference sequences in the bam do not match the order of the reference sequences in the genome. Check bam " + _bamFilePath);
                }
                else
                    lastIndex = foundIndex;

            }
            return false;
        }

        private void InitializeReader(string chromosomeFilter = null)
        {
            _bamReader = new BamReader(_bamFilePath);
            _references = _bamReader.GetReferences().OrderBy(r => r.Index).ToList();
            _bamIsStitched = CheckIfBamHasBeenStitched(_bamReader.GetHeader());
            SourceIsCollapsed = CheckIfBamHasBeenCollapsed(_bamReader.GetHeader()); 

            if (!string.IsNullOrEmpty(chromosomeFilter))
            {
                var chrReference = _references.FirstOrDefault(r => r.Name == chromosomeFilter);
                if (chrReference == null)
                    throw new InvalidDataException(string.Format("Cannot set chr filter to '{0}'.  This chr is not in the bam.", chromosomeFilter));

                _bamIndexFilter = chrReference.Index;
            }
            var chrToStart = !string.IsNullOrEmpty(chromosomeFilter)
                ? chromosomeFilter
                : _references.First().Name;

            Jump(chrToStart);
        }

        public static bool CheckIfBamHasBeenCollapsed(string header)
        {
            if (string.IsNullOrEmpty(header))
                return false;

            string[] headerLines = header.Split('\n');

            foreach (var headerLine in headerLines)
            {
                if (string.IsNullOrEmpty(headerLine) || (headerLine.Length < 3))
                    continue;

                if ((headerLine.Substring(0, 3) == "@PG")
                    && headerLine.ToLower().Contains("pn:reco"))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CheckIfBamHasBeenStitched(string header)
        {
            if (string.IsNullOrEmpty(header))
                return false;

            string[] headerLines = header.Split('\n');

            foreach (var headerLine in headerLines)
            {
                if (string.IsNullOrEmpty(headerLine) || (headerLine.Length < 3))
                    continue;

                if ((headerLine.Substring(0, 3) == "@PG") 
                    && headerLine.ToLower().Contains("stitcher")
                          && (headerLine.ToLower().Contains("pisces")))
                {
                    return true;
                }
            }
            return false;
        }

        public bool GetNextAlignment(Read read)
        {
            if (_bamReader == null)
                throw new IOException("Already disposed.");

            while (true)
            {
                Region currentInterval = null;

                if(_rawAlignment == null)
                    _rawAlignment = new BamAlignment(); // first time pass
                
                if (!_bamReader.GetNextAlignment(ref _rawAlignment, false) ||
                    ((_bamIndexFilter > -1) && (_rawAlignment.RefID != _bamIndexFilter)))
                {
                    Dispose();
                    return false;
                }

                if (currentInterval == null || _rawAlignment.Position < currentInterval.EndPosition)
                {
                    var reference = _references.FirstOrDefault(r => r.Index == _rawAlignment.RefID);

                    read.Reset(reference?.Name, _rawAlignment);

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

    }
}