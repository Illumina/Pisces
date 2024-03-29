﻿using System.Collections.Generic;
using System.Linq;
using VariantPhasing.Interfaces;

namespace VariantPhasing.Models
{
    public class Cluster : ICluster
    {
        private readonly List<VeadGroup> _veadGroups;
        private int _numKnownRef;
        private int _numKnownAlt;
        private VeadGroup _consensus; //note, this Consensus currently presumes no disagreements are allowed in the cluster.

        private bool Closed
        {
            get
            {
                return (_numKnownAlt == _consensus.SiteResults.Length)
                       && (_numKnownRef == _consensus.SiteResults.Length);
            }
        }

        public string Name { get; set; }

        public int[] CountsAtSites
        {
            get
            {
                var countsAtSites = new int[GetConsensusSites().Length];
                foreach (var vg in _veadGroups)
                {
                    var depths = vg.ToDepths();
                    for (var i = 0; i < GetConsensusSites().Length; i++)
                    {
                        countsAtSites[i] += depths[i];
                    }
                }
                return countsAtSites;
            } 
        }

        public int NumVeadGroups
        {
            get { return _veadGroups.Count; }
        }

        public int NumVeads
        {
            get
            {
                return _veadGroups.Sum(rg => rg.NumVeads);
            }
        }

        public Cluster(string name, List<VeadGroup> vgs)
        {
            Name = name;
            _veadGroups = vgs;
            ResetConsensus();
        }

        public List<VeadGroup> GetVeadGroups()
        {
            return _veadGroups;
        }

        public void Add(VeadGroup vg, bool updateConsensus = true)
        {
            _veadGroups.Add(vg);

            if (!Closed && updateConsensus)
            {
                UpdateConsensus(vg);
            }

        }

        public void Add(List<VeadGroup> vgs, bool updateConsensus = true)
        {
            foreach (var vg in vgs)
            {
                Add(vg, updateConsensus);
            }
        }

        public void Remove(VeadGroup vg)
        {
            _veadGroups.Remove(vg);
            ResetConsensus();
        }

        public int GetClusterReferenceSupport(IEnumerable<ICluster> clusters)
        {	
            var clusterVead = new Vead("test", GetConsensusSites());	
            var numRef = 0;	
            foreach (VariantSite vs in clusterVead.SiteResults)	
            {	
                // Nima: If cluster contains reference or X>X at this site, this site won't be included in the MNV.	
                // So veads that support reference for this cluster can hold any variant at these sites (hence using N>N).	
                // In the following example, cluster reference support for Cluster3 includes both Cluster1 and Cluster4	
                //---------------------------------------	
                //      Sites       S1      S2      S3	
                //      Cluster1    R       R       R	
                //      Cluster2    R       R       X	
                //      Cluster3    R       X       X	
                //      Cluster4    X       R       R	
                //----------------------------------------	
                //Cluster3's refVead N>N    R>R     R>R	
                //	
                // However, if the cluster is all ref we should avoid all N>N, and instead have the original R>R	
                // In example above:	
                //Cluster1's refVead R>R    R>R     R>R	
                if (vs.IsReference || vs.VcfAlternateAllele == vs.VcfReferenceAllele)	
                {	
                    vs.VcfReferenceAllele = "N";	
                    vs.VcfAlternateAllele = "N";	
                    if (vs.IsReference)	
                    {	
                        numRef++;	
                    }	
                }	
                else  // If cluster contains a variant at this site, ref veads need to contain ref>ref at this site.	
                {	
                    vs.VcfReferenceAllele = vs.VcfReferenceAllele[0].ToString();	
                    vs.VcfAlternateAllele = vs.VcfReferenceAllele;	
                }	
            }	
            var refVead = clusterVead;	
            // Check if all variants were reference, assign original consensus to refVead	
            if (numRef == clusterVead.SiteResults.Count())	
            {	
                refVead = new Vead("test", GetConsensusSites());	
            }	
	
            var clusterRefVeadGroup = new VeadGroup(refVead);	
	
	
            var numVariants = clusterRefVeadGroup.NumSitesPerVead;	
            var cluterRefSupport = 0;	
            foreach (var cls in clusters)	
            {	
                List<VeadGroup> vgs = cls.GetVeadGroups();	
                foreach (var vg in vgs)	
                {	
                    var agreement = new Agreement(vg, clusterRefVeadGroup);	
                    if (agreement.NumDisagreement == 0 &&	
                        agreement.NumAgreement >= numVariants)	
                    {	
                        cluterRefSupport += vg.NumVeads;	
                    }	
                }	
	
            }	
            return cluterRefSupport;	
        }

        public void ResetConsensus()
        {
            _consensus = null;
            _numKnownAlt = 0;
            _numKnownRef = 0;

            if ((_veadGroups == null) || (_veadGroups.Count == 0))
                return;

            var vead = new Vead(Name, _veadGroups[0].SiteResults);
            _consensus = new VeadGroup(vead);

            foreach (var rg in _veadGroups.Where(rg => !Closed))
            {
                UpdateConsensus(rg);
            }
        }

        private void UpdateConsensus(VeadGroup rg)
        {
            if (_consensus == null) return;

            //TODO why would you ever have more known refs than alts or vice versa?
            VeadGroupMerger.MergeProfile1Into2(rg.SiteResults, _consensus.SiteResults);

            _numKnownRef = _consensus.SiteResults.Count(s => s.HasRefData());
            _numKnownAlt = _consensus.SiteResults.Count(s => s.HasAltData());                
        }

        public VeadGroup GetWorstAgreement()
        {
            // There will always be a "worst" unless there are no vead groups...

            VeadGroup worstVeadGroup = null;
            var worstAgreement = new Agreement { NumAgreement = int.MaxValue, NumDisagreement = 0 };

            for (var i = 0; i < NumVeadGroups; i++)
            {
                var agreementWithCluster = GetAgreementWithCluster(_veadGroups[i]);

                if (agreementWithCluster.CompareTo(worstAgreement) >= 0) continue;

                worstAgreement = agreementWithCluster;
                worstVeadGroup = _veadGroups[i];
            }

            return worstVeadGroup;
        }

        private Agreement GetAgreementWithCluster(VeadGroup v1)
        {
            var netAgreement = new Agreement();

            for (var i = 0; i < NumVeadGroups; i++)
            {
                var v2 = _veadGroups[i];

                if (v1 == v2) continue;

                var a = new Agreement(v1, v2);
                netAgreement.AddAgreement(a);
            }

            return netAgreement;
        }

        public Agreement GetBestAgreementWithVeadGroup(VeadGroup newVeadGroup, int maxNumberDisagreements)
        {
            var bestAgreement = new Agreement();

            foreach (var agreement in _veadGroups.Where(vg => newVeadGroup.Name != vg.Name).
                Select(vg=> new Agreement(newVeadGroup, vg)))
            {
                //we disagree with something in the cluster already.
                if (agreement.NumDisagreement > maxNumberDisagreements)
                {
                    return null;
                }

                if (agreement.CompareTo(bestAgreement) > 0)
                    bestAgreement = agreement;
            }

            return bestAgreement;

        }

        public VariantSite[] GetConsensusSites()
        {
            return _consensus == null ? new VariantSite[0] : _consensus.SiteResults;
        }

        public Dictionary<VariantSite, int> GetVeadCountsInCluster(List<VariantSite> vsList)
        {
            var readCountSupportForVs = new Dictionary<VariantSite, int>(); // This is doing what it was before, without the extra wrapping. Does this make sense though?

            foreach (var vs in vsList)
            {
                readCountSupportForVs.Add(vs, 0);
                readCountSupportForVs[vs] = GetVeadCountsForVariantSite(vs);
            }

            return readCountSupportForVs;
        }

        private int GetVeadCountsForVariantSite(VariantSite vs)
        {
            var support = 0;

            foreach (var rg in _veadGroups)
            {
                //all veads are the same within a group, so just choose one.
                var vead = rg.RepresentativeVead;

                support += 
                    vead.SiteResults.Where(vsRead => vs.VcfReferencePosition == vsRead.VcfReferencePosition && 
                        (vs.VcfReferenceAllele == vsRead.VcfReferenceAllele) && 
                        (vs.VcfAlternateAllele == vsRead.VcfAlternateAllele)).Sum(vsRead => rg.NumVeads);
            }

            return support;
        }
    }

   
}
