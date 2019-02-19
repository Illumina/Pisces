using Alignment.IO;

namespace Gemini.IO
{
    public interface IBamWriterFactory
    {
        IBamWriterHandle CreateSingleBamWriter(string outBam);
        IBamWriterMultithreaded CreateBamWriter(string outBam, int? overrideNumThreads = null);
    }
}