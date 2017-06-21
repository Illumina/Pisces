using System;
using System.Collections.Generic;
using System.Linq;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Models
{
    public static class ClusterTestHelpers
    {
        public static List<VeadGroup> GetSampleVeadGroups(int numVeads = 4, int numVeadGroups = 1, bool useAlternateVariantSites = false, string prefix = "")
        {
            var veadgroups = new List<VeadGroup>();

            for (int i = 0; i < numVeadGroups; i++)
            {
                var veads = new List<Vead>();

                for (int j = 0; j < numVeads; j++)
                {
                    Vead vead;
                    if (useAlternateVariantSites)
                    {
                        vead = PhasedVariantTestUtilities.CreateVeadFromStringArray(prefix + "r" + i * j, new[,] { { "C", "C" }, { "G", "A" } });
                    }
                    else
                    {
                        vead = PhasedVariantTestUtilities.CreateVeadFromStringArray(prefix + "r" + i * j, new[,] { { "A", "T" }, { "G", "C" } });
                    }
                    veads.Add(vead);
                }

                veadgroups.Add(PhasedVariantTestUtilities.CreateVeadGroup(veads));
            }

            return veadgroups;
        }

    }
    public class ClusterTests
    {
        private readonly Random _random;

        public ClusterTests()
        {
            _random = new Random();
        }

        [Fact]
        public void Cluster()
        {
            var veadgroups = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster = new Cluster("test", veadgroups);
            Assert.Equal("test", cluster.Name);
        }

        [Fact]
        public void GetVeadGroups()
        {
            var veadGroups = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster = new Cluster("test", veadGroups);
            Assert.Equal(1, cluster.GetVeadGroups().Count);
        }

        [Fact]
        public void AddFromSingle()
        {
            var initialVeadGroups = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster = new Cluster("test", initialVeadGroups);
            cluster.Add(ClusterTestHelpers.GetSampleVeadGroups(3).First());
            Assert.Equal(2, cluster.GetVeadGroups().Count);
        }

        [Fact]
        public void AddFromList()
        {
            var initialVeadGroups = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster = new Cluster("test", initialVeadGroups);
            cluster.Add(ClusterTestHelpers.GetSampleVeadGroups(3,2));
            Assert.Equal(3, cluster.GetVeadGroups().Count);
        }

        [Fact]
        public void Remove()
        {
            var initialVeadGroups = ClusterTestHelpers.GetSampleVeadGroups(3,3);
            var cluster = new Cluster("test", initialVeadGroups);
            Assert.Equal(3, cluster.GetVeadGroups().Count);
            cluster.Remove(initialVeadGroups.First());
            Assert.Equal(2, cluster.GetVeadGroups().Count);
        }

        [Fact]
        public void GetVeadCountsInCluster()
        {
            var initialVeadGroups = ClusterTestHelpers.GetSampleVeadGroups(3, 3);
            var cluster = new Cluster("test", initialVeadGroups);
            var variantSite = new VariantSite(0){VcfReferenceAllele = "A", VcfAlternateAllele = "T"};
            var variantSite2 = new VariantSite(0) { VcfReferenceAllele = "A", VcfAlternateAllele = "C" };
            var sites = new List<VariantSite>() { variantSite, variantSite2 };

            Assert.Equal(9, cluster.GetVeadCountsInCluster(sites)[variantSite]);
            Assert.Equal(0, cluster.GetVeadCountsInCluster(sites)[variantSite2]);
        }

        [Fact]
        public void ResetConsensus()
        {
            var initialVeadGroups = ClusterTestHelpers.GetSampleVeadGroups(3, 3);
            var cluster = new Cluster("test", initialVeadGroups);
            Assert.Equal(3, cluster.NumVeadGroups);
            Assert.Equal(9, cluster.NumVeads);
            Assert.Equal(2, cluster.GetConsensusSites().Count());

            foreach (var initialVeadGroup in initialVeadGroups.ToList())
            {
                cluster.Remove(initialVeadGroup);
            }
            Assert.Equal(0, cluster.NumVeadGroups);
            Assert.Equal(0, cluster.NumVeads);
            Assert.Equal(0, cluster.GetConsensusSites().Count());
        }

        [Fact]
        public void GetWorstAgreement()
        {
            var initialVeadGroups = ClusterTestHelpers.GetSampleVeadGroups(3, 1);
            var matchVeadGroups = ClusterTestHelpers.GetSampleVeadGroups(3, 1);
            var nonMatchVeadGroups = ClusterTestHelpers.GetSampleVeadGroups(3, 1, true);

            var cluster = new Cluster("test", initialVeadGroups);

            // If there's only one veadgroup, that will by default be the "worst"
            var worst = cluster.GetWorstAgreement();
            Assert.Equal(initialVeadGroups.First(), worst);

            // This is a tie, they're both the same, first will go
            cluster.Add(matchVeadGroups);
            worst = cluster.GetWorstAgreement();
            Assert.Equal(initialVeadGroups.First(), worst);

            // The non-matching group should be the worst agreement
            cluster.Add(nonMatchVeadGroups);
            worst = cluster.GetWorstAgreement();
            Assert.Equal(nonMatchVeadGroups.First(), worst);
        }

        [Fact]
        public void GetBestAgreement()
        {
            var initialVeadGroups = ClusterTestHelpers.GetSampleVeadGroups(3, 1);
            var matchVeadGroups = ClusterTestHelpers.GetSampleVeadGroups(3, 1, false, _random.Next().ToString());
            var nonMatchVeadGroups = ClusterTestHelpers.GetSampleVeadGroups(3, 1, true, _random.Next().ToString());

            var cluster = new Cluster("test", initialVeadGroups);
            var bestAgreement = cluster.GetBestAgreementWithVeadGroup(matchVeadGroups.First(), 0);
            Assert.Equal(0, bestAgreement.NumDisagreement);
            Assert.Equal(2, bestAgreement.NumAgreement);

            // Since we're starting with default Agreement (0,0), will never get a bestAgreement worse than that...
            bestAgreement = cluster.GetBestAgreementWithVeadGroup(nonMatchVeadGroups.First(), 5);
            //Assert.Equal(2, bestAgreement.NumDisagreement);
            Assert.Equal(0, bestAgreement.NumDisagreement);
            Assert.Equal(0, bestAgreement.NumAgreement);

            // If we're over the max num disagreements, return null
            bestAgreement = cluster.GetBestAgreementWithVeadGroup(nonMatchVeadGroups.First(), 1);
            Assert.True(bestAgreement == null);

        }

        [Fact]
        public void SetCountsAtConsensusSites()
        {
            var initialVeadGroups = ClusterTestHelpers.GetSampleVeadGroups();
            var cluster = new Cluster("test", initialVeadGroups);
            Assert.Equal(1, cluster.GetVeadGroups().Count);
            Assert.Equal(2, cluster.GetConsensusSites().Count());
 
            // CountsAtSites should be same length as GetConsensusSites
            Assert.Equal(2, cluster.CountsAtSites.Length);
            Assert.Equal(new List<int>(){4,4}, cluster.CountsAtSites);
        }

    }
}
