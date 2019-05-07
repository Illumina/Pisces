using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.IO;
using Common.IO.Utility;
using Pisces.Domain.Models;
using Pisces.IO.Sequencing;
using VariantPhasing.Interfaces;
using VariantPhasing.Models;

namespace VariantPhasing.Logic
{
    public class NeighborhoodBuilder : INeighborhoodBuilder
    {
        private readonly IAlleleSource _vcfVariantSource;
        private readonly Genome _genome;
        private readonly int _maxNumNbhdsInBatch;
        private readonly List<VcfNeighborhood> _nextBatchOfVcfNeighborhoods;
        private readonly List<VcfNeighborhood> _unfinshedNeighborhoods;
        private readonly VariantCallingParameters _variantCallingParams;
        private readonly PhasableVariantCriteria _phasableVariantCriteria;

        string _referenceStringBetweenVariants = "";

        VcfVariant _tempRawVcfVariant = new VcfVariant();
        VariantSite _lastVariantSite = new VariantSite();

        public bool GenomeIsSet => ( _genome != null) ;

        public NeighborhoodBuilder(PhasableVariantCriteria phasableVariantCriteria, VariantCallingParameters variantCallingParams, 
            IAlleleSource vcfVariantSource, Genome genome, int batchSize)
        {

            _variantCallingParams = variantCallingParams;
            _phasableVariantCriteria = phasableVariantCriteria;
            _vcfVariantSource = vcfVariantSource;
            _genome = genome;
            _nextBatchOfVcfNeighborhoods = new List<VcfNeighborhood>();
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

        public IEnumerable<CallableNeighborhood> GetBatchOfCallableNeighborhoods(int numNbhdsSoFar, out int rawNumNeighborhoods)
        {
            _nextBatchOfVcfNeighborhoods.Clear();
            _nextBatchOfVcfNeighborhoods.AddRange(_unfinshedNeighborhoods);
            _unfinshedNeighborhoods.Clear();

            bool keepAddingNbhdsToTheBatch = true;
            while (keepAddingNbhdsToTheBatch)
            {
                var allelesUnpackedFromVcfVariant = new List<CalledAllele>();
                keepAddingNbhdsToTheBatch = _vcfVariantSource.GetNextVariants(out allelesUnpackedFromVcfVariant);

                //only loose allelesUnpackedFromVcfVariant if it was null/empty. We are done with the file.
                if (!keepAddingNbhdsToTheBatch)
                    break;

                foreach (var currentAllele in allelesUnpackedFromVcfVariant)
                {
                   
                    if (currentAllele.Filters.Contains(FilterType.ForcedReport)) continue;

                    var currentVariantSite = new VariantSite(currentAllele);
                    var refBase = currentVariantSite.VcfReferenceAllele.Substring(0, 1);

                    //append the next base, unless we have a repeated variant.
                    // GB question - doesn't this assume gvcf?
                    if (currentVariantSite.VcfReferencePosition != _lastVariantSite.VcfReferencePosition)
                        _referenceStringBetweenVariants += refBase;

                    //check its not reference or otherwise useless
                    if (!IsEligibleVariant(currentAllele)) continue;

                    //the current variant is close to the last one
                    if (IsProximal(currentVariantSite, _lastVariantSite, _phasableVariantCriteria.PhasingDistance))
                    {
                        keepAddingNbhdsToTheBatch = FitVariantsInNeighborhood(_lastVariantSite, currentVariantSite, numNbhdsSoFar);

                        _referenceStringBetweenVariants = "";
                    }
                    else
                        _referenceStringBetweenVariants = "";

                    _lastVariantSite = currentVariantSite;
                }
            }

            rawNumNeighborhoods = _nextBatchOfVcfNeighborhoods.Count();
            var callableNeighborhoods = ConvertToCallableNeighborhoods(_nextBatchOfVcfNeighborhoods);

            return callableNeighborhoods;
        }

  
        public List<CallableNeighborhood> ConvertToCallableNeighborhoods(List<VcfNeighborhood> neighborhoods)
        {
            var callableNeighborhoods = new List<CallableNeighborhood>() { };

            if (neighborhoods.Count == 0)
                return callableNeighborhoods;

            ChrReference chrReference = null;
            var currentChr = neighborhoods[0].ReferenceName;
            
            //note, GetChrReference will return null if the sequence doesnt exist
            if (GenomeIsSet)
            {
                chrReference = _genome.GetChrReference(currentChr);
            }

            //defensively fix any ordering issues that might have come in from the vcf
            foreach (var nbhd in neighborhoods)
            {
                // TODO consider a more nuanced cutoff for this. Should it be based on a proportion? Max number of non-passing allowed? 
                // If the neighborhood doesn't have enough passing variants, skip it (unless the neighborhood consists only of passing variants)
                if (nbhd.PassingVariants < _phasableVariantCriteria.MinPassingVariantsInNbhd && nbhd.NonPassingVariants > 0)
                {
                    continue;
                }

                //Make sure our reference it the right one. If not, update. If it doenst exist, thats fine too.
                if ((nbhd.ReferenceName != currentChr) && (GenomeIsSet))
                {
                    chrReference = _genome.GetChrReference(nbhd.ReferenceName);
                    currentChr = nbhd.ReferenceName;
                }

                callableNeighborhoods.Add(new CallableNeighborhood(nbhd, _variantCallingParams, chrReference));
            }

            return callableNeighborhoods;
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

            if (allele.Type == AlleleCategory.Unsupported)
            {
                Logger.WriteToLog(string.Join('\t',"The following variant is an unsupported type and shall not be phased:",
                    allele.Chromosome, allele.ReferencePosition, allele.ReferenceAllele, allele.AlternateAllele));
                return false;
            }


            if (_phasableVariantCriteria.HetVariantsOnly) //usually false
            {
                //by default, we allow this.
                if (genotype == Genotype.HomozygousAlt)
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
        public bool FitVariantsInNeighborhood(VariantSite lastVariantSite, VariantSite currentVariantSite, int numNbhdsSoFar)
        {

            bool ItsOKToAddANbhdToThisBatch = _nextBatchOfVcfNeighborhoods.Count < _maxNumNbhdsInBatch;


            //if no batches exist yet.. We have to add one.
            if (_nextBatchOfVcfNeighborhoods.Count == 0)
            {
                AddNewNeighborhoodToBatch(lastVariantSite, currentVariantSite, numNbhdsSoFar);

                return ItsOKToAddANbhdToThisBatch;
            }

            //else, we previously had a nbhd we were working on:
            var lastNeighborhood = _nextBatchOfVcfNeighborhoods.Last();

            // Have we skipped any positions since our last addition to this chain? If so, we need to make a new neighborhood. 
            // Othwerise, we can add on to the old chain
            if (lastNeighborhood.LastPositionIsNotMatch(lastVariantSite))
            {
                //are we allowed to start a new chain on the old batch?
                if (ItsOKToAddANbhdToThisBatch)
                {
                    //start a new chain
                    AddNewNeighborhoodToBatch(lastVariantSite, currentVariantSite, numNbhdsSoFar);

                    return true;
                }
                else    //We cant add any more nbhds to this batch. Leave the new nbhd hanging for now.
                {
                    //buffer this.
                    MakeAHangingNeighborhood(lastVariantSite, currentVariantSite, numNbhdsSoFar);

                    return false;
                }

            }
            else
            {
                //add to the old chain of the nbhd we already have.
                lastNeighborhood.AddVariantSite(currentVariantSite);
            }

            return true;
        }

        
        private void MakeAHangingNeighborhood(VariantSite lastVariantSite, VariantSite currentVariantSite, int numNbhdsSoFar)
        {
            //buffer this for our next call to "GetBatchOfNeighborhoods" .

            var newNeighborhood = new VcfNeighborhood(numNbhdsSoFar + _maxNumNbhdsInBatch, currentVariantSite.ReferenceName,
                    lastVariantSite, currentVariantSite);

            _unfinshedNeighborhoods.Add(newNeighborhood);
        }

        private void AddNewNeighborhoodToBatch(VariantSite lastVariantSite, VariantSite currentVariantSite, int numNbhdsSoFar)
        {

            int numNbhdInBatchSoFar = _nextBatchOfVcfNeighborhoods.Count;

            var newNeighborhood = new VcfNeighborhood(numNbhdsSoFar + numNbhdInBatchSoFar, currentVariantSite.ReferenceName,
                lastVariantSite, currentVariantSite);
            
            _nextBatchOfVcfNeighborhoods.Add(newNeighborhood);

        }

    }
}
