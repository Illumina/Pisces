using System;
using System.Collections.Generic;
using Alignment.IO.Sequencing;
using Alignment.Domain.Sequencing;

namespace Alignment.IO
{
    public interface IBamWriter : IDisposable
    {
        void WriteAlignment(BamAlignment alignment);
    }

    public interface IBamWriterHandle
    {
        void WriteAlignment(BamAlignment alignment);
    }

    public interface IBamWriterMultithreaded : IBamWriter
    {
        List<IBamWriterHandle> GenerateHandles();

        void Flush();
    }
}