using System;
using Gemini.Types;

namespace Gemini.Interfaces
{
    public interface IGenomeSnippetSource : IDisposable
    {
        GenomeSnippet GetGenomeSnippet(int position);
    }
}