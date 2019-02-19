using System;

namespace Gemini.Interfaces
{
    public interface IDataSource<T> : IDisposable
    {
        T GetNextEntryUntilNull();
    }
}