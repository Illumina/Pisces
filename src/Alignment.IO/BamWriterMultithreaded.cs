using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Alignment.Domain.Sequencing;
using Common.IO.Sequencing;

namespace Alignment.IO.Sequencing
{
    public class BamWriterMultithreaded : BamWriter, IBamWriterMultithreaded
    {
        public class BamWriterHandle : IBamWriterHandle
        {
            BamWriterMultithreaded _bamWriter;
            int _threadNumber;

            public BamWriterHandle(BamWriterMultithreaded bamWriter, int threadNumber)
            {
                _bamWriter = bamWriter;
                _threadNumber = threadNumber;
            }

            public void WriteAlignment(BamAlignment al)
            {
                _bamWriter.WriteAlignment(al, _threadNumber);
            }
        }

        #region members
        private List<List<BamAlignment>> _alignmentBuffer;
        const int MAX_BUFFER_SIZE = 100;
        #endregion

        public BamWriterMultithreaded(Stream outStream,
                                      string samHeader,
                                      List<GenomeMetadata.SequenceMetadata> references,
                                      int numThreads = 1,
                                      int compressionLevel = BamConstants.DefaultCompression)
            : base(outStream, samHeader, references, compressionLevel, numThreads)
        {
            Initialize(numThreads);
        }

        public BamWriterMultithreaded(string fileName,
                                      string samHeader,
                                      List<GenomeMetadata.SequenceMetadata> references,
                                      int numThreads = 1,
                                      int compressionLevel = BamConstants.DefaultCompression)
            : base(fileName, samHeader, references, compressionLevel, numThreads)
        {
            Initialize(numThreads);
        }

        private void Initialize(int numThreads)
        {
            _alignmentBuffer = new List<List<BamAlignment>>(numThreads);
            for (int i = 0; i < numThreads; ++i)
            {
                _alignmentBuffer.Add(new List<BamAlignment>(MAX_BUFFER_SIZE));
            }
        }

        public List<IBamWriterHandle> GenerateHandles()
        {
            List<IBamWriterHandle> handles = new List<IBamWriterHandle>();
            for (int i = 0; i < NumThreads; ++i)
            {
                handles.Add(new BamWriterHandle(this, i));
            }
            return handles;
        }

        public void Flush()
        {
            foreach (var buffer in _alignmentBuffer)
            {
                foreach (var alignment in buffer)
                {
                    WriteAlignment(alignment);
                }
            }
        }

        private void WriteAlignment(BamAlignment al, int bufferNumber)
        {
            var buffer = _alignmentBuffer[bufferNumber];
            buffer.Add(al);
            if (buffer.Count >= MAX_BUFFER_SIZE)
            {
                lock (_alignmentBuffer)
                {
                    foreach (var alignment in buffer)
                    {
                        WriteAlignment(alignment);
                    }
                }

                buffer.Clear();
            }
        }
    }
}
