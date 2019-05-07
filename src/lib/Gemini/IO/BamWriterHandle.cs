using Alignment.Domain.Sequencing;
using Alignment.IO;
using Alignment.IO.Sequencing;

namespace Gemini.IO
{
    public class BamWriterHandle : IBamWriterHandle
    {
        private readonly BamWriter _writer;

        public BamWriterHandle(BamWriter writer)
        {
            _writer = writer;
        }

        public void WriteAlignment(BamAlignment alignment)
        {
            if (alignment == null)
            {
                _writer?.Dispose();
            }
            else
            {
                _writer.WriteAlignment(alignment);
            }
        }
    }
}