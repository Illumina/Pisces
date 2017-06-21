using System.Collections.Generic;
using System.Linq;
using VariantPhasing.Helpers;
using VariantPhasing.Models;
using VariantPhasing.Tests.Models;
using Pisces.Domain.Options;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class ClusterMergerTests
    {
        [Fact]
        public void MergeAllBestCandidates()
        {
            var setOfClusters = new SetOfClusters(new ClusteringParameters());
            var veadgroups = ClusterTestHelpers.GetSampleVeadGroups(prefix: "Original");
            var cluster = new Cluster("test", veadgroups);
            setOfClusters.AddCluster(cluster);

            var veadgroups2 = ClusterTestHelpers.GetSampleVeadGroups(prefix: "Second");
            var cluster2 = new Cluster("test2", veadgroups2);
            setOfClusters.AddCluster(cluster2);

            Assert.Equal(2, setOfClusters.NumClusters);
            var veadgroups3 = ClusterTestHelpers.GetSampleVeadGroups(1, 1, prefix: "Tester");

            // If can be merged, we should have 1 cluster.
            var bestCluster = ClusterMerger.MergeAllBestCandidates(setOfClusters, 0, new List<Cluster> { cluster, cluster2 }, veadgroups3.First());
            Assert.Equal(1, setOfClusters.NumClusters);
            
        }
    }
}
