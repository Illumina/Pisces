using System;
using System.Collections.Generic;
using Pisces.Domain.Models;

namespace Pisces.Domain.Interfaces
{
    public interface IAlignmentExtractor : IDisposable
    {
        bool GetNextAlignment(Read read);

        bool Jump(string chromosomeName, int position = 0);

        void Reset();
    }
}
