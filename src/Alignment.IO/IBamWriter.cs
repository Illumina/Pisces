using System;
using Alignment.IO.Sequencing;
using Alignment.Domain.Sequencing;

namespace Alignment.IO
{
    public interface IBamWriter : IDisposable
    {
        void WriteAlignment(BamAlignment alignment);
    }
}