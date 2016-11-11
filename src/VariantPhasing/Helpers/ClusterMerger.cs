using System.Collections.Generic;
using System.Linq;
using VariantPhasing.Interfaces;
using VariantPhasing.Models;

namespace VariantPhasing.Helpers
{
    public static class ClusterMerger
    {
        public static void TryMergeAllClusters(SetOfClusters clusterSet, int maxNumDisagreements)
        {
            var clusters = clusterSet.Clusters;
            for (var i = 0; i < clusters.Count(); i++)
            {
                for (var j = i + 1; j < clusters.Count(); j++)
                {
                    var clusterA = clusters[i];
                    var clusterB = clusters[j];

                    var canBeMerged = TestCanBeMerged(clusterA, clusterB, maxNumDisagreements);

                    if (!canBeMerged) continue;

                    clusterSet.RemoveCluster(clusterA.Name);
                    clusterSet.RemoveCluster(clusterB.Name);

                    var mergedCluster = MergeClusters(clusterA, clusterB);
                    clusterSet.AddCluster(mergedCluster);
                }
            }
        }
 
        public static Cluster MergeAllBestCandidates(SetOfClusters clusters, int maxNumDisagreements, 
            List<Cluster> bestCandidates, VeadGroup testVeadGroup)
        {
            var numCandidates = bestCandidates.Count;
            var bestcluster = bestCandidates[0];

            for (var i = 0; i < numCandidates; i++)
            {
                for (var j = i + 1; j < numCandidates; j++)
                {
                    // First test if the clusters can be merged. If they can, merge them. 
                    // If they can't, return the better of the two.
                    var clusterA = bestCandidates[i];
                    var clusterB = bestCandidates[j];

                    var canBeMerged = TestCanBeMerged(clusterA, clusterB, maxNumDisagreements, testVeadGroup);

                    if (canBeMerged)
                    {
                        clusters.RemoveCluster(clusterA.Name);
                        clusters.RemoveCluster(clusterB.Name);

                        var mergedCluster = MergeClusters(clusterA, clusterB);
                        clusters.AddCluster(mergedCluster);
                        bestcluster = mergedCluster;
                    }
                    else
                    {
                        if (clusterB.NumVeads > clusterA.NumVeads)
                            bestcluster = clusterB;
                        //else, leave it as      bestcluster = cA;
                    }


                }
            }
            return bestcluster;
        }


        private static bool TestCanBeMerged(ICluster clusterA, ICluster clusterB, int maxNumDisagreements, VeadGroup veadGroupC = null)
        {
            var vgToCheck = new List<VeadGroup>();
            vgToCheck.AddRange(clusterA.GetVeadGroups());
            vgToCheck.AddRange(clusterB.GetVeadGroups());
            if (veadGroupC!=null) vgToCheck.Add(veadGroupC);

            var worstAgreement = VeadGroup.GetWorstAgreement(vgToCheck);
            var tooManyDisagreements = (worstAgreement.NumDisagreement > maxNumDisagreements);
           
            //note we already know the num agreements are OK, b/c vC has an acceptable num agreements with each cluster
            //before we got into this method

            return (!tooManyDisagreements);

        }

        private static Cluster MergeClusters(Cluster cA, Cluster cB)
        {          
            cA.Name = cA.Name + "_" + cB.Name;
            cA.Add(cB.GetVeadGroups(), false);
            cA.ResetConsensus();

            return cA;

        }

    }
}