using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;

namespace Alignment.IO
{
    public interface IReadPairHandler
    {
        List<BamAlignment> ExtractReads(ReadPair pair);
    }
}