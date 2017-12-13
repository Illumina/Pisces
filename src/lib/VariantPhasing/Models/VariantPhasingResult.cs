using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace VariantPhasing.Models
{
    public class VariantPhasingResult
    {
        private readonly int _totalNumClusters;
        private readonly Dictionary<VariantSite, double> _supportOfB;
        private readonly Dictionary<VariantSite, double> _supportOfAAndB;
        private readonly Dictionary<VariantSite, double> _weightedSupportOfB;
        private readonly Dictionary<VariantSite, double> _weightedSupportOfAAndB;

        public VariantSite VariantA;
        private const double ApproximatelyZero = 0.00001;

        public VariantPhasingResult(VariantSite vsA, IEnumerable<VariantSite> variantGroup, int totalNumClusters)
        {
            VariantA = vsA;
            _totalNumClusters = totalNumClusters;

            // Initialize counters
            _supportOfB = new Dictionary<VariantSite, double>();
            _supportOfAAndB = new Dictionary<VariantSite, double>();
            _weightedSupportOfB = new Dictionary<VariantSite, double>();
            _weightedSupportOfAAndB = new Dictionary<VariantSite, double>();

            foreach (var vsB in variantGroup)
            {
                // Initialize support counters for "other" ("B") variants
                _supportOfB.Add(vsB, 0);
                _supportOfAAndB.Add(vsB, 0);
                _weightedSupportOfB.Add(vsB, 0);
                _weightedSupportOfAAndB.Add(vsB, 0);
            }
        }

        public void AddSupportForB(VariantSite site, double support)
        {
            AddSupport(_supportOfB, site, 1);
            AddSupport(_weightedSupportOfB, site, support);
        }

        public void AddSupportForAandB(VariantSite site, double support)
        {
            AddSupport(_supportOfAAndB, site, 1);
            AddSupport(_weightedSupportOfAAndB, site, support);
        }

        public double GetProbOfAGivenB(VariantSite site)
        {
            //what is chance of A given B
            //P(A|B) = P (A in a cluster with with B ) / P (B)
            //so...
            //P(B) = #Bs / total reads
            //P(A in a cluster with with B ) = #Bs with As / total reads.

            CheckVariantSiteTracked(site);

            var probOfAandB = CalculateProbability(_supportOfAAndB[site]);
            var probOfB = CalculateProbability(_supportOfB[site]);

            Console.WriteLine("Prob of A and B: "+probOfAandB);
            Console.WriteLine("Prob of B: " + probOfB);

            var probOfAGivenB = probOfAandB / probOfB;

            if (probOfB < ApproximatelyZero)
                probOfAGivenB = 0;

            return probOfAGivenB;
        }

        public double GetWeightedProbOfAGivenB(VariantSite site)
        {
            CheckVariantSiteTracked(site);

            var weightedProbOfB = CalculateProbability(_weightedSupportOfB[site]);
            var weightedProbOfAandB = CalculateProbability(_weightedSupportOfAAndB[site]);

            var weightedProbOfAGivenB = weightedProbOfAandB / weightedProbOfB;

            if (weightedProbOfB < ApproximatelyZero)
                weightedProbOfAGivenB = 0;

            return weightedProbOfAGivenB;            
        }

        private void CheckVariantSiteTracked(VariantSite site)
        {
            if (!_supportOfB.ContainsKey(site))
            {
                throw new InvalidDataException(
                    string.Format(
                        "Support for VariantSite '{0}' is not being tracked in relation to VariantSite '{1}'", site,
                        VariantA));
            }    
        }

        private static void AddSupport(Dictionary<VariantSite, double> dictionary, VariantSite site, double amount)
        {
            if (!dictionary.ContainsKey(site))
            {
                dictionary.Add(site, 0);
            }
            dictionary[site] += amount;
        }

        private double CalculateProbability(double count)
        {
            return count/_totalNumClusters;
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
    }
}