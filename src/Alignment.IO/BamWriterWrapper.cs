using Alignment.IO.Sequencing;
using Alignment.Domain.Sequencing;
using Alignment.Domain;

namespace Alignment.IO
{
    public class BamWriterWrapper : IBamWriter
    {
        // Would really like to just update BamWriter to be based on IBamWriter. This works for now.
        private BamWriter _writer;

        public BamWriterWrapper(BamWriter writer)
        {
            _writer = writer;
        }

        public void WriteAlignment(BamAlignment alignment)
        {
            _writer.WriteAlignment(alignment);
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Close();
                _writer.Dispose();

                _writer = null;
            }
        }
    }
}
