using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;
using Pisces.IO.Sequencing;
using VariantPhasing.Interfaces;
using VariantPhasing.Models;

namespace VariantPhasing.Logic
{
    public class VcfNeighborhoodBuilder : INeighborhoodBuilder
    {
        private readonly int _phasingDistance;
        private readonly IVcfVariantSource _vcfVariantSource;
        private readonly IEnumerable<string> _chrsToProcess;
        private readonly bool _passingOnly;
        private readonly bool _hetOnly;
        private readonly List<VcfNeighborhood> _neighborhoods;
        private readonly VariantCallingParameters _variantCallingParams;

        public VcfNeighborhoodBuilder(PhasableVariantCriteria phasableVariantCriteria, VariantCallingParameters variantCallingParams, IVcfVariantSource vcfVariantSource)
        {
            _chrsToProcess = phasableVariantCriteria.ChrToProcessArray;
            _passingOnly = phasableVariantCriteria.PassingVariantsOnly;
            _hetOnly = phasableVariantCriteria.HetVariantsOnly;
            _phasingDistance = phasableVariantCriteria.PhasingDistance;
            _vcfVariantSource = vcfVariantSource;
            _neighborhoods = new List<VcfNeighborhood>();
            _variantCallingParams = variantCallingParams;
        }

        public IEnumerable<VcfNeighborhood> GetNeighborhoods()
        {
            var indexInOrignalVcf = -1;
            var referenceStringBetweenVariants = "";

            var lastVariantSite = new VariantSite(0)
            {
                ReferenceName = "",
                VcfReferenceAllele = "",
                VcfAlternateAllele = "",
            };

            var tempRawVcfVariants = _vcfVariantSource.GetVariants();
            var rawVcfVariants = Extensions.Convert(tempRawVcfVariants);


            foreach (var rawVcfVariant in rawVcfVariants)
            {
                indexInOrignalVcf++;

                var currentVariantSite = new VariantSite(rawVcfVariant);
                var refBase = currentVariantSite.VcfReferenceAllele.Substring(0, 1);

                //append the next base, unless we have a repeated variant.
                if (currentVariantSite.VcfReferencePosition != lastVariantSite.VcfReferencePosition)
                    referenceStringBetweenVariants += refBase;


                if (!IsEligibleVariant(rawVcfVariant)) continue;

                //the current variant is close to the last one
                if (IsProximal(currentVariantSite, lastVariantSite, _phasingDistance))
                {
                    FitVariantsInNeighborhood(lastVariantSite, currentVariantSite, referenceStringBetweenVariants);

                    referenceStringBetweenVariants = "";
                }
                else
                    referenceStringBetweenVariants = "";

                lastVariantSite = currentVariantSite;
            }

            //TODO debug log variant sites in all neighborhoods as "phaseables".

            PrepNbhdsForUse(_neighborhoods);

            return _neighborhoods;
        }

        private void PrepNbhdsForUse(List<VcfNeighborhood> neighborhoods)
        {
            foreach (var nbhd in neighborhoods)
            {
                nbhd.OrderVariantSitesByFirstTrueStartPosition();
                nbhd.SetRangeOfInterest();
            }
        }

        private bool IsEligibleVariant(CalledAllele allele)
        {
            if (_chrsToProcess.Any() && !_chrsToProcess.Contains(allele.Chromosome))
            {
                return false;
            }

           
            var genotype = allele.Genotype;

            if ((allele.IsRefType) || (allele.IsNocall))
                return false;
                
            
            if (_hetOnly) //usually false
            {
                //by default, we allow this.
                if (genotype == Pisces.Domain.Types.Genotype.HomozygousAlt)
                    return false;
            }

            if (!_passingOnly) return true;

            var filters = allele.Filters;

            return (filters.Count() == 0);
        }

        public static bool IsProximal(VariantSite variantSite1, VariantSite otherSite, int phasingDistance)
        {
            return variantSite1.ReferenceName == otherSite.ReferenceName && Math.Abs(variantSite1.VcfReferencePosition - otherSite.VcfReferencePosition) < phasingDistance;
        }

        public void FitVariantsInNeighborhood(VariantSite lastVariantSite, VariantSite currentVariantSite, string referenceStringBetweenVariants)
        {
            if (!_neighborhoods.Any())
            {
                ProcessNewNeighborhood(lastVariantSite, currentVariantSite, referenceStringBetweenVariants);
            }
            else
            {
                var lastNeighborhood = _neighborhoods.Last();

                // Have we skipped any positions since our last addition to this chain? If so, we need to make a new neighborhood. 
                // Othwerise, we can add on to the old chain
                if (lastNeighborhood.LastPositionIsNotMatch(lastVariantSite))
                {
                    ProcessNewNeighborhood(lastVariantSite, currentVariantSite, referenceStringBetweenVariants);
                }
                else
                {
                    lastNeighborhood.AddVariantSite(currentVariantSite, referenceStringBetweenVariants);
                }   
            }
        }

        private void ProcessNewNeighborhood(VariantSite lastVariantSite, VariantSite currentVariantSite, string referenceStringBetweenVariants)
        {
            var newNeighborhood = new VcfNeighborhood(_variantCallingParams, currentVariantSite.ReferenceName, 
                lastVariantSite, currentVariantSite, referenceStringBetweenVariants);

            _neighborhoods.Add(newNeighborhood);
        }

    }
}
