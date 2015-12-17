using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CallSomaticVariants.Models;

namespace CallSomaticVariants.Interfaces
{
    public interface IGenome
    {
        List<string> ChromosomesToProcess { get; }

        IEnumerable<Tuple<string, long>> ChromosomeLengths { get; }

        ChrReference GetChrReference(string chrName);

        string Directory { get; }
    }
}
