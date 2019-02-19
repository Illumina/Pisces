using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Alignment.IO.Sequencing;

namespace Gemini.IO
{
    public class CachedBamWriter : IBamWriterHandle
    {
        private readonly List<BamAlignment> alignments = new List<BamAlignment>();
        private readonly BamWriter _writer;
        private readonly int _cacheSize;

        public CachedBamWriter(BamWriter writer, int cacheSize = 1000)
        {
            _writer = writer;
            _cacheSize = cacheSize;
        }

        public void WriteAlignment(BamAlignment alignment)
        {
            if (alignment == null)
            {
                // TODO make this wrapper disposable instead
                foreach (var a in alignments)
                {
                    _writer.WriteAlignment(a);
                }

                alignments.Clear();

                _writer.Dispose();
            }

            alignments.Add(alignment);

            if (alignments.Count > _cacheSize)
            {
                foreach (var a in alignments)
                {
                    _writer.WriteAlignment(a);
                }

                alignments.Clear();
            }
        }
    }
}