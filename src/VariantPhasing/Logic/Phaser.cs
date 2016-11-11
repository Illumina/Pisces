using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pisces.Processing.Logic;
using Pisces.Processing.Utility;
using Pisces.Domain.Models.Alleles;
using VariantPhasing.Models;

namespace VariantPhasing.Logic
{
    public class Phaser : BaseProcessor
    {
        private readonly Factory _factory;

        public Phaser(Factory factory)
        {
            _factory = factory;
        }

        public override void InternalExecute(int maxThreads)
        {
            var startTime = DateTime.UtcNow;
            Logger.WriteToLog("Start processing.");

            // Package proximal variants into neighborhoods
            Logger.WriteToLog("Building neighborhoods.");
            var neighborhoods = _factory.CreateNeighborhoodBuilder().GetNeighborhoods();

            Logger.WriteToLog(string.Format("Neighborhood building complete. {0} neighborhoods created.",
                neighborhoods.Count()));

            //Then process each neighborhood separately

            var jobManager = new JobManager(maxThreads);

            var jobs = new List<IJob>();

            foreach (var vcfNeighborhood in neighborhoods)
            {

                if (!string.IsNullOrEmpty(_factory.FilteredNbhd) && (_factory.FilteredNbhd != vcfNeighborhood.Id))
                    continue;

                Logger.WriteToLog("Creating Neighborhood: {0}", vcfNeighborhood.Id);


                var clusterer = _factory.CreateNeighborhoodClusterer();
                var veadGroupSource = _factory.CreateVeadGroupSource();
                var collapsedReads = veadGroupSource.GetVeadGroups(vcfNeighborhood);

                if (_factory.DebugMode)
                {
                    Logger.WriteToLog("variant-compressed read groups as follows:  ");
                    Logger.WriteToLog("count" + "\t", collapsedReads.First().ToPositions());
                    foreach (var vG in collapsedReads)
                    {
                        Logger.WriteToLog("\t" + vG.NumVeads + "\t" + vG);
                    }
                }

                Logger.WriteToLog("Found " + collapsedReads.Count() + " variant-collapsed read groups.");

                if (_factory.DebugMode)
                {
                    StringBuilder sb = new StringBuilder();
                    int[] depths = VeadGroup.DepthAtSites(collapsedReads);
                    Logger.WriteToLog("depth at sites:  ");
                    Logger.WriteToLog(collapsedReads.First().ToPositions());
                    Logger.WriteToLog(string.Join("\t", depths));
                }

                jobs.Add(new GenericJob(() => ProcessNeighborhood(vcfNeighborhood, clusterer, collapsedReads)));

            }

            jobManager.Process(jobs);

            // Finally, come back and combine the information for results
            //  Add back everything that wasn't sucked up into MNVs
            //  Some adjustment for refs in MNVs or something
            //  Write VCF


            //(3) write results             

            var variantCaller = _factory.CreateVariantCaller();
            var variantMerger = _factory.CreateVariantMerger();
           // var nbhds = neighborhoods.ToArray();

            using (var writer = _factory.CreatePhasedVcfWriter())
            {

                Logger.WriteToLog("Writing phased vcf.");
                writer.WriteHeader();

               //do this in chunks to avoid having all variants in memory at all times.
               var originalAllelesTrailingNeighbhood = new List<CalledAllele>();
                foreach(var nbhd in neighborhoods)
                {
                    Logger.WriteToLog("Writing original variants up to neighborhood " + nbhd.Id);
                    originalAllelesTrailingNeighbhood = variantMerger.WriteVariantsUptoChr(writer, originalAllelesTrailingNeighbhood , nbhd.ReferenceName);

                    Logger.WriteToLog("Writing phased variants inside neighborhood " + nbhd.Id);
                    variantCaller.CallMNVs(nbhd);
                    variantCaller.CallRefs(nbhd);
                    originalAllelesTrailingNeighbhood  = variantMerger.WriteVariantsUptoIncludingNbhd(nbhd,
                        writer,  originalAllelesTrailingNeighbhood);
                }

                Logger.WriteToLog("Writing variants past last neighborhood");

                variantMerger.WriteRemainingVariants(writer, originalAllelesTrailingNeighbhood);
           
            }

            Logger.WriteToLog("Completed processing in {0}s.",
                DateTime.UtcNow.Subtract(startTime).TotalSeconds);
        }

        private void ProcessNeighborhood(VcfNeighborhood neighborhood, NeighborhoodClusterer clusterer, IEnumerable<VeadGroup> collapsedReads)
        {
            Logger.WriteToLog("Processing Neighborhood {0}.", neighborhood.Id);

            try
            {
                var clusters = clusterer.ClusterVeadGroups(collapsedReads.ToList());

                if (clusters != null)
                {
                    Logger.WriteToLog("Found " + clusters.Clusters.Length + " clusters in Nbhd " + neighborhood.Id);

                    //tjd+
                    //Commenting out for speed. We currently never use these results
                    //neighborhood.PhasingProbabiltiies =
                    //   VariantPhaser.GetPhasingProbabilities(neighborhood.VcfVariantSites, clusters);
                    //tjd-
                }

                bool crushNbhdVariantsToSamePositon = !_factory.Options.VcfWritingParams.AllowMultipleVcfLinesPerLoci;

                neighborhood.AddMnvsFromClusters(clusters.Clusters, 
                    _factory.Options.BamFilterParams.MinimumBaseCallQuality, _factory.Options.VariantCallingParams.MaximumVariantQScore,
                    crushNbhdVariantsToSamePositon);
                neighborhood.SetGenotypesAndPruneExcessAlleles();
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("Error processing neighborhood {0}", neighborhood.Id);
                Logger.WriteExceptionToLog(ex);
            }

        }
    }
}