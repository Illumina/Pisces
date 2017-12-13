using System;
using System.Linq;
using Pisces.Domain.Options;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Models
{
    public class SetOfClustersTests
    {
        private Random _random;

        public SetOfClustersTests()
        {
            _random = new Random();
        }

        private SetOfClusters CreateDefaultSetOfClusters()
        {
            var clusteringParams = new ClusteringParameters(){MaxNumberDisagreements = 0, MinNumberAgreements = 0};
            var setOfClusters = new SetOfClusters(clusteringParams);
            Assert.Equal(0, setOfClusters.Clusters.Count());

            return setOfClusters;
        }

        [Fact]
        public void SetOfClusters()
        {
            CreateDefaultSetOfClusters();
        }

        [Fact]
        public void AddCluster()
        {
            var setOfClusters = CreateDefaultSetOfClusters();

            var veadgroups = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster = new Cluster("test", veadgroups);
            setOfClusters.AddCluster(cluster);

            Assert.Equal(1, setOfClusters.Clusters.Count());
        }

        [Fact]
        public void CreateAndAddCluster()
        {
            var setOfClusters = CreateDefaultSetOfClusters();

            var veadgroups = ClusterTestHelpers.GetSampleVeadGroups();
            setOfClusters.CreateAndAddCluster(veadgroups.First());

            Assert.Equal(1, setOfClusters.Clusters.Count());
            Assert.Equal("#1", setOfClusters.Clusters.First().Name);
        }

        [Fact]
        public void RemoveWorstClusters()
        {
            //check it works with zero clusters.

            var setOfClusters = CreateDefaultSetOfClusters();
            Assert.Equal(0, setOfClusters.NumClusters);
            
            int result = setOfClusters.RemoveWorstClusters(0);
            Assert.Equal(0, setOfClusters.NumClusters);
            Assert.Equal(0, result);

            result = setOfClusters.RemoveWorstClusters(2);
            Assert.Equal(0, setOfClusters.NumClusters);
            Assert.Equal(0, result);
            
            //check it works with one cluster

            var veadgroups = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster = new Cluster("test", veadgroups);
            setOfClusters.AddCluster(cluster);

            result = setOfClusters.RemoveWorstClusters(0);
            Assert.Equal(1, setOfClusters.NumClusters);
            Assert.Equal(1, result);


            result = setOfClusters.RemoveWorstClusters(1);
            Assert.Equal(0, setOfClusters.NumClusters);
            Assert.Equal(1, result);

            setOfClusters.AddCluster(cluster);
            Assert.Equal(1, setOfClusters.NumClusters);

            result = setOfClusters.RemoveWorstClusters(2);
            Assert.Equal(0, setOfClusters.NumClusters);
            Assert.Equal(1, result);

            //check it works with two clusters (equal clusters)

            setOfClusters.AddCluster(cluster);
            var clusterSame = new Cluster("same", veadgroups);
            setOfClusters.AddCluster(clusterSame);
            Assert.Equal(2, setOfClusters.NumClusters);

            result = setOfClusters.RemoveWorstClusters(0);
            Assert.Equal(2, setOfClusters.NumClusters);
            Assert.Equal(2, result);


            result = setOfClusters.RemoveWorstClusters(1);
            Assert.Equal(2, setOfClusters.NumClusters);
            Assert.Equal(2, result);

            
            result = setOfClusters.RemoveWorstClusters(2);
            Assert.Equal(0, setOfClusters.NumClusters);
            Assert.Equal(2, result);

            //check it works with two clusters (unequal clusters)
            //So there is one distinct worst cluster (ie, result==1).

            var smallnum_veadgroups = ClusterTestHelpers.GetSampleVeadGroups(numVeads: 2,numVeadGroups:1);
            setOfClusters.AddCluster(cluster);
            var clusterSmall = new Cluster("small", smallnum_veadgroups);
            setOfClusters.AddCluster(clusterSmall);
            Assert.Equal(2, setOfClusters.NumClusters);

            result = setOfClusters.RemoveWorstClusters(0);
            Assert.Equal(2, setOfClusters.NumClusters);
            Assert.Equal(1, result);

            result = setOfClusters.RemoveWorstClusters(1);
            Assert.Equal(1, setOfClusters.NumClusters);
            Assert.Equal(1, result);

            setOfClusters.AddCluster(clusterSmall);
            Assert.Equal(2, setOfClusters.NumClusters);

            result = setOfClusters.RemoveWorstClusters(2);
            Assert.Equal(1, setOfClusters.NumClusters);
            Assert.Equal(1, result);

            //three clusters (one low, two high weighted):
            setOfClusters.AddCluster(clusterSmall);
            setOfClusters.AddCluster(clusterSame);
            Assert.Equal(3, setOfClusters.NumClusters);

            result = setOfClusters.RemoveWorstClusters(0);
            Assert.Equal(3, setOfClusters.NumClusters);
            Assert.Equal(1, result);

            result = setOfClusters.RemoveWorstClusters(1);
            Assert.Equal(2, setOfClusters.NumClusters);
            Assert.Equal(1, result);

            Assert.NotEqual("small", setOfClusters.Clusters[0].Name);
            Assert.NotEqual("small", setOfClusters.Clusters[1].Name);


            //four clusters (two low, two high weighted):
            setOfClusters.AddCluster(clusterSmall);
            var smallnum2_veadgroups = ClusterTestHelpers.GetSampleVeadGroups(numVeads: 1, numVeadGroups: 2);
            var clusterSmall2 = new Cluster("small2", smallnum_veadgroups);
            setOfClusters.AddCluster(clusterSmall2);

            Assert.Equal(4, setOfClusters.NumClusters);

            result = setOfClusters.RemoveWorstClusters(0);
            Assert.Equal(4, setOfClusters.NumClusters);
            Assert.Equal(2, result);

            result = setOfClusters.RemoveWorstClusters(1);
            Assert.Equal(4, setOfClusters.NumClusters);
            Assert.Equal(2, result);

            result = setOfClusters.RemoveWorstClusters(3);
            Assert.Equal(2, setOfClusters.NumClusters);
            Assert.Equal(2, result);

            result = setOfClusters.RemoveWorstClusters(1);
            Assert.Equal(2, setOfClusters.NumClusters);
            Assert.Equal(2, result);

            Assert.NotEqual("small", setOfClusters.Clusters[0].Name);
            Assert.NotEqual("small", setOfClusters.Clusters[1].Name);

            Assert.NotEqual("small2", setOfClusters.Clusters[0].Name);
            Assert.NotEqual("small2", setOfClusters.Clusters[1].Name);

            result = setOfClusters.RemoveWorstClusters(56);
            Assert.Equal(0, setOfClusters.NumClusters);
            Assert.Equal(2, result);

        }

        [Fact]
        public void GetRelativeWeights()
        {
            var setOfClusters = CreateDefaultSetOfClusters();
            var weights = setOfClusters.GetRelativeWeights();
            Assert.Equal(0, weights.Count);

            var veadgroups = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster = new Cluster("test", veadgroups);
            setOfClusters.AddCluster(cluster);

            weights = setOfClusters.GetRelativeWeights();
            Assert.Equal(1, weights.Count);
            Assert.Equal(1, weights["test"]);

            var veadgroups2 = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster2 = new Cluster("test2", veadgroups2);
            setOfClusters.AddCluster(cluster2);

            weights = setOfClusters.GetRelativeWeights();
            Assert.Equal(2, weights.Count);
            Assert.Equal(.5, weights["test"]);
            Assert.Equal(.5, weights["test2"]);
        }

        [Fact]
        public void ReAssignWorstFit()
        {
            var setOfClusters = CreateDefaultSetOfClusters();

            var veadgroups = ClusterTestHelpers.GetSampleVeadGroups(3, 1, prefix: _random.Next().ToString());
            var cluster = new Cluster("test", veadgroups);
            setOfClusters.AddCluster(cluster);
            Assert.Equal(1, setOfClusters.NumClusters);
            Assert.Equal(3, setOfClusters.Clusters.First().NumVeads);


            // Only one cluster: shouldn't do anything.
            setOfClusters.ReAssignWorstFit();
            Assert.Equal(1, setOfClusters.NumClusters);
            Assert.Equal(3, setOfClusters.Clusters.First().NumVeads);

            //TODO this does not yield expected results. See the TODO in SetOfClusters.ReassignWorstFit
            //// If reassigning worst leaves cluster empty, remove that cluster
            //var veadgroups2 = ClusterTestHelpers.GetSampleVeadGroups(1,1, prefix: _random.Next().ToString());
            //var cluster2 = new Cluster("test2", veadgroups2);
            //setOfClusters.AddCluster(cluster2);
            //Assert.Equal(2, setOfClusters.NumClusters);

            //setOfClusters.ReAssignWorstFit();
            //Assert.Equal(1, setOfClusters.NumClusters);
            //Assert.Equal(4, setOfClusters.Clusters.First().NumVeads);


        }

        [Fact]
        public void FindBestClusterFits()
        {
            var setOfClusters = CreateDefaultSetOfClusters();

            var veadgroups = ClusterTestHelpers.GetSampleVeadGroups(prefix: _random.Next().ToString());
            var cluster = new Cluster("test", veadgroups);
            setOfClusters.AddCluster(cluster);

            var veadgroups2 = ClusterTestHelpers.GetSampleVeadGroups(prefix: _random.Next().ToString());
            var cluster2 = new Cluster("test2", veadgroups2);
            setOfClusters.AddCluster(cluster2);

            var matchingVeadGroup = ClusterTestHelpers.GetSampleVeadGroups(useAlternateVariantSites: false,
                prefix: _random.Next().ToString());

            var misMatchingVeadGroup = ClusterTestHelpers.GetSampleVeadGroups(useAlternateVariantSites: true,
                prefix: _random.Next().ToString());

            var bestFits = setOfClusters.GetClusterFits(matchingVeadGroup.First());
            Assert.Equal(1, bestFits.Count);
            Assert.Equal(2, bestFits.Keys.First());
            Assert.Equal(2, bestFits[2].Count);


            //TODO
            var mismatchCluster = new Cluster("mismatch", misMatchingVeadGroup);
            setOfClusters.AddCluster(mismatchCluster);

            bestFits = setOfClusters.GetClusterFits(matchingVeadGroup.First());
            foreach (var fit in bestFits)
            {
                Console.WriteLine("{0}: {1}", fit.Key, fit.Value.Count);
            }

            //Assert.Equal(2, bestFits.Count);


        }

        [Fact]
        public void RemoveCluster()
        {
            var setOfClusters = CreateDefaultSetOfClusters();

            var veadgroups = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster = new Cluster("test", veadgroups);
            setOfClusters.AddCluster(cluster);

            var veadgroups2 = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster2 = new Cluster("test2", veadgroups2);
            setOfClusters.AddCluster(cluster2);

            Assert.Equal(2, setOfClusters.Clusters.Count());

            setOfClusters.RemoveCluster("test");

            var clustersRemaining = setOfClusters.Clusters;

            Assert.Equal(1, clustersRemaining.Count());
            Assert.Equal("test2", clustersRemaining.First().Name);

        }
    }
}
