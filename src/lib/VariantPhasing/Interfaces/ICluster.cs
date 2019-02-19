using System.Collections.Generic;
using VariantPhasing.Models;

namespace VariantPhasing.Interfaces
{
    public interface ICluster
    {
        string Name { get; }
        List<VeadGroup> GetVeadGroups();
        int[] CountsAtSites { get; }
        VariantSite[] GetConsensusSites();
        int GetClusterReferenceSupport(IEnumerable<ICluster> clusters);
    }
}