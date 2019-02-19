using System.Collections.Generic;
using VariantPhasing.Models;

namespace VariantPhasing.Interfaces
{
    public interface IVeadGroupSource
    {
        IEnumerable<VeadGroup> GetVeadGroups(CallableNeighborhood neighborhood);
    }
}