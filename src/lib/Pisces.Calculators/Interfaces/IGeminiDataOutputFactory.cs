using Gemini.Interfaces;

namespace Gemini
{
    public interface IGeminiDataOutputFactory
    {
        ITextWriter GetTextWriter(string outFile);
        IWriterSource GetWriterSource(string inBam, string outBam);
    }
}