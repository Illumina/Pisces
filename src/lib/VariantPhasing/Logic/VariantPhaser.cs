using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Processing.Logic;
using Pisces.Processing.Utility;
using VariantPhasing.Models;
using Common.IO.Utility;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;

namespace VariantPhasing.Logic
{
    public class VariantPhaser : BaseProcessor
    {
        private readonly Factory _factory;
        PhasableVariantCriteria _phasableVariantCriteria = new PhasableVariantCriteria();
        private int _batchSize;

        public VariantPhaser(Factory factory)
        {
            _factory = factory;
            _batchSize = factory.Options.NumThreads;
        }

        public override void InternalExecute(int maxThreads)
        {
            var startTime = DateTime.UtcNow;
            Logger.WriteToLog("Start processing.");
            Logger.WriteToLog("Building neighborhoods.");


            var nbhdBuilder = _factory.CreateNeighborhoodBuilder(_batchSize);
            var variantMerger = _factory.CreateVariantMerger();

            bool needToMakeMoreNbhds = true;
            int numNbhdsSoFar = 0;    //just used to set the nbhd id ##
            var originalAllelesTrailingNeighbhood = new List<CalledAllele>();

            using (var phasedVcfWriter = _factory.CreatePhasedVcfWriter())
            {
                phasedVcfWriter.WriteHeader();

                //will continuously add batches until complete
                while (needToMakeMoreNbhds)
                {

                    //just for debugging. quite early if we want need to:
                    if ((_phasableVariantCriteria.MaxNumNbhdsToProcess > 0) && (numNbhdsSoFar > _phasableVariantCriteria.MaxNumNbhdsToProcess))
                        break;

                    Logger.WriteToLog("Getting batch of " + _batchSize);
                    var latestBatchOfNbhds = nbhdBuilder.GetBatchOfNeighborhoods(numNbhdsSoFar);

                    if (latestBatchOfNbhds.Count() == 0)
                        needToMakeMoreNbhds = false;


                    //neighborhoods.AddRange(latestBatchOfNbhds);
                    numNbhdsSoFar += latestBatchOfNbhds.Count();

                    Logger.WriteToLog(string.Format("Neighborhood building complete. {0} neighborhoods created.",
                            latestBatchOfNbhds.Count()));



                    var jobManager = new JobManager(maxThreads);
                    var clusteringJobs = new List<IJob>();
                    foreach (var vcfNeighborhood in latestBatchOfNbhds)
                    {
                        //just a debug option to skip to Nbhd of iterest
                        if (!string.IsNullOrEmpty(_factory.FilteredNbhd) && (_factory.FilteredNbhd != vcfNeighborhood.Id))
                            continue;

                        Logger.WriteToLog("Creating Neighborhood Clustering Job: {0}", vcfNeighborhood.Id);

                        //this does the clustering and MNV calling for the nbhd
                        clusteringJobs.Add(
                            new GenericJob(() => ProcessNeighborhood(vcfNeighborhood),
                            "ProcessNeighborhood_" + vcfNeighborhood.Id));

                    }

                    jobManager.Process(clusteringJobs);

                    // Finally, come back and combine the information for results
                    //  Add back everything that wasn't sucked up into MNVs
                    //  Some adjustment for refs in MNVs or something
                    //  Write VCF

                    //(3) Write results. Do this in chunks to avoid having all variants in memory at all times.  

                    Logger.WriteToLog("Writing batch info to phased vcf.");
                    foreach (var nbhd in latestBatchOfNbhds)
                    {
                        originalAllelesTrailingNeighbhood = variantMerger.WriteVariantsUptoChr(phasedVcfWriter, originalAllelesTrailingNeighbhood, nbhd.ReferenceName);

                        originalAllelesTrailingNeighbhood = variantMerger.WriteVariantsUptoIncludingNbhd(nbhd,
                            phasedVcfWriter, originalAllelesTrailingNeighbhood);
                    }

                }//close batch loop

                variantMerger.WriteRemainingVariants(phasedVcfWriter, originalAllelesTrailingNeighbhood);

            }//close writer

            Logger.WriteToLog("Completed processing in {0}s.",
                DateTime.UtcNow.Subtract(startTime).TotalSeconds);
        }


        private void ProcessNeighborhood(VcfNeighborhood neighborhood)
        {
            Logger.WriteToLog("Processing Neighborhood {0}.", neighborhood.Id);

            try
            {
                var clusterer = _factory.CreateNeighborhoodClusterer();
                var veadGroupSource = _factory.CreateVeadGroupSource();
                var collapsedReads = veadGroupSource.GetVeadGroups(neighborhood);

                //(1) Get CLUSTERS
                var clusters = clusterer.ClusterVeadGroups(collapsedReads.ToList(), neighborhood.Id);


                //clean out vg, we dont need them any more
                veadGroupSource = null;
                collapsedReads = null;

                bool crushNbhdVariantsToSamePositon = !_factory.Options.VcfWritingParams.AllowMultipleVcfLinesPerLoci;

                //(2) Turn clusters into MNV candidates
                neighborhood.CreateMnvsFromClusters(clusters.Clusters,
                    _factory.Options.BamFilterParams.MinimumBaseCallQuality, _factory.Options.VariantCallingParams.MaximumVariantQScore,
                    crushNbhdVariantsToSamePositon);
                neighborhood.SetGenotypesAndPruneExcessAlleles();

                // (3) Variant call the candidates
                var variantCaller = _factory.CreateVariantCaller();
                variantCaller.CallMNVs(neighborhood);
                variantCaller.CallRefs(neighborhood);
                
                //wait untill vcf is ready to write...

            }
            catch (Exception ex)
            {
                Logger.WriteToLog("Error processing neighborhood {0}", neighborhood.Id);
                Logger.WriteExceptionToLog(ex);
            }

        }

    }
}