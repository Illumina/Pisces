using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Types;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Calculators
{

    public class DiploidGenotypeCalculator : IGenotypeCalculator
    {

       
        DiploidThresholdingParameters _diploidThresholdingParameters = new DiploidThresholdingParameters();
        PloidyModel _ploidyModel = PloidyModel.Diploid;

        public int MinGQScore { get; set; }
        public int MaxGQScore { get; set; }
        public int MinDepthToGenotype { get; set; }

        public DiploidGenotypeCalculator() { }
        public DiploidGenotypeCalculator(DiploidThresholdingParameters parameters, int minCalledVariantDepth, int minGQscore, int maxGQscore)
        {
            _diploidThresholdingParameters = parameters;
            MinGQScore = minGQscore;
            MaxGQScore = maxGQscore;
            MinDepthToGenotype = minCalledVariantDepth;
        }

        public PloidyModel PloidyModel
        {
            get
            {
                return _ploidyModel;
            }
        }

        public DiploidThresholdingParameters Parameters
        {
            get
            {
                return _diploidThresholdingParameters;
            }
        }

        public List<CalledAllele> SetGenotypes(IEnumerable<CalledAllele> alleles)
        {
            var allelesToPrune = new List<CalledAllele>();


            var singleGTForLoci = CalculateDiploidGenotype(alleles, MinDepthToGenotype, _diploidThresholdingParameters, out allelesToPrune);

            foreach (var allele in alleles)
            {
                allele.Genotype = singleGTForLoci;
                allele.GenotypeQscore = DiploidGenotypeQualityCalculator.Compute(allele,MinGQScore,MaxGQScore);             
            }

            return allelesToPrune;
        }

        private static double GetReferenceFrequency(IEnumerable<CalledAllele> alleles, double minorVF)
        {
            double altFrequencyCount = 0;

            if (alleles.Count() == 0)
                return 0;

            if (alleles.Count() == 1)
                return alleles.First().RefFrequency;

            foreach (var allele in alleles)
            {

                if (allele.Type == AlleleCategory.Reference)
                {
                    return allele.Frequency;
                }
                if (allele is CalledAllele)
                {
                    altFrequencyCount += allele.Frequency;
                    if (allele.Type == AlleleCategory.Snv)
                        return ((CalledAllele)allele).RefFrequency;
                }
            }

            //we only get here if all the calls are indels or MNVS, ie a 1/2 GT
            //in which case (since MNV and indel, the reference counts are just equal to ~not MNV or indel)
            // so the % ref boils down to our best estimate.

            //we know its got to be less that the MinorVF, and also less than the sum or the VF calls put together.

            //this is a bit of a hack for very rare cases. if we fixed the fact that the ref counts on indels
            //are not the true ref count, we can take this out.

            return Math.Min((1.0 - altFrequencyCount), minorVF - 0.0001);
        }


        private static List<CalledAllele> FilterAndOrderAllelesByFrequency(IEnumerable<CalledAllele> alleles, List<CalledAllele> allelesToPrune,
            double minFreqThreshold)
        {
            var variantAlleles = new List<CalledAllele>();

            foreach (var allele in alleles)
            {
                if (allele.Type != AlleleCategory.Reference)
                {
                    if (allele.Frequency >= minFreqThreshold)
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

        private static bool CheckForTriAllelicIssue(bool hasReference, double referenceFreq, List<CalledAllele> variantAlleles, double threshold)
        {

            if (hasReference && ((variantAlleles[0].Frequency + referenceFreq) < threshold))
                return true;

            // if the top two VFs are less than some threshold, we have 3 alt calls at this site, and thats a problem.           
            return ((variantAlleles[0].Frequency + variantAlleles[1].Frequency) < threshold);
        }

        private static void SetMultiAllelicFilter(IEnumerable<CalledAllele> alleles)
        {
            foreach (var allele in alleles)
            {
                allele.Filters.Add(FilterType.MultiAllelicSite);
            }
        }

        private static bool CheckForDepthIssue(IEnumerable<CalledAllele> alleles, int minDepthToEmit)
        {
            foreach (var allele in alleles)
            {
                if (allele.TotalCoverage < minDepthToEmit)
                {
                    return true;
                }
            }
            return false;
        }

        private static Genotype CalculateDiploidGenotype(IEnumerable<CalledAllele> alleles, 
            int minDepthToGenotype, DiploidThresholdingParameters thresholdingParameters, out List<CalledAllele> allelesToPrune)
        {
            allelesToPrune = new List<CalledAllele>();
            var singleGTForLoci = Genotype.RefLikeNoCall;
            var orderedVariants = FilterAndOrderAllelesByFrequency(alleles, allelesToPrune, thresholdingParameters.MinorVF);
            var referenceFrequency = GetReferenceFrequency(alleles, thresholdingParameters.MinorVF);
            var refExists = (referenceFrequency >= thresholdingParameters.MinorVF);
            var depthIssue = CheckForDepthIssue(alleles, minDepthToGenotype);
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

                    if (!refExists)
                        singleGTForLoci = Genotype.RefLikeNoCall; //there might have been an upstream deletion
                    else
                    {
                        var firstAllele = alleles.First();

                        //we see too much of something else (unknown) for a clean ref call.
                        if ((firstAllele.Type == AlleleCategory.Reference) && ((1 - firstAllele.Frequency) > thresholdingParameters.MinorVF))
                          singleGTForLoci = Genotype.RefAndNoCall;
                        else
                            singleGTForLoci = Genotype.HomozygousRef;  // being explicit for readability

                    }
                }//else, types of alt calls...
                else if ((orderedVariants[0].Frequency >= thresholdingParameters.MinorVF) && (orderedVariants[0].Frequency <= thresholdingParameters.MajorVF))
                {
                    if (orderedVariants.Count == 1)
                    {
                        if (refExists)
                            singleGTForLoci = Genotype.HeterozygousAltRef;
                        else
                            singleGTForLoci = Genotype.AltAndNoCall;
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

            //if (!diploidModelFail)
            allelesToPrune = GetAllelesToPruneBasedOnGTCall(singleGTForLoci, orderedVariants, allelesToPrune);

            //tjd +
            //incase of DiploidModelFail, we *used* to show all alleles we detected, but then we were worried about down stream processors.
            //So now we will will prune in this case, too.
            // we still will flag this DiploidModelFail site as a no call. But no longer report everything we found
            //tjd -


            return singleGTForLoci;
        }


        private static List<CalledAllele> GetAllelesToPruneBasedOnGTCall(Genotype singleGTForLoci,
            List<CalledAllele> orderedVariants, List<CalledAllele> allelesToPrune)
        {
            int allowedNumVarAlleles = 0;

            switch (singleGTForLoci)
            {
                case Genotype.AltAndNoCall:
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

        private static Genotype CalculateSomaticGenotype(CalledAllele allele, float minFrequency)
        {

            //var defaultGenotype = allele.Genotype;//<- would prefer to replace this with more deterministic behavior
            var defaultGenotype = Genotype.HomozygousRef;

            if (allele.Filters.Contains(FilterType.LowDepth))
            {
                return ((allele.Type==AlleleCategory.Reference) ? Genotype.RefLikeNoCall : Genotype.AltLikeNoCall);
            }

            if (allele.Type != AlleleCategory.Reference)
            {
                
                // if we see no evidence of a reference allele, according to the genotype model
                // then presume our variant is a homozygous alt
                if (allele.RefFrequency < minFrequency)
                {
                    return Genotype.HomozygousAlt;
                }
                return Genotype.HeterozygousAltRef;
            }
            else if (allele.Frequency == 0f)
                return Genotype.RefLikeNoCall;
            else
                return defaultGenotype;
        }
    }
}

