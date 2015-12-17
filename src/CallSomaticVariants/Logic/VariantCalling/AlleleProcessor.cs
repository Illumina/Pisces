using System;
using System.Collections.Generic;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Logic.VariantCalling
{
    public static class AlleleProcessor
    {
        public static void Process(BaseCalledAllele allele, GenotypeModel model, 
            float minFrequency, int minCoverage, int? filterVariantQscore, bool filterSingleStrandVariants)
        {
            SetFractionNoCall(allele);
            ApplyFilters(allele, minCoverage, filterVariantQscore, filterSingleStrandVariants);
            SetGenotype(allele, model, minFrequency);
        }

        private static void SetGenotype(BaseCalledAllele allele, GenotypeModel model, float minFrequency)
        {
            if (allele.Filters.Contains(FilterType.LowDepth))
            {
                allele.Genotype = allele is CalledReference ? Genotype.RefLikeNoCall : Genotype.AltLikeNoCall;
            }
            else if (allele is CalledVariant && model != GenotypeModel.None)
            {
                var variant = (CalledVariant) allele;

                // if we see no evidence of a reference allele, according to the genotype model
                // then presume our variant is a homozygous alt
                if (variant.RefFrequency < (model == GenotypeModel.Symmetrical ? minFrequency : 0.25f))
                {
                    //if we are using the thresholding model, if we see less than 25% reference,
                    variant.Genotype = Genotype.HomozygousAlt;
                }
            }
        }

        private static void SetFractionNoCall(BaseCalledAllele allele)
        {
            var allReads = (float)(allele.TotalCoverage + allele.NumNoCalls);
            if (allReads == 0)
                allele.FractionNoCalls = 0;
            else
                allele.FractionNoCalls = allele.NumNoCalls / allReads;
        }

        private static void ApplyFilters(BaseCalledAllele allele, int minCoverage, int? variantQscoreThreshold, bool filterSingleStrandVariants)
        {
            //Reset filters
            allele.Filters = new List<FilterType>();

            if (allele.TotalCoverage < minCoverage)
                allele.AddFilter(FilterType.LowDepth);

            if (variantQscoreThreshold.HasValue && allele.Qscore < variantQscoreThreshold)
                allele.AddFilter(FilterType.LowQscore);

            if (allele is CalledVariant)
            {
                if (!allele.StrandBiasResults.BiasAcceptable ||
                (filterSingleStrandVariants && !allele.StrandBiasResults.VarPresentOnBothStrands))
                    allele.AddFilter(FilterType.StrandBias);
            }
        }
    }
}
