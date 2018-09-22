using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Alignment.IO.Sequencing;

namespace Alignment.Logic.Tests
{
    public class MockBamReader : IBamReader
    {
        private readonly List<BamAlignment> _alignments;
        private readonly Dictionary<string, int> _refIdLookup;
        private int _currentIndex = 0;


        public MockBamReader(List<BamAlignment> alignments, Dictionary<string, int> refIdLookup = null)
        {
            _alignments = alignments;
            _refIdLookup = refIdLookup;
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
            return _refIdLookup[referenceName];
        }

        public bool Jump(int refID, int position)
        {
            // Works for jumping forward only
            while (_currentIndex < _alignments.Count)
            {
                if (_alignments[_currentIndex].RefID >= refID)
                {
                    if (_alignments[_currentIndex].Position >= position)
                    {
                        return true;
                        break;
                    }
                }

                _currentIndex++;
            }

            return false;
        }

        public List<string> GetReferenceNames()
        {
            return _refIdLookup.Keys.ToList();
        }
    }
}
