using System.Collections.Generic;
using VariantPhasing.Models;

namespace VariantPhasing.Interfaces
{
    public interface INeighborhoodBuilder
    {
        IEnumerable<CallableNeighborhood> GetBatchOfCallableNeighborhoods(int numSoFar);
    }
}