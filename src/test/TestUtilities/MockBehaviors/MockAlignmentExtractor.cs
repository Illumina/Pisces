using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Types;

namespace TestUtilities.MockBehaviors
{
    public class MockAlignmentExtractor : IAlignmentExtractor
    {
        private const string _alignmentNamePrefix = "Read";
        private int _readIndex = -1;
        private int _readCounter = 1;
        private List<Read> _reads;
        private readonly string _chrName;
        private bool _sourceIsStitched;

        public bool SourceIsStitched
        {
            get
            {
                return _sourceIsStitched;
            }
        }

        public List<string> SourceReferenceList
        {
            get
            {
                return new List<string>() { "dummy"};
            }
        }

        public bool SourceIsCollapsed => throw new System.NotImplementedException();

        public MockAlignmentExtractor(List<Read> reads)
        {
            _reads = reads;
        }

        public MockAlignmentExtractor(ChrReference chrInfo, bool SourceIsStitched = false)
        {
            _reads = new List<Read>();
            _chrName = chrInfo.Name;
            _sourceIsStitched = SourceIsStitched;
        }

        public void Reset()
        {
            _readIndex = -1;
        }


        public void StageAlignment(BamAlignment read1A, int depth, string alignmentNamePrefix = null)
        {
            for (int i = 0; i < depth; i++)
            {
                var b1 = new BamAlignment(read1A);
                b1.Name = (alignmentNamePrefix ?? _alignmentNamePrefix) + _readCounter++;
                var read1 = new Read(_chrName, b1);

                DirectionType[] dirMap = read1.SequencedBaseDirectionMap;
                _reads.Add(read1);
            }
        }

        public void StageAlignment(BamAlignment read1A, BamAlignment read2A, int depth, string alignmentNamePrefix = null)
        {
            for (int i = 0; i < depth; i++)
            {
                var b1 = new BamAlignment(read1A);
                b1.Name = (alignmentNamePrefix ?? _alignmentNamePrefix) + _readCounter++;
                var read1 = new Read(_chrName, b1);
                var b2 = new BamAlignment(read2A);
                b2.Name = b1.Name;
                var read2 = new Read(_chrName, b2);

                _reads.Add(read1);
                _reads.Add(read2);
            }
        }

        public bool GetNextAlignment(Read read)
        {
            _readIndex++;
            if (_readIndex >= _reads.Count)
                return false;
            else
            {
                var sourceRead = _reads[_readIndex];
                read.Reset(sourceRead.Chromosome, sourceRead.BamAlignment);
                return true;
            }
        }

        public bool Jump(string chromosomeName, int position = 0)
        {
            return true;
        }

        public void Dispose()
        {

        }
    }
}
