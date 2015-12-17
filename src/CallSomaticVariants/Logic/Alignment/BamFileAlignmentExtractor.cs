using System;
using System.IO;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;
using SequencingFiles;

namespace CallSomaticVariants.Logic.Alignment
{
    public class BamFileAlignmentExtractor : IAlignmentExtractor
    {
        private readonly bool _stitchReads;
        private BamReader _bamReader;
        public string ChromosomeFilter { get; private set;}
        private int _bamIndexFilter = -1;
        private BamAlignment _rawAlignment = new BamAlignment();

        public BamFileAlignmentExtractor(string bamFilePath, bool stitchReads, string chromosomeFilter = null)
        {
            _stitchReads = stitchReads;
            if (!File.Exists(bamFilePath))
                throw new ArgumentException(string.Format("Bam file '{0}' does not exist.", bamFilePath));

            if (!File.Exists(bamFilePath + ".bai"))
                throw new ArgumentException(string.Format("Bai file '{0}.bai' does not exist.", bamFilePath));

            _bamReader = new BamReader(bamFilePath);

            ChromosomeFilter = chromosomeFilter;

            if (!string.IsNullOrEmpty(ChromosomeFilter))
                JumpToChromosome(ChromosomeFilter);
        }

        public bool GetNextAlignment(Read read)
        {
            if (_bamReader == null)
                throw new Exception("Already disposed.");

            if (!_bamReader.GetNextAlignment(ref _rawAlignment, false) ||
                (_bamIndexFilter > -1 && _rawAlignment.RefID != _bamIndexFilter))
            {
                Dispose();
                return false;
            }

            read.Reset(_bamReader.GetReferenceNameByID(_rawAlignment.RefID), _rawAlignment, _stitchReads);

            return true;
        }

        public void JumpToChromosome(string chromosomeName)
        {
            _bamIndexFilter = _bamReader.GetReferenceIndex(chromosomeName);
            _bamReader.Jump(_bamIndexFilter, 0);
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
            catch (Exception ex)
            {
                // swallow it
                var wrappedException = new Exception("Error disposing BamReader: " + ex.Message, ex);
                Logger.WriteExceptionToLog(wrappedException);
            }
        }
    }
}