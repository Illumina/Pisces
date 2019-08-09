using System;
using Gemini.Interfaces;
using Gemini.Types;

namespace Gemini.IO
{
    public class ReusableSnippetSource : IGenomeSnippetSource
    {
        private readonly IGenomeSnippetSource _snippetSource;
        private readonly int _snippetBuffer;
        private GenomeSnippet _snippet;
        private int _lastPosition;
        private int _currentEndPos;

        public ReusableSnippetSource(IGenomeSnippetSource snippetSource, int snippetBuffer = 1000)
        {
            _snippetSource = snippetSource;
            _snippetBuffer = snippetBuffer;
        }
        public void Dispose()
        {
        }

        public GenomeSnippet GetGenomeSnippet(int position)
        {
            if (position < 0)
            {
                throw new ArgumentException(
                    $"Invalid snippet reference position ({position}): must be non-negative.");
            }
            if (Math.Abs(position - _lastPosition) < _snippetBuffer && _currentEndPos - position > _snippetBuffer)
            {
                return _snippet;
            }
            else
            {
                _snippet = _snippetSource.GetGenomeSnippet(position);
                _lastPosition = position;
                _currentEndPos = _snippet.StartPosition + _snippet.Sequence.Length;
                return _snippet;
            }
        }
    }
}