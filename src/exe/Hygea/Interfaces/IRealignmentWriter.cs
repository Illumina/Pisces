using Alignment.IO.Sequencing;
using Alignment.Domain.Sequencing;

namespace RealignIndels.Interfaces
{
    public interface IRealignmentWriter
    {
        void Initialize();
        void FinishAll();
        void FlushAllBufferedRecords();
        void WriteRead(ref BamAlignment read, bool remapped);
    }
}
