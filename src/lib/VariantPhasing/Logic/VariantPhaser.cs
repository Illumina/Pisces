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
                    var latestBatchOfNbhds = nbhdBuilder.GetBatchOfCallableNeighborhoods(numNbhdsSoFar);

                    if (latestBatchOfNbhds.Count() == 0)
                        needToMakeMoreNbhds = false;

                    numNbhdsSoFar += latestBatchOfNbhds.Count();

                    Logger.WriteToLog(string.Format("Neighborhood building complete. {0} neighborhoods created.",
                            latestBatchOfNbhds.Count()));



                    var jobManager = new JobManager(maxThreads);
                    var jobs = new List<IJob>();

                    foreach (var callableNeighborhood in latestBatchOfNbhds)
                    {
                        //just a debug option to skip to Nbhd of iterest
                        if (!string.IsNullOrEmpty(_factory.FilteredNbhd) && (_factory.FilteredNbhd != callableNeighborhood.Id))
                            continue;

                        Logger.WriteToLog("Creating Neighborhood Clustering and Calling Job: {0}", callableNeighborhood.Id);

                        //this does the clustering and MNV calling for the nbhd
                        jobs.Add(
                            new GenericJob(() => CallMnvsForNeighborhood(callableNeighborhood),
                            "ProcessNeighborhood_" + callableNeighborhood.Id));

                    }

                    jobManager.Process(jobs);

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


        private void CallMnvsForNeighborhood(CallableNeighborhood neighborhood)
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
                if (neighborhood.NumberClippedReads > 0 &&
                    _factory.Options.SoftClipSupportParams.UseSoftClippedReads)
                {
                    var softClippedSupportFinder = _factory.CreateSoftClipSupportFinder();
                    softClippedSupportFinder.SupplementSupportWithClippedReads(neighborhood);
                }
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