using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Calculators;
using Pisces.Domain.Models.Alleles;
using Common.IO.Utility;
using Pisces.Domain.Options;
using VariantPhasing.Interfaces;
using VariantPhasing.Logic;

namespace VariantPhasing.Models
{
    public class VcfNeighborhood : IVcfNeighborhood
    {
        //private  Dictionary<int, List<CalledAllele>> _calledVariants;
        //private Dictionary<int, List<CalledAllele>> _calledRefs;
        private readonly List<CalledAllele> _acceptedPhasedVariants;
        private readonly List<CalledAllele> _rejectedPhasedVariants;
        private readonly IGenotypeCalculator _nbhdGTcalculator;
        public string Id = "";
        public List<VariantSite> VcfVariantSites = new List<VariantSite>();
        public Dictionary<int, int> UsedRefCountsLookup { get; set; } //which reference counts have been sucked up by MNVs.  we need to remember to subtract this from any reference calls.
        public Dictionary<int, List<CalledAllele>> CalledVariants { get; set; }
        public Dictionary<int, CalledAllele> CalledRefs { get; set; }
        public Dictionary<VariantSite, VariantPhasingResult> PhasingProbabiltiies;
        public string ReferenceSequence = "";
        public string _referenceName = "";
        private int lastVcfPositionInNbhd = -1;
        private int lastPositionOfInterest = -1;
        private int firstPositionOfInterest = -1;
        public bool HasVariants { get { return VcfVariantSites.Count > 0; } }
        public string ReferenceName { get { return _referenceName; } }

        public List<CalledAllele> CandidateVariants
        {
            get
            {
                //TODO come back and potentially use the actual OrderVariantsExtension
                //return _acceptedPhasedVariants.OrderBy(x=> x.Chromosome).ThenBy(x=>x.ReferencePosition).ThenBy(x=>x.ReferenceAllele).ThenBy(x=>x.AlternateAllele).ToList();

                var comparer = new AlleleComparer();
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
      
              public int LastPositionOfInterestInVcf
        {
            get
            {
                return lastVcfPositionInNbhd;
            }

            set
            {
                lastVcfPositionInNbhd = value;
            }
        }

        public int LastPositionOfInterestWithLookAhead
        {
            get
            {
                return lastPositionOfInterest;
            }

            set
            {
                lastPositionOfInterest = value;
            }
        }

        public int FirstPositionOfInterest
        {
            get
            {
                return firstPositionOfInterest;
            }

            set
            {
                firstPositionOfInterest = value;
            }
        }

        public IGenotypeCalculator NbhdGTcalculator
        {
            get
            {
                return _nbhdGTcalculator;
            }
        }

        public VcfNeighborhood(VariantCallingParameters variantCallingParams, string refName, VariantSite vs1, VariantSite vs2, string interveningRef)
        {
            _nbhdGTcalculator = GenotypeCreator.CreateGenotypeCalculator(variantCallingParams.PloidyModel, variantCallingParams.MinimumFrequencyFilter,
                variantCallingParams.MinimumCoverage,
                variantCallingParams.DiploidThresholdingParameters,
                variantCallingParams.MinimumGenotypeQScore, variantCallingParams.MaximumGenotypeQScore);
             VcfVariantSites = new List<VariantSite>();
            _referenceName = refName;
            _acceptedPhasedVariants = new List<CalledAllele>();
            _rejectedPhasedVariants = new List<CalledAllele>();
            UsedRefCountsLookup = new Dictionary<int, int>();

            AddVariantSite(vs1, vs1.VcfReferenceAllele.Substring(0, 1));
            AddVariantSite(vs2, interveningRef);

            SetID();
        }

        public void SetID()
        {
            int posID = VcfVariantSites.Any() ? VcfVariantSites.First().VcfReferencePosition : -1;
            Id = ReferenceName + "_" + posID;
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

        public void SetRangeOfInterest()
        {
           
            LastPositionOfInterestWithLookAhead = VcfVariantSites.First().VcfReferencePosition;
            LastPositionOfInterestInVcf = VcfVariantSites.Last().VcfReferencePosition;

            foreach (var vs in VcfVariantSites)
            {
                var lookAhead = vs.VcfReferencePosition + System.Math.Max(vs.VcfAlternateAllele.Length, vs.VcfReferenceAllele.Length);

                if (lookAhead > LastPositionOfInterestWithLookAhead)
                    LastPositionOfInterestWithLookAhead = lookAhead;
            }
            FirstPositionOfInterest = VcfVariantSites.First().VcfReferencePosition;
        }

        public void AddVariantSite(VariantSite variantSite, string refSinceLastVariant)
        {
            ReferenceSequence += refSinceLastVariant;
            VcfVariantSites.Add(variantSite.DeepCopy());
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

        public void AddMnvsFromClusters(IEnumerable<ICluster> clusters, int qNoiselevel, int maxQscore, bool crushNbhd=false)
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
            if (crushNbhd || _nbhdGTcalculator.PloidyModel == Pisces.Domain.Types.PloidyModel.Diploid)
                anchorPosition = FirstPositionOfInterest;


            foreach (var cluster in clusters)
            {
                CalledAllele mnv;

                var clusterConsensus = cluster.GetConsensusSites();
               
                Logger.WriteToLog(cluster.Name + "\tVariantSites\t" + VariantSite.ArrayToString(clusterConsensus));
                Logger.WriteToLog(cluster.Name + "\tVariantPositions\t" + VariantSite.ArrayToPositions(clusterConsensus));


                var referenceRemoval = PhasedVariantExtractor.Extract(out mnv, clusterConsensus,
                    ReferenceSequence, depthAtSites, nocallsAtSites, cluster.CountsAtSites, ReferenceName, qNoiselevel, maxQscore, anchorPosition);

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
                            UsedRefCountsLookup.Add(refPosition, 0);
                        }

                        UsedRefCountsLookup[refPosition] += referenceRemoval[refPosition];
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
                
                calledPhasedVariant.ReferenceSupport = phasedVariant.TotalCoverage - phasedVariant.AlleleSupport;
                if (UsedRefCountsLookup.ContainsKey(phasedVariant.ReferencePosition))
                    calledPhasedVariant.ReferenceSupport = calledPhasedVariant.ReferenceSupport - UsedRefCountsLookup[phasedVariant.ReferencePosition];

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
            _acceptedPhasedVariants.Add(variant);   
        }

        private void AddRejectedPhasedVariant(CalledAllele variant)
        {
            _rejectedPhasedVariants.Add(variant);
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
