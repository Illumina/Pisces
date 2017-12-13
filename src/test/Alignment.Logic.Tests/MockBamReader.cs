using System;
using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Alignment.IO.Sequencing;

namespace Alignment.Logic.Tests
{
    public class MockBamReader : IBamReader
    {
        private readonly List<BamAlignment> _alignments;
        private int _currentIndex = 0;

        public MockBamReader(List<BamAlignment> alignments)
        {
            _alignments = alignments;
        }

        public void Dispose()
        {
          //
        }


        public bool GetNextAlignment(ref BamAlignment alignment, bool skipAdditionalParsing)
        {
            if (_currentIndex >= _alignments.Count) return false;

            alignment = _alignments[_currentIndex];
            _currentIndex++;

            return true;
        }

        public int GetReferenceIndex(string referenceName)
        {
            return 0;
        }

        public bool Jump(int refID, int position)
        {
            throw new NotImplementedException();
        }

        public List<string> GetReferenceNames()
        {
            throw new NotImplementedException();
        }
    }
}
