using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Options;
using Common.IO.Utility;

namespace VariantPhasing.Models
{
    public class SetOfClusters
    {
        private readonly ClusteringParameters _clusterParameters;
        private readonly Dictionary<string, Cluster> _clusterLookup = new Dictionary<string, Cluster>();
        public Cluster[] Clusters
        {
            get { return _clusterLookup.Values.ToArray(); }
        }

        public SetOfClusters(ClusteringParameters clusterParams)
        {
            _clusterParameters = clusterParams;
        }

        public int NumClusters
        {
            get { return _clusterLookup.Count; }
        }


        public void AddCluster(Cluster cluster)
        {
            if (!_clusterLookup.ContainsKey(cluster.Name))
            {
                _clusterLookup.Add(cluster.Name, cluster);
            }
        }

        public void CreateAndAddCluster(VeadGroup veadGroup)
        {
            var clusterName = "#" + (NumClusters + 1);
            var cluster = new Cluster(clusterName, new List<VeadGroup> {veadGroup});
            AddCluster(cluster);
        }

        public int RemoveWorstClusters(int maxNumToRemove)
        {
            Dictionary<string, double> weightsByName = GetRelativeWeights();
            List<double> weights = weightsByName.Values.ToList();
            double lowestWeight = weights.Any() ? weights.Min() : 0;
            List<double> lightWeights = weights.FindAll(x => x == lowestWeight);

            if (lightWeights.Count <= maxNumToRemove)
            {
                foreach (var cluster in Clusters)
                {
                    if (weightsByName[cluster.Name] == lowestWeight)
                    {
                        RemoveCluster(cluster.Name);
                    }
                }
            }
            return lightWeights.Count;
        }
        public Dictionary<string, double> GetRelativeWeights()
        {
            var relativeWeights = new Dictionary<string, double>();

            var totalNumReads = _clusterLookup.Values.Sum(cluster => cluster.NumVeads);
            foreach (var cluster in _clusterLookup.Values)
            {
                var weight = (double) cluster.NumVeads/totalNumReads;
                relativeWeights.Add(cluster.Name, weight);
            }

            return relativeWeights;
        }
  
        public void ReAssignWorstFit()
        {
            //dont try if no reassignment is possible.
            if (NumClusters < 2)
                return;

            var emptyClusters = new List<Cluster>();

            //todo, can also skip clusters that have not changed since the last iteration...

            foreach (var originalCluster in Clusters.OrderBy(x=>x.NumVeads))
            {
                while (true)
                {
                    if (originalCluster.NumVeadGroups == 0) break;
                    var worstVg = originalCluster.GetWorstAgreement();

                    var bestFits = GetClusterFits(worstVg);

                    if (bestFits.Count == 0)
                        break;


                    //TODO
                    //if a read fits in both, equally, test if these two clusters can be merged

                    //TODO GB for expected results, change to Last.
                    var bestFit = bestFits.First().Value[0];

                    //TODO if there is a tie for first, will originalCluster always be first? Or do we bounce back and forth between the two...
                    //if it fits better somewhere else, move it over.
                    if (bestFit != originalCluster)
                    {
                        originalCluster.Remove(worstVg);
                        bestFit.Add(worstVg);

                        if (originalCluster.NumVeadGroups == 0)
                        {
                            emptyClusters.Add(originalCluster);

                            //TODO GB THIS GIVES MY EXPECTED RESULTS
                            //RemoveCluster(originalCluster.Name);
                            //break;
                        }
                    }
                    else
                        break;
                }

            }

            foreach (var cluster in emptyClusters)
                _clusterLookup.Remove(cluster.Name);
        }


        public SortedDictionary<int, List<Cluster>> GetClusterFits(VeadGroup r1)
        {
            var bestFits = new SortedDictionary<int, List<Cluster>>();

            for (var clusterIndex = 0; clusterIndex < NumClusters; clusterIndex++)
            {
                var cluster = Clusters[clusterIndex];

                var bestAgreementWithThiscluster = cluster.GetBestAgreementWithVeadGroup(r1, _clusterParameters.MaxNumberDisagreements);

                //not allowed to connect this read!
                if (!AgreementIsSufficient(bestAgreementWithThiscluster, _clusterParameters.MaxNumberDisagreements, _clusterParameters.MinNumberAgreements))
                    continue;

                if (!bestFits.ContainsKey(bestAgreementWithThiscluster.Score))
                    bestFits.Add(bestAgreementWithThiscluster.Score, new List<Cluster>());

                bestFits[bestAgreementWithThiscluster.Score].Add(cluster);

                if ((bestFits.Count <= 1) && (bestFits[bestAgreementWithThiscluster.Score].Count <= 1)) continue;

                //TODO, when debug enabled.
                /*
                WriteToClusterLog("investigate!!");
                WriteToClusterLog("found a read that can join two clusters!");
                WriteToClusterLog("read group: " + r1.ToString());
                WriteToClusterLog("bestFits.Count: " + bestFits.Count);
                WriteToClusterLog("num best scoring : " + bestFits[bestAgreementWithThiscluster.Score].Count);

                foreach (var c in bestFits[bestAgreementWithThiscluster.Score])
                {
                    WriteToClusterLog("\t" + c.Name);
                }*/
            }

            return bestFits;
        }

        private static bool AgreementIsSufficient(Agreement agreement, int maxNumberDisagreements, int requiredNumAgreements)
        {
            if (agreement == null) return false;

            if (agreement.NumDisagreement > maxNumberDisagreements)
                return false;

            if (agreement.NumAgreement < requiredNumAgreements)
                return false;

            return true;

        }

        private void WriteToClusterLog(string message)
        {
            Logger.WriteToLog(message);
        }

        public void RemoveCluster(string name)
        {
            _clusterLookup.Remove(name);
        }
    }

}
