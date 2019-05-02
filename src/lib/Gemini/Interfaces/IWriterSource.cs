using System.Collections.Generic;
using Alignment.IO;
using Gemini.Types;

namespace Gemini.Interfaces
{
    public interface IWriterSource
    {
        IBamWriterHandle BamWriterHandle(string chrom,
            PairClassification classification,
            int idNum);

        void DoneWithWriter(string chrom, PairClassification classification, int idNum, int numWritten, IBamWriterHandle handle = null);
        void Finish();
        List<string> GetBamFiles();
    }
}