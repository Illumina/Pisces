using System;
using System.Linq;
using System.Collections.Generic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Calculators
{
  

    public class GenotypeCalculator
    {

        public class DiploidThresholdingParameters
        {
            public float MinorVF = 0.20f;  //could make separate threshold values for SNP and Indel...
            public float MajorVF = 0.70f;
            public float SumVFforMultiAllelicSite = 0.80f;

            public DiploidThresholdingParameters()
            {
            }

            //not too safe, but dev use only.
            public DiploidThresholdingParameters(float[] parameters)
            {
                MinorVF = parameters[0];
                MajorVF = parameters[1];
                SumVFforMultiAllelicSite = parameters[2];
            }

        
        }

        GenotypeModel _genotypeModel = GenotypeModel.None;
        PloidyModel _ploidyModel = PloidyModel.Somatic;
        DiploidThresholdingParameters _diploidThresholdingParameters = new DiploidThresholdingParameters();

        float _minFrequency = 0.01f;

        public GenotypeCalculator(GenotypeModel genotypeModel, PloidyModel ploidyModel, 
            float minFrequency, DiploidThresholdingParameters parameters)
        {
            _genotypeModel = genotypeModel;
            _minFrequency = minFrequency;
            _ploidyModel = ploidyModel;
            _diploidThresholdingParameters = parameters;
        }



        public List<BaseCalledAllele> SetGenotypes(IEnumerable<BaseCalledAllele> alleles)
        {
            var allelesToPrune = new List<BaseCalledAllele>();

            if (_ploidyModel == PloidyModel.Somatic)
            {
                foreach (var allele in alleles)
                {
                    allele.Genotype = CalculateSomaticGenotype(allele, _genotypeModel, _minFrequency);
                }
            }
            else if (_ploidyModel == PloidyModel.Diploid)
            {
                
                var singleGTForLoci = CalculateDiploidGenotype(alleles, _genotypeModel, _diploidThresholdingParameters, out allelesToPrune );

                foreach (var allele in alleles)
                {
                    allele.Genotype = singleGTForLoci;
                }
            
            }
            else
            {
                throw new NotImplementedException("This ploidy model has no associated GT models: " + _ploidyModel);
            }

            return allelesToPrune;
        }

        private static double GetReferenceFrequency(IEnumerable<BaseCalledAllele> alleles, double minorVF)
        {
            double altFrequencyCount = 0;

            if (alleles.Count() == 0)
                return 0;

            foreach (var allele in alleles)
            {
                
                if (allele is CalledReference)
                {
                    return ((CalledReference)allele).Frequency;
                }
                if (allele is CalledVariant)
                {
                    altFrequencyCount += allele.Frequency;
                    if (allele.Type == AlleleCategory.Snv)
                        return ((CalledVariant)allele).RefFrequency;
                }
            }

            //we only get here if all the calls are indels or MNVS, ie a 1/2 GT
            //in which case (since MNV and indel, the reference counts are just equal to ~not MNV or indel)
            // so the % ref boils down to our best estimate.

            //we know its got to be less that the MinorVF, and also less than the sum or the VF calls put together.

            //this is a bit of a hack for very rare cases. if we fixed the fact that the ref counts on indels
            //are not the true ref count, we can take this out.

            return Math.Min( (1.0 - altFrequencyCount), minorVF - 0.0001);
        }

        private static List<BaseCalledAllele> FilterAndOrderAllelesByFrequency(IEnumerable<BaseCalledAllele> alleles, List<BaseCalledAllele> allelesToPrune,
            double minFreqThreshold)
        {
            var variantAlleles = new List<BaseCalledAllele>();

            foreach (var allele in alleles)
            {
                if (allele is CalledVariant)
                {
                    if (((CalledVariant)allele).Frequency >= minFreqThreshold)
                    {
                        variantAlleles.Add(allele);
                    }
                    else
                        allelesToPrune.Add(allele);
                }
            }

            variantAlleles = variantAlleles.OrderByDescending(p => p.Frequency).ToList();

            return variantAlleles;
        }

        private static bool CheckForTriAllelicIssue(bool hasReference, double referenceFreq, List<BaseCalledAllele> variantAlleles, double threshold)
        {

            if (hasReference && ((variantAlleles[0].Frequency + referenceFreq) < threshold))
                return true;

            // if the top two VFs are less than some threshold, we have 3 alt calls at this site, and thats a problem.           
            return ( (variantAlleles[0].Frequency + variantAlleles[1].Frequency) < threshold );
        }

        private static void SetMultiAllelicFilter(IEnumerable<BaseCalledAllele> alleles)
        {
            foreach (var allele in alleles)
            {
                allele.Filters.Add(FilterType.MultiAllelicSite);
            }
        }

        private static bool CheckForDepthIssue(IEnumerable<BaseCalledAllele> alleles)
        {
            foreach (var allele in alleles)
            {
                if (allele.Filters.Contains(FilterType.LowDepth))
                {
                    return true;
                }
            }
            return false;
        }

        private static Genotype CalculateDiploidGenotype(IEnumerable<BaseCalledAllele> alleles,
            GenotypeModel model, DiploidThresholdingParameters thresholdingParameters, out List<BaseCalledAllele> allelesToPrune)
        {
            allelesToPrune = new List<BaseCalledAllele>();
            var singleGTForLoci = Genotype.RefLikeNoCall;
            var orderedVariants = FilterAndOrderAllelesByFrequency(alleles, allelesToPrune, thresholdingParameters.MinorVF);
            var referenceFrequency = GetReferenceFrequency(alleles, thresholdingParameters.MinorVF);
            var refExists = (referenceFrequency >= thresholdingParameters.MinorVF);
            var depthIssue = CheckForDepthIssue(alleles);
            var diploidModelFail = false;  // as in {30%,30%,30%} not {49%,49%,2%},ie, diploid model FAIL.
            bool refCall = ((orderedVariants.Count == 0) || (orderedVariants[0].Frequency < thresholdingParameters.MinorVF));

            if (depthIssue)
            {
                if (refCall)
                    singleGTForLoci = Genotype.RefLikeNoCall;
                else
                    singleGTForLoci = Genotype.AltLikeNoCall;
            }
            else
            {
                //obvious reference call
                if (refCall)
                {
                    singleGTForLoci = Genotype.HomozygousRef;  // being explicit for readability
                }  //else, types of alt calls...
                else if ((orderedVariants[0].Frequency >= thresholdingParameters.MinorVF) && (orderedVariants[0].Frequency <= thresholdingParameters.MajorVF))
                {
                    if (orderedVariants.Count == 1)
                    {
                        singleGTForLoci = Genotype.HeterozygousAltRef;
                    }
                    else
                    {
                        //is this 0/1, 1/2, or 0/1/2 or 1/2/3/...
                        diploidModelFail = CheckForTriAllelicIssue(refExists, referenceFrequency, orderedVariants, thresholdingParameters.SumVFforMultiAllelicSite);
                        if (diploidModelFail)
                        {
                            SetMultiAllelicFilter(alleles);

                            if (refExists)
                                singleGTForLoci = Genotype.AltLikeNoCall;
                            else
                                singleGTForLoci = Genotype.Alt12LikeNoCall;


                        }
                        else if (refExists)
                        {
                            singleGTForLoci = Genotype.HeterozygousAltRef;
                        }
                        else
                        {
                            singleGTForLoci = Genotype.HeterozygousAlt1Alt2;
                        }
                    }
                }
                else if (orderedVariants[0].Frequency > thresholdingParameters.MajorVF)
                {
                    singleGTForLoci = Genotype.HomozygousAlt;
                }
            }

            if (!diploidModelFail)
                allelesToPrune = GetAllelesToPruneBasedOnGTCall(singleGTForLoci, orderedVariants, allelesToPrune);

            //else (if diploidModelFail== true) it *really does have multiple alleles*, we will flag this site as a no call. and report everything we found
            // on one ugle line. some how, this site did not act diploid.

            return singleGTForLoci;
        }

        private static List<BaseCalledAllele> GetAllelesToPruneBasedOnGTCall(Genotype singleGTForLoci,
            List<BaseCalledAllele> orderedVariants, List<BaseCalledAllele> allelesToPrune)
        {
            int allowedNumVarAlleles = 0;

            switch (singleGTForLoci)
            {
                case Genotype.AltLikeNoCall:
                case Genotype.HomozygousAlt:
                case Genotype.HeterozygousAltRef:
                    {
                        allowedNumVarAlleles = 1;
                        break;
                    }
                case Genotype.Alt12LikeNoCall:
                case Genotype.HeterozygousAlt1Alt2:
                    {
                        allowedNumVarAlleles = 2;
                        break;
                    }
                default:
                    {
                        allowedNumVarAlleles = 0;
                        break;
                    }
            }

            for (int i = 0; i < orderedVariants.Count; i++)
            {
                if (i >= allowedNumVarAlleles)
                    allelesToPrune.Add(orderedVariants[i]);

            }
            return allelesToPrune;
        }

        private static Genotype CalculateSomaticGenotype(BaseCalledAllele allele, GenotypeModel model, float minFrequency)
        {

            var defaultGenotype = allele.Genotype;//<- would prefer to replace this with more deterministic behavior

            if (allele.Filters.Contains(FilterType.LowDepth))
            {
                return (allele is CalledReference ? Genotype.RefLikeNoCall : Genotype.AltLikeNoCall);
            }
            
            if (allele is CalledVariant && model != GenotypeModel.None)
            {
                var variant = (CalledVariant)allele;

                // if we see no evidence of a reference allele, according to the genotype model
                // then presume our variant is a homozygous alt
                if (variant.RefFrequency < (model == GenotypeModel.Thresholding ? minFrequency : 0.25f))
                {
                    //if we are using the "none" model, if we see less than 25% reference,
                    return Genotype.HomozygousAlt;
                }
                return Genotype.HeterozygousAltRef;
            }

            if (allele is CalledReference && allele.Frequency == 0f)
                return Genotype.RefLikeNoCall;

            return defaultGenotype;
        }
    }
}
