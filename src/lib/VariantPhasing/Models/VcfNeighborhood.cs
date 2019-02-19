using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Calculators;
using Pisces.Domain.Models.Alleles;
using Common.IO.Utility;
using Pisces.Domain.Options;
using Pisces.Genotyping;
using VariantPhasing.Interfaces;
using VariantPhasing.Types;

namespace VariantPhasing.Models
{
    public class VcfNeighborhood : INeighborhood
    {
        public string Id = "";
        public List<VariantSite> VcfVariantSites = new List<VariantSite>();
        public string _referenceName = "";

        public bool HasVariants { get { return VcfVariantSites.Count > 0; } }
        public string ReferenceName { get { return _referenceName; } }

        public int LastPositionOfInterestInVcf { get; set; } = -1;
        public int LastPositionOfInterestWithLookAhead { get; set; } = -1;
        public int FirstPositionOfInterest { get; set; } = -1;
        public int SoftClipEndBeforeNbhd { get; set; } = -1;
        public int SoftClipPosAfterNbhd { get; set; } = -1;


        public VcfNeighborhood(int nbhdNum, string refName, VariantSite vs1, VariantSite vs2)
        {

            VcfVariantSites = new List<VariantSite>();
            _referenceName = refName;

            AddVariantSite(vs1);
            AddVariantSite(vs2);

            SetID(nbhdNum);
        }

        public void AddVariantSite(VariantSite variantSite)
        {
            VcfVariantSites.Add(variantSite.DeepCopy());
        }

        public void SetID(int nbhdNum)
        {
            int posID = VcfVariantSites.Any() ? VcfVariantSites.First().VcfReferencePosition : -1;
            Id = "NbhdNum" + nbhdNum + "_" + ReferenceName + "_" + posID;
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
        /// This method sets the first/last positions of interest, needed to later get the correct chr ref string 
        /// </summary>
        public void SetRangeOfInterest()
        {
            LastPositionOfInterestWithLookAhead = VcfVariantSites.First().VcfReferencePosition;
            LastPositionOfInterestInVcf = VcfVariantSites.Last().VcfReferencePosition;

            foreach (var vs in VcfVariantSites)
            {
                // Nima: lookahead can go 1 or more bases beyond the last base of difference in neighborhood
                //       1 base is expected.
                //      Insertion: A>ACC -> lookahead = refPosition + 3 (3 bases after neighborhood ends on "A")
                //      Deletion:  ACG>A -> lookahead = refPosition + 3 (1 base after neighborhood ends on "G")
                //      SNV: A>G -> lookahead = refPosition + 1 (1 base after neighborhood ends on "A")
                var lookAhead = vs.VcfReferencePosition + Math.Max(vs.VcfAlternateAllele.Length, vs.VcfReferenceAllele.Length);

                if (lookAhead > LastPositionOfInterestWithLookAhead)
                    LastPositionOfInterestWithLookAhead = lookAhead;
            }
            FirstPositionOfInterest = VcfVariantSites.First().VcfReferencePosition;
            var firstVariantSite = VcfVariantSites.First();
            var firstVariantType = firstVariantSite.GetVariantType();
            var lastVariantSite = VcfVariantSites.Last();

            //Legend:       SC Start: > (First base in the clipped portion of the read)
            //              SC End: < (Last base in the clipped portion of the read)
            //              VCF var site: .
            //              Ref, Mismatch, Insertion, Deletion: R, M, I, D

            //                        .  >     (SC end on VCF var site)
            //      Alt1            RRRDDRR
            //
            //                        .  > (SC starts 1 position after VCF start) (SC end on VCF var site)
            //      Alt2            RRRIIRR
            //
            //                        <.> (All three <.> on top of M)
            //      Alt3            RRRMRRR

            // If first variant is insertion or deletion, soft clipped reads will end at VcfReferencePosition
            if (firstVariantType == SubsequenceType.DeletionSequence ||
                firstVariantType == SubsequenceType.InsertionSquence)
            {
                SoftClipEndBeforeNbhd = firstVariantSite.VcfReferencePosition;
            }
            // If first variant is SNV, soft clipped reads end one base before VcfReferencePosition (last matching position)
            else
            {
                SoftClipEndBeforeNbhd = firstVariantSite.VcfReferencePosition - 1;
            }

            SoftClipPosAfterNbhd = lastVariantSite.VcfReferencePosition + lastVariantSite.VcfReferenceAllele.Length;

        }
  
        public bool LastPositionIsNotMatch(VariantSite variantSite)
        {
            return VcfVariantSites.Last().VcfReferencePosition != variantSite.VcfReferencePosition;
        }
    }
}
