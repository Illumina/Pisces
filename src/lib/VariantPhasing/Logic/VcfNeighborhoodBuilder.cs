using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.IO;
using Pisces.IO.Sequencing;
using VariantPhasing.Interfaces;
using VariantPhasing.Models;

namespace VariantPhasing.Logic
{
    public class VcfNeighborhoodBuilder : INeighborhoodBuilder
    {
        private readonly IVcfVariantSource _vcfVariantSource;
        private readonly int _maxNumNbhdsInBatch;
        private readonly List<VcfNeighborhood> _nextBatchOfNeighborhoods;
        private readonly List<VcfNeighborhood> _unfinshedNeighborhoods;
        private readonly VariantCallingParameters _variantCallingParams;
        private readonly PhasableVariantCriteria _phasableVariantCriteria;

        string _referenceStringBetweenVariants = "";

        VcfVariant _tempRawVcfVariant = new VcfVariant();
        VariantSite _lastVariantSite = new VariantSite();

        public VcfNeighborhoodBuilder(PhasableVariantCriteria phasableVariantCriteria, VariantCallingParameters variantCallingParams, 
            IVcfVariantSource vcfVariantSource, int batchSize)
        {

            _variantCallingParams = variantCallingParams;
            _phasableVariantCriteria = phasableVariantCriteria;
            _vcfVariantSource = vcfVariantSource;
            _nextBatchOfNeighborhoods = new List<VcfNeighborhood>();
            _unfinshedNeighborhoods = new List<VcfNeighborhood>();
            _maxNumNbhdsInBatch = batchSize;

            var lastVariantSite = new VariantSite()
            {
                VcfReferenceAllele = "",
                VcfAlternateAllele = "",
                ReferenceName = "",
                VcfReferencePosition = 0

            };
        }

        public IEnumerable<VcfNeighborhood> GetBatchOfNeighborhoods(int numNbhdsSoFar)
        {

            _nextBatchOfNeighborhoods.Clear();
            _nextBatchOfNeighborhoods.AddRange(_unfinshedNeighborhoods);
            _unfinshedNeighborhoods.Clear();

            bool keepAddingNbhdsToTheBatch = true;

            while (keepAddingNbhdsToTheBatch)
            {
                keepAddingNbhdsToTheBatch = _vcfVariantSource.GetNextVariant(_tempRawVcfVariant);

                //only loose _tempRawVcfVariant if it was null. We are done with the file.
                if (!keepAddingNbhdsToTheBatch)
                    break;

                var allelesUnpackedFromVcfVariant = Extensions.Convert(new List<VcfVariant> { _tempRawVcfVariant });
               

                foreach (var currentAllele in allelesUnpackedFromVcfVariant)
                {
                   
                    if(currentAllele.Filters.Contains(FilterType.ForcedReport)) continue;

                    var currentVariantSite = new VariantSite(currentAllele);
                    var refBase = currentVariantSite.VcfReferenceAllele.Substring(0, 1);

                    //append the next base, unless we have a repeated variant.
                    if (currentVariantSite.VcfReferencePosition != _lastVariantSite.VcfReferencePosition)
                        _referenceStringBetweenVariants += refBase;

                    //check its not reference or otherwise useless
                    if (!IsEligibleVariant(currentAllele)) continue;

                    //the current variant is close to the last one
                    if (IsProximal(currentVariantSite, _lastVariantSite, _phasableVariantCriteria.PhasingDistance))
                    {
                        keepAddingNbhdsToTheBatch = FitVariantsInNeighborhood(_lastVariantSite, currentVariantSite, 
                            _referenceStringBetweenVariants, numNbhdsSoFar);

                        _referenceStringBetweenVariants = "";
                    }
                    else
                        _referenceStringBetweenVariants = "";

                    _lastVariantSite = currentVariantSite;

                }
            }

            PrepNbhdsForUse(_nextBatchOfNeighborhoods);

            return _nextBatchOfNeighborhoods;
        }

  
        private void PrepNbhdsForUse(List<VcfNeighborhood> neighborhoods)
        {
            //definsively fix any ordering issues that might have come in from the vcf
            foreach (var nbhd in neighborhoods)
            {
                nbhd.OrderVariantSitesByFirstTrueStartPosition();
                nbhd.SetRangeOfInterest();
            }
        }

