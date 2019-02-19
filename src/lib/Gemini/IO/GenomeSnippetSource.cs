using System;
using Gemini.Interfaces;
using Gemini.Types;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;

namespace Gemini.IO
{
    public class GenomeSnippetSource : IGenomeSnippetSource
    {
        private readonly int _genomeContextSize;
        private readonly int _buffer;
        private ChrReference _chrReference;

        public GenomeSnippetSource(string chrom, IGenome genome, int genomeContextSize, int buffer = 300)
        {
            _genomeContextSize = genomeContextSize;
            _buffer = buffer;
            _chrReference = genome.GetChrReference(chrom); // TODO keep this in memory or access each time?
        }

        public GenomeSnippet GetGenomeSnippet(int position)
        {
            if (_chrReference == null)
            {
                // TODO optionally could open the genome back up?
                throw new Exception("Already disposed of the chr reference.");
            }

            if (position < 0)
            {
                throw new ArgumentException(
                    $"Invalid snippet reference position ({position}): must be non-negative.");
            }

            var contextStart = position - _genomeContextSize;

            contextStart -= _buffer;
            contextStart = Math.Max(0, contextStart);

            if (contextStart >= _chrReference.Sequence.Length)
            {
                throw new ArgumentException(
                    $"Snippet would go off the end of the chromosome: {position} vs {_chrReference.Sequence.Length}.");
            }

            var contextLength = Math.Min(_chrReference.Sequence.Length - contextStart, 2 * _buffer + _genomeContextSize * 2);
            var context = _chrReference.Sequence.Substring(Math.Max(0,contextStart), contextLength);

            var snippet = new GenomeSnippet
            {
                Chromosome = _chrReference.Name,
                Sequence = context,
                StartPosition = contextStart
            };

            return snippet;
        }

        public void Dispose()
        {
            _chrReference = null;
        }
    }
}