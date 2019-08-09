using System;
using System.Collections.Generic;

namespace Gemini.Interfaces
{
    public interface IDataSource<T> : IDisposable
    {
        T GetNextEntryUntilNull();
        IEnumerable<T> GetWaitingEntries(int upToPosition = -1);
    }
}