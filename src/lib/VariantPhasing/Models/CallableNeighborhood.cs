using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Genotyping;
using Pisces.Domain.Models.Alleles;
using Common.IO.Utility;
using Pisces.Domain.Options;
using Pisces.Domain.Models;
using VariantPhasing.Interfaces;
using VariantPhasing.Logic;

namespace VariantPhasing.Models
{
    /// <summary>
    /// A callable nbhd is different from the vcf nbhd, because it has additional data (the ref chr), 
    /// which enables new methods (clustering and variant calling) which require access to the reads and the genome
    /// </summary>
    public class CallableNeighborhood : ICallableNeighborhood
    {
        private readonly List<CalledAllele> _acceptedPhasedVariants;
        private readonly List<CalledAllele> _rejectedPhasedVariants;
        private readonly IGenotypeCalculator _nbhdGTcalculator;
       
        public Dictionary<int, SuckedUpRefRecord> UsedRefCountsLookup { get; set; } //which reference counts have been sucked up by MNVs.  we need to remember to subtract this from any reference calls.
        public Dictionary<int, List<CalledAllele>> CalledVariants { get; set; }
        public Dictionary<int, CalledAllele> CalledRefs { get; set; }
        public Dictionary<VariantSite, VariantPhasingResult> PhasingProbabiltiies;
        public string NbhdReferenceSequenceSubstring = "";
        private VcfNeighborhood _vcfNeighborhood;
        public bool HasVariants { get { return VcfVariantSites.Count > 0; } }
        public string ReferenceName { get { return _vcfNeighborhood.ReferenceName; } }
        public string Id { get { return _vcfNeighborhood.Id; } }

       public List<VariantSite> VcfVariantSites { get { return _vcfNeighborhood.VcfVariantSites; } }

       public int FirstPositionOfInterest { get { return _vcfNeighborhood.FirstPositionOfInterest; } }

        public int LastPositionOfInterestInVcf { get { return _vcfNeighborhood.LastPositionOfInterestInVcf; } }

        public int LastPositionOfInterestWithLookAhead { get { return _vcfNeighborhood.LastPositionOfInterestWithLookAhead; } }

        public int SoftClipEndBeforeNbhd { get { return _vcfNeighborhood.SoftClipEndBeforeNbhd; } }

        public int SoftClipPosAfterNbhd { get { return _vcfNeighborhood.SoftClipPosAfterNbhd; } }

        public int NumberClippedReads = 0;
        public int MaxQScore = 100;

        public List<CalledAllele> CandidateVariants
        {
            get
            {
                //TODO come back and potentially use the actual OrderVariantsExtension
                //return _acceptedPhasedVariants.OrderBy(x=> x.Chromosome).ThenBy(x=>x.ReferencePosition).ThenBy(x=>x.ReferenceAllele).ThenBy(x=>x.AlternateAllele).ToList();

                var comparer = new AlleleCompareByLoci();
                _acceptedPhasedVariants.Sort(comparer);
                return _acceptedPhasedVariants;
            }
        }

        public List<CalledAllele> Refs
        {
            get
            {
                return _rejectedPhasedVariants.OrderBy(x => x.Chromosome).ThenBy(x => x.ReferencePosition).ThenBy(x => x.ReferenceAllele).ThenBy(x => x.AlternateAllele).ToList();
            }
        }
       
        public IGenotypeCalculator NbhdGTcalculator
        {
            get
            {
                return _nbhdGTcalculator;
            }
        }

        public CallableNeighborhood(VcfNeighborhood vcfNeighborhood, VariantCallingParameters variantCallingParams, ChrReference chrReference = null)
        {

            //housekeeping
          
            _nbhdGTcalculator = GenotypeCreator.CreateGenotypeCalculator(variantCallingParams.PloidyModel, variantCallingParams.MinimumFrequencyFilter,
                variantCallingParams.MinimumCoverage,
                variantCallingParams.DiploidSNVThresholdingParameters,
                variantCallingParams.DiploidINDELThresholdingParameters,
                variantCallingParams.AdaptiveGenotypingParameters,
                variantCallingParams.MinimumGenotypeQScore, variantCallingParams.MaximumGenotypeQScore, variantCallingParams.TargetLODFrequency);


            _vcfNeighborhood = vcfNeighborhood;
            _acceptedPhasedVariants = new List<CalledAllele>();
            _rejectedPhasedVariants = new List<CalledAllele>();
            UsedRefCountsLookup = new Dictionary<int, SuckedUpRefRecord>();
            MaxQScore = variantCallingParams.MaximumVariantQScore;

            //prep vcf nbhd for use, so we know the final range of loci in play
            vcfNeighborhood.OrderVariantSitesByFirstTrueStartPosition();
            vcfNeighborhood.SetRangeOfInterest();

            //set reference bases here, then let go of the chr
            if ((chrReference == null) || (chrReference.Sequence == null)) //be gentle if they did not include a ref genome
                NbhdReferenceSequenceSubstring = new String('R', vcfNeighborhood.LastPositionOfInterestWithLookAhead - vcfNeighborhood.FirstPositionOfInterest);
            else
            {
                NbhdReferenceSequenceSubstring = chrReference.Sequence.Substring(vcfNeighborhood.FirstPositionOfInterest - 1, vcfNeighborhood.LastPositionOfInterestWithLookAhead - vcfNeighborhood.FirstPositionOfInterest);
            }


        }



        /// <summary>
        /// sometimes we get variant sites like this, below in the original vcf. 
        /// And in truth, the insertion should come after the C>T.
        /// So, we reorder. Keeping this list ordered makes downstream calculations easier.
        /// chr7	140453136	.	A	.	100	PASS	
        ///  chr7	140453137	.	C CGTA	52	PASS
        ///  chr7	140453137	.	C T	58	
        /// </summary>
        public void OrderVariantSitesByFirstTrueStartPosition()
        {
            var indexes = VcfVariantSites.Select(vs => vs.OriginalAlleleFromVcf).ToList();
            VcfVariantSites.Sort();

            for (int i = 0; i < VcfVariantSites.Count; i++)
                VcfVariantSites[i].OriginalAlleleFromVcf = indexes[i];

        }

       

        /// <summary>
        /// Use the diploid GT calculator to figure out GT, and remove any extra alleles.
        /// </summary>
        public void SetGenotypesAndPruneExcessAlleles()
        {
            List<CalledAllele> allelesToPrune = _nbhdGTcalculator.SetGenotypes(_acceptedPhasedVariants);


            foreach (var mnv in allelesToPrune)
            {
                _acceptedPhasedVariants.Remove(mnv);
            }

        }

