using System.Collections.Generic;
using System.Linq;
using VariantPhasing.Models;

namespace VariantPhasing.Logic
{
    public static class VariantPhaser
    {
        public static Dictionary<VariantSite, VariantPhasingResult> GetPhasingProbabilities(List<VariantSite> variantSites, SetOfClusters clusters)
        {
            var results = new Dictionary<VariantSite, VariantPhasingResult>();

            foreach (var variantSite in variantSites)
            {
                results.Add(variantSite, GetPhasingProbabilitiesForVariant(variantSites.ToList(), clusters, variantSite));
            }

            //TODO debug Clusters: (VariantGroup, Chr, Pos, Ref, Alt); Neighbors (VariantGroup, Chr, Pos, Ref, Alt, ProbOfAGivenB, ProbOfB, ProbOfAAndB)

            return results;
        }

        private static VariantPhasingResult GetPhasingProbabilitiesForVariant(List<VariantSite> variantGroup, SetOfClusters clusters, VariantSite variantSiteA)
        {
            var otherVariants = variantGroup.Where(vs => vs != variantSiteA).ToList();

            var phasingResult = new VariantPhasingResult(variantSiteA, otherVariants, clusters.NumClusters);

            var relativeWeights = clusters.GetRelativeWeights();

            //how many clusters have B in them
            //how many clusters have A and B in them?

            foreach (var cluster in clusters.Clusters)
            {
                var supportDict = cluster.GetVeadCountsInCluster(variantGroup);

                var weight = relativeWeights[cluster.Name];

                foreach (var variantSiteB in otherVariants)
                {
                    if (supportDict[variantSiteB] <= 0) continue;
                    phasingResult.AddSupportForB(variantSiteB, weight);


                    if (supportDict[variantSiteA] > 0)
                    {
                        phasingResult.AddSupportForAandB(variantSiteB, weight);
                    }
                }
            }
            return phasingResult;
        }
    }
}
