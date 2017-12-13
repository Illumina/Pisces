using System;
using System.Collections;
using System.Collections.Generic;
using Pisces.Domain.Models;

namespace Pisces.Domain.Interfaces
{
    public interface IAlignmentMateFinder
    {
        Read GetMate(Read bamAlignment);
        int? LastClearedPosition { get; }
        int? NextMatePosition { get; }

        int ReadsUnpairable { get; }
        event Action<Read> ReadPurged;
        IEnumerable<Read> GetUnpairedReads();
    }
}
