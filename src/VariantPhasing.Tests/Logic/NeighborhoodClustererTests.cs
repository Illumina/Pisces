using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class NeighborhoodClustererTests
    {


        [Fact]
        public void ClusterVeadGroups()
        {
            // ----------------------------------------------------
            // Four Ns
            //  - This is from original "FourNs Test"
            // ----------------------------------------------------

            var veads = new List<Vead>()
            {
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r1", new string[2,2]{{"C","C"},{"G","N"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r2", new string[2,2]{{"C","C"},{"G","N"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r3", new string[2,2]{{"C","C"},{"G","N"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r4", new string[2,2]{{"C","C"},{"G","N"}}),
            };

            var veadgroup = PhasedVariantTestUtilities.CreateVeadGroup(veads);
            ExecuteClusteringTest(new List<VeadGroup>() { veadgroup },
                new List<List<VeadGroup>>
                {
                    new List<VeadGroup>{veadgroup}
                }, new List<string>() { "C>C,G>N" }
                , 1);


            // ----------------------------------------------------
            // Real Data
            //  - This data is from Sample 129 (original "Sample129Test")
            // ----------------------------------------------------

            veads = new List<Vead>()
            {
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r1", new string[2,2]{{"A","G"},{"N","N"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r2", new string[2,2]{{"A","G"},{"C","C"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r3", new string[2,2]{{"A","A"},{"C","C"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r4", new string[2,2]{{"A","G"},{"C","A"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r5", new string[2,2]{{"N","N"},{"C","C"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r6", new string[2,2]{{"N","N"},{"C","A"}}),
            };

            var group1 = new VeadGroup(PhasedVariantTestUtilities.CreateVeadFromStringArray("r1", new string[2, 2] { { "A", "G" }, { "N", "N" } }));
            var group4 = new VeadGroup(PhasedVariantTestUtilities.CreateVeadFromStringArray("r4", new string[2, 2] { { "A", "G" }, { "C", "A" } }));
            var group6 = new VeadGroup(PhasedVariantTestUtilities.CreateVeadFromStringArray("r6", new string[2, 2] { { "N", "N" }, { "C", "A" } }));

            var group2 = new VeadGroup(PhasedVariantTestUtilities.CreateVeadFromStringArray("r2", new string[2, 2] { { "A", "G" }, { "C", "C" } }));

            var group3 = new VeadGroup(PhasedVariantTestUtilities.CreateVeadFromStringArray("r3", new string[2, 2] { { "A", "A" }, { "C", "C" } }));
            var group5 = new VeadGroup(PhasedVariantTestUtilities.CreateVeadFromStringArray("r5", new string[2, 2] { { "N", "N" }, { "C", "C" } }));

            ExecuteClusteringTest(new List<VeadGroup>() { group1, group2, group3, group4, group5, group6 },
                new List<List<VeadGroup>>
                {
                    new List<VeadGroup>{group4, group6, group1},
                    new List<VeadGroup>{group3, group5},
                    new List<VeadGroup>{group2},
                },
                new List<string>() { "A>G,C>A", "A>G,C>C", "A>A,C>C" }
                , 1, 0);

            // ----------------------------------------------------
            // Ten grouped reads
            //  - This is from original "10 ReadsTest"
            // ----------------------------------------------------

            group1 = PhasedVariantTestUtilities.CreateVeadGroup(new List<Vead>
            {
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r1", new string[6,2]{{"N","N"},{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"C","A"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r2", new string[6,2]{{"N","N"},{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"C","A"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r5", new string[6,2]{{"N","N"},{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"C","A"}}),
            });
            group2 = PhasedVariantTestUtilities.CreateVeadGroup(new List<Vead>
            {
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r3", new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r4", new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r7", new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r8", new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r9", new string[6,2]{{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
            });
            group3 = PhasedVariantTestUtilities.CreateVeadGroup(new List<Vead>
            {
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r10", new string[6,2]{{"C","A"},{"C","A"},{"C","A"},{"C","A"},{"N","N"},{"C","A"}}),
            });
            group4 = PhasedVariantTestUtilities.CreateVeadGroup(new List<Vead>
            {
                PhasedVariantTestUtilities.CreateVeadFromStringArray("r6", new string[6,2]{{"C","C"},{"C","C"},{"C","C"},{"C","C"},{"C","C"},{"C","C"}}),
            });

            ExecuteClusteringTest(new List<VeadGroup>() { group1, group2, group3, group4 },
    new List<List<VeadGroup>>
    {
        new List<VeadGroup>{group1},
        new List<VeadGroup>{group2, group3},
        new List<VeadGroup>{group4},
    }
    , new List<string>() { "N>N,N>N,C>A,C>A,C>A,C>A", "C>A,C>A,C>A,C>A,N>N,C>A", "C>C,C>C,C>C,C>C,C>C,C>C" }, 4, 0);


            ExecuteClusteringTest(new List<VeadGroup>() { group1, group2, group3, group4 },
    new List<List<VeadGroup>>
    {
        new List<VeadGroup>{group1},
        new List<VeadGroup>{group2, group3},
        new List<VeadGroup>{group4},
    }
    , new List<string>() { "N>N,N>N,C>A,C>A,C>A,C>A", "C>A,C>A,C>A,C>A,N>N,C>A", "C>C,C>C,C>C,C>C,C>C,C>C" }, 4, 0,
    ploidyConstraint: 3);


            ExecuteClusteringTest(new List<VeadGroup>() { group1, group2, group3, group4 },
 new List<List<VeadGroup>>
 {
        new List<VeadGroup>{group1},  // 6 reads
        new List<VeadGroup>{group2, group3}, //3 reads
        //new List<VeadGroup>{group4}, //1 reads -> the looser
 }
 , new List<string>() { "N>N,N>N,C>A,C>A,C>A,C>A", "C>A,C>A,C>A,C>A,N>N,C>A", "C>C,C>C,C>C,C>C,C>C,C>C" }, 4, 0,
 ploidyConstraint: 2);

            ExecuteClusteringTest(new List<VeadGroup>() { group1, group2, group3, group4 },
new List<List<VeadGroup>>
{
        new List<VeadGroup>{group1},  // 6 reads -> the winner
}
, new List<string>() { "N>N,N>N,C>A,C>A,C>A,C>A", "C>A,C>A,C>A,C>A,N>N,C>A", "C>C,C>C,C>C,C>C,C>C,C>C" }, 4, 0,
ploidyConstraint: 1);
        }


        private void ExecuteClusteringTest(List<VeadGroup> groups, List<List<VeadGroup>> expectedClusters, List<string> consensusSites, int? minNumAgreements = null, int? maxNumDisagreements = null, int ploidyConstraint = -1)
        {
            var options = new ClusteringParameters();
            if (minNumAgreements!=null) options.MinNumberAgreements = (int)minNumAgreements;
            if (maxNumDisagreements != null) options.MaxNumberDisagreements = (int)maxNumDisagreements;
            options.ClusterConstraint = ploidyConstraint;

            var clusterer = new NeighborhoodClusterer(options);
            var clusterSet = clusterer.ClusterVeadGroups(groups);

            Assert.Equal(expectedClusters.Count, clusterSet.NumClusters);
            foreach (var cluster in clusterSet.Clusters)
            {
                var clusterConsensus = string.Join(",", cluster.GetConsensusSites().ToList());
                Assert.Equal(1, consensusSites.Count(c=>c == clusterConsensus));
            }
        }
    }
}