        public void CreateMnvsFromClusters(IEnumerable<ICluster> clusters, int qNoiselevel, bool crushNbhd = false)
        {
            if (clusters == null) return;
            if (clusters.Count() == 0) return;

            var depthAtSites = new int[0];
            var nocallsAtSites = new int[0];
            DepthAtSites(clusters, out depthAtSites, out nocallsAtSites);

            Logger.WriteToLog("Creating MNVs from clusters.");

            int anchorPosition = -1;
            //if we are crushing the vcf, or in diploid mode, always report all phased alleles throug the nbhd, starting at the first position of interest. (ie, the first position we started phasing on)
            //If we are in somatic mode or uncrushed mode, we just report the variants at the loci we find them on (normal Pisces)
            if (crushNbhd || _nbhdGTcalculator.PloidyModel == Pisces.Domain.Types.PloidyModel.DiploidByThresholding
                 || _nbhdGTcalculator.PloidyModel == Pisces.Domain.Types.PloidyModel.DiploidByAdaptiveGT)
                anchorPosition = _vcfNeighborhood.FirstPositionOfInterest;



            foreach (var cluster in clusters)
            {
                CalledAllele mnv;

                var clusterConsensus = cluster.GetConsensusSites();

                Logger.WriteToLog(cluster.Name + "\tVariantSites\t" + VariantSite.ArrayToString(clusterConsensus));
                Logger.WriteToLog(cluster.Name + "\tVariantPositions\t" + VariantSite.ArrayToPositions(clusterConsensus));

                // Finding cluster ref support:
                // Nima: is it a bad idea to pass cluster[] clusters to a function in a cluster object?
                var cluterRefSupport = cluster.GetClusterReferenceSupport(clusters);

                var referenceRemoval = PhasedVariantExtractor.Extract(out mnv, clusterConsensus,
                   NbhdReferenceSequenceSubstring, depthAtSites, nocallsAtSites, cluterRefSupport, cluster.CountsAtSites, ReferenceName, qNoiselevel, MaxQScore, anchorPosition);

                if ((mnv.Type != Pisces.Domain.Types.AlleleCategory.Reference) && mnv.AlleleSupport != 0)
                {
                    Logger.WriteToLog(cluster.Name + "mnv accepted:\t" + mnv.ToString());
                    AddAcceptedPhasedVariant(mnv);

                    //keep track of reference calls sucked into MNVs.
                    //We will need to subtract this from the ref counts when we write out the final vcf.
                    foreach (var refPosition in referenceRemoval.Keys)
                    {
                        if (!UsedRefCountsLookup.ContainsKey(refPosition))
                        {
                            var suckedUpRefRecord = new SuckedUpRefRecord()
                            { Counts = 0, AlleleThatClaimedIt = mnv };
                            UsedRefCountsLookup.Add(refPosition, suckedUpRefRecord);
                        }

                        UsedRefCountsLookup[refPosition].Counts += referenceRemoval[refPosition].Counts;
                    }
                }
                else if (mnv.TotalCoverage != 0) //dont add empty stuff..
                {
                    Logger.WriteToLog("mnv rejected:\t" + mnv.ToString());
                    AddRejectedPhasedVariant(mnv);
                }



            }
            foreach (var phasedVariant in CandidateVariants)
            {
                var calledPhasedVariant = phasedVariant as CalledAllele;
                if (calledPhasedVariant == null) continue;

                // calledPhasedVariant.ReferenceSupport = phasedVariant.TotalCoverage - phasedVariant.AlleleSupport;
                calledPhasedVariant.ReferenceSupport = phasedVariant.ReferenceSupport;
                if (UsedRefCountsLookup.ContainsKey(phasedVariant.ReferencePosition) && (UsedRefCountsLookup[phasedVariant.ReferencePosition].AlleleThatClaimedIt != phasedVariant))
                    calledPhasedVariant.ReferenceSupport = calledPhasedVariant.ReferenceSupport - UsedRefCountsLookup[phasedVariant.ReferencePosition].Counts;

                calledPhasedVariant.ReferenceSupport = Math.Max(0, calledPhasedVariant.ReferenceSupport);
            }

        }


        public void DepthAtSites(IEnumerable<ICluster> clusters, out int[] depths, out int[] nocalls)
        {
            var veadgroups = clusters.SelectMany(x => x.GetVeadGroups());
            VeadGroup.DepthAtSites(veadgroups, out depths, out nocalls);
        }

        public void AddAcceptedPhasedVariant(CalledAllele variant)
        {
            var match = _acceptedPhasedVariants.Find(v => v.IsSameAllele(variant));

            if (match == null)
                _acceptedPhasedVariants.Add(variant);
            else
            {
                var combinedVar = PhasedVariantExtractor.CombinePhasedVariants(match, variant, MaxQScore);
                _acceptedPhasedVariants.Remove(match);
                _acceptedPhasedVariants.Add(combinedVar);
            }
        }

        public void AddRejectedPhasedVariant(CalledAllele variant)
        {
            var match = _rejectedPhasedVariants.Find(v => v.IsSameAllele(variant));

            if (match == null)
                _rejectedPhasedVariants.Add(variant);
            else
            {
                var combinedVar = PhasedVariantExtractor.CombinePhasedVariants(match, variant, MaxQScore);
                _rejectedPhasedVariants.Remove(match);
                _rejectedPhasedVariants.Add(combinedVar);
            }
        }

        public bool LastPositionIsNotMatch(VariantSite variantSite)
        {
            return VcfVariantSites.Last().VcfReferencePosition != variantSite.VcfReferencePosition;
        }

        public List<CalledAllele> GetOriginalVcfVariants()
        {
            return VcfVariantSites.Select(vs => vs.OriginalAlleleFromVcf).ToList();
        }

    }

}