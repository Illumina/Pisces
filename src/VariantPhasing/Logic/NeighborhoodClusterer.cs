using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Processing.Utility;
using VariantPhasing.Helpers;
using VariantPhasing.Models;

namespace VariantPhasing.Logic
{
    public class NeighborhoodClusterer
    {
        private readonly ClusteringParameters _options;
        private readonly bool _debug;

        public NeighborhoodClusterer(ClusteringParameters options, bool debug)
        {
            _options = options;
            _debug = debug;
        }

        public NeighborhoodClusterer(ClusteringParameters options)
        {
            _options = options;
        }

        public SetOfClusters ClusterVeadGroups(List<VeadGroup> veadGroups)
        {
            try
            {
                // Make the meatiest clusters first.
                veadGroups.Sort();

                if (veadGroups.Count == 0)
                {
                    throw new ApplicationException("No vead groups given to clustering algorithm.");
                }

                var maxNumNewClusters = veadGroups[0].SiteResults.Length * _options.MaxNumNewClustersPerSite;
                var clusters = new SetOfClusters(_options);


                var nbhdStart = veadGroups[0].SiteResults[0].VcfReferencePosition;
                var nbhdEnd = veadGroups[0].SiteResults[veadGroups[0].SiteResults.Length - 1].VcfReferencePosition;

                Logger.WriteToLog("Maximum num new clusters for this nbhd is " + maxNumNewClusters);
                Logger.WriteToLog("There are {0} variant sites in this nbhd, from position {1} to {2}", veadGroups[0].SiteResults.Length, nbhdStart, nbhdEnd);

                if (_debug)
                {
                    Logger.WriteToLog("variant-compressed read groups as follows:  ");
                    Logger.WriteToLog("count" + "\t", veadGroups[0].ToPositions());
                    foreach (var vG in veadGroups)
                    {
                        Logger.WriteToLog("\t" + vG.NumVeads + "\t" + vG);
                    }
                }

                int outerIteration = 0;
                while (veadGroups.Count > 0)
                {
                    Logger.WriteToLog("ITER: {0}.{1}\tNum clusters: {2} \tUnassigned read groups: {3}", outerIteration, 0, clusters.NumClusters, veadGroups.Count);
                    CreateNewCluster(veadGroups, clusters);

                    // Is there any poor-fitting read that should go somewhere else?
                    if (_options.AllowWorstFitRemoval)
                        clusters.ReAssignWorstFit();

                    //TODO log the contents of clusterSet (previously "PrintContents()").
                    const int maxNumReallocationIterations = 10;
                    var reallocationIterationNumber = 1;

                    // Keep trying to allocate free agents to clusters if possible, then tell how many we have left free.
                    // If we haven't allocated any in the last round, give up and break off a new cluster.
                    while (veadGroups.Count > 0)
                    {
                        var initialReadsLeft = veadGroups.Count;
                        Logger.WriteToLog("ITER: {0}.{1}\tNum clusters: {2} \tUnassigned read groups: {3}", outerIteration, reallocationIterationNumber, clusters.NumClusters, initialReadsLeft);

                        // Find the best existing cluster for each free agent. Add the free agent to that cluster if it exists. Return anyone left free.
                        veadGroups = AllocateReadsToClusters(veadGroups, clusters,
                            _options.MaxNumberDisagreements);

                        //TODO log the contents of clusterSet (previously "PrintContents()").

                        if (veadGroups.Count == initialReadsLeft)
                        {
                            break; // Reallocation didn't do any good. Give up.

                        }
                        reallocationIterationNumber++;
                        if (reallocationIterationNumber > maxNumReallocationIterations)
                            break;
                    }

                    if (clusters.NumClusters > maxNumNewClusters)
                        break;

                    outerIteration++;
                }

                Logger.WriteToLog("ITER: {0}.{1}\tNum clusters: {2} \tUnassigned read groups: {3}", outerIteration, 0, clusters.NumClusters, veadGroups.Count);

                if (clusters != null)
                {
                    Logger.WriteToLog("Found " + clusters.Clusters.Length + " clusters.");

                    if (_options.ClusterConstraint > 0)
                    {
                        MeetPloidyConstraints(clusters);
                    }

                }

                return clusters;
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("Clustering issue.", ex);
                throw;
            }
        }

        private void CreateNewCluster(List<VeadGroup> vgs, SetOfClusters clusters)
        {
            clusters.CreateAndAddCluster(vgs[0]);
            vgs.Remove(vgs[0]);
        }

        private void MeetPloidyConstraints(SetOfClusters clusters)
        {
            while (clusters.NumClusters > _options.ClusterConstraint)
            {
                Logger.WriteToLog("Num clusters: " + clusters.Clusters.Length);
                Logger.WriteToLog("Num cluster constraint " + _options.ClusterConstraint + " is violated.  Pruning clusters...");
                int maxAllowedToRemove = (clusters.NumClusters - _options.ClusterConstraint);
                int numWorstClusters = clusters.RemoveWorstClusters(maxAllowedToRemove);

                if (numWorstClusters <= maxAllowedToRemove)
                {
                    Logger.WriteToLog(numWorstClusters + " clusters pruned.");

                }
                else
                {
                    Logger.WriteToLog(numWorstClusters + " low ranked clusters found. This is not resolveable with our cluster constraints.");
                    // then we had a tie situation, and we do not know how to proceed.
                    //if this is not resolvable, we are going to fail our cluster constraint.
                    break;
                }
            }
            Logger.WriteToLog("Clusters finalized: " + clusters.Clusters.Length);
        }

        private List<VeadGroup> AllocateReadsToClusters(List<VeadGroup> veadGroups, SetOfClusters clusters, int maxNumDisagreements)
        {
            var vgsRemaining = new List<VeadGroup>();

            foreach (var vg in veadGroups)
            {
                var bestFits = clusters.GetClusterFits(vg);

                if (bestFits.Count == 0)
                    vgsRemaining.Add(vg);
                else
                {
                    var bestCandidates = bestFits.Last().Value;
                    var bestcluster = bestCandidates[0];

                    if (_options.AllowClusterMerging)
                    {
                        bestcluster = ClusterMerger.MergeAllBestCandidates(clusters, maxNumDisagreements, bestCandidates, vg);
                    }

                    bestcluster.Add(vg);
                }
            }

            return vgsRemaining;
        }
    }
}