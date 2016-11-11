using System.Collections.Generic;
using VariantPhasing.Models;

namespace VariantPhasing.Interfaces
{
    public interface IVariantSource
    {
        IEnumerable<VariantSite> GetPhasableVariants();
    }
}
