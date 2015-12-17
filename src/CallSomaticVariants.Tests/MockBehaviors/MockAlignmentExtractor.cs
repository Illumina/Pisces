using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using Moq;
using SequencingFiles;

namespace CallSomaticVariants.Tests.MockBehaviors
{
    public class MockAlignmentExtractor : IAlignmentExtractor
    {
        private const string _alignmentNamePrefix = "Read";
        private int _readIndex = -1;
        private int _readCounter = 1;
        private List<Read> _reads;
        private readonly bool _stitchReads;

        public MockAlignmentExtractor(List<Read> reads, bool stitchReads = false)
        {
            _reads = reads;
            _stitchReads = stitchReads;
        }

        public MockAlignmentExtractor(ChrReference chrInfo, bool stitchReads = false)
        {
            _reads = new List<Read>();
            ChromosomeFilter = chrInfo.Name;
            _stitchReads = stitchReads;
        }

        public void Reset()
        {
            _readIndex = -1;
        }

        public void StageAlignment(BamAlignment read1A, BamAlignment read2A, int depth, string alignmentNamePrefix = null)
        {
            for (int i = 0; i < depth; i++)
            {
                var b1 = new BamAlignment(read1A);
                b1.Name = (alignmentNamePrefix ?? _alignmentNamePrefix) + _readCounter++;
                var read1 = new Read(ChromosomeFilter, b1, _stitchReads);
                var b2 = new BamAlignment(read2A);
                b2.Name = b1.Name;
                var read2 = new Read(ChromosomeFilter, b2, _stitchReads);

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
                read.Reset(sourceRead.Chromosome, sourceRead.BamAlignment, _stitchReads);
                return true;
            }
        }

        public void JumpToChromosome(string chromosomeName)
        {

        }

        public string ChromosomeFilter { get; set; }
    }
}