        public bool IsEligibleVariant(CalledAllele allele)
        {
            if (_phasableVariantCriteria.ChrToProcessArray.Any() && !_phasableVariantCriteria.ChrToProcessArray.Contains(allele.Chromosome))
            {
                return false;
            }


            var genotype = allele.Genotype;

            if ((allele.IsRefType) || (allele.IsNocall))
                return false;


            if (_phasableVariantCriteria.HetVariantsOnly) //usually false
            {
                //by default, we allow this.
                if (genotype == Pisces.Domain.Types.Genotype.HomozygousAlt)
                    return false;
            }

            if (!_phasableVariantCriteria.PassingVariantsOnly)
                return true;

            var filters = allele.Filters;

            return (filters.Count() == 0);
        }

        public static bool IsProximal(VariantSite variantSite1, VariantSite otherSite, int phasingDistance)
        {
            return variantSite1.ReferenceName == otherSite.ReferenceName && Math.Abs(variantSite1.VcfReferencePosition - otherSite.VcfReferencePosition) < phasingDistance;
        }

        //These two variants are close enough go in the same nbhd. Either, add them to the last nbhd chain, or start a new chain.
        public bool FitVariantsInNeighborhood(VariantSite lastVariantSite, VariantSite currentVariantSite,
            string referenceStringBetweenVariants, int numNbhdsSoFar)
        {

            bool ItsOKToAddANbhdToThisBatch = _nextBatchOfNeighborhoods.Count < _maxNumNbhdsInBatch;


            //if no batches exist yet.. We have to add one.
            if (_nextBatchOfNeighborhoods.Count == 0)
            {
                AddNewNeighborhoodToBatch(lastVariantSite, currentVariantSite, referenceStringBetweenVariants,
                    numNbhdsSoFar);

                return ItsOKToAddANbhdToThisBatch;
            }

            //else, we previously had a nbhd we were working on:
            var lastNeighborhood = _nextBatchOfNeighborhoods.Last();

            // Have we skipped any positions since our last addition to this chain? If so, we need to make a new neighborhood. 
            // Othwerise, we can add on to the old chain
            if (lastNeighborhood.LastPositionIsNotMatch(lastVariantSite))
            {
                //are we allowed to start a new chain on the old batch?
                if (ItsOKToAddANbhdToThisBatch)
                {
                    //start a new chain
                    AddNewNeighborhoodToBatch(lastVariantSite, currentVariantSite, referenceStringBetweenVariants,
                        numNbhdsSoFar);

                    return true;
                }
                else    //We cant add any more nbhds to this batch. Leave the new nbhd hanging for now.
                {
                    //buffer this.
                    MakeAHangingNeighborhood(lastVariantSite, currentVariantSite, referenceStringBetweenVariants,
                     numNbhdsSoFar);

                    return false;
                }

            }
            else
            {
                //add to the old chain of the nbhd we already have.
                lastNeighborhood.AddVariantSite(currentVariantSite, referenceStringBetweenVariants);
            }

            return true;
        }

        
        private void MakeAHangingNeighborhood(VariantSite lastVariantSite, VariantSite currentVariantSite, 
            string referenceStringBetweenVariants, int numNbhdsSoFar)
        {
            //buffer this for our next call to "GetBatchOfNeighborhoods" .

            var newNeighborhood = new VcfNeighborhood(_variantCallingParams, numNbhdsSoFar + _maxNumNbhdsInBatch, currentVariantSite.ReferenceName,
                    lastVariantSite, currentVariantSite, referenceStringBetweenVariants);

            _unfinshedNeighborhoods.Add(newNeighborhood);
        }

        private void AddNewNeighborhoodToBatch(VariantSite lastVariantSite, VariantSite currentVariantSite,
            string referenceStringBetweenVariants, int numNbhdsSoFar)
        {

            int numNbhdInBatchSoFar = _nextBatchOfNeighborhoods.Count;

            var newNeighborhood = new VcfNeighborhood(_variantCallingParams, numNbhdsSoFar + numNbhdInBatchSoFar, currentVariantSite.ReferenceName,
                lastVariantSite, currentVariantSite, referenceStringBetweenVariants);
            
            _nextBatchOfNeighborhoods.Add(newNeighborhood);

        }

    }
}
