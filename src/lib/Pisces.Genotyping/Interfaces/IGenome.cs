using System;
using System.Collections.Generic;
using Pisces.Domain.Models;

namespace Pisces.Domain.Interfaces
{
    public interface IGenome
    {
        List<string> ChromosomesToProcess { get; }

        IEnumerable<Tuple<string, long>> ChromosomeLengths { get; }

        ChrReference GetChrReference(string chrName);

        string Directory { get; }
    }
}
