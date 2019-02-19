using System.IO;
using Gemini.IO;

namespace Gemini
{
    public interface IGeminiDataOutputFactory
    {
        IBamWriterFactory GetBamWriterFactory(string inBam);
        TextWriter GetTextWriter(string outFile);
    }
}