using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.Genotyping;
using Pisces.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdaptiveGenotyper
{
    public static class LocusProcessor
    {
        public static TypeOfUpdateNeeded ProcessLocus(VcfConsumerAppOptions options, RecalibrationResults results,
    List<CalledAllele> incomingAlleles, out List<CalledAllele> outGoingAlleles)
        {
            // Use somatic call for chrM
            if (GenotypeCreator.GetPloidyForThisChr(
                    options.VariantCallingParams.PloidyModel,
                    options.VariantCallingParams.IsMale,
                    incomingAlleles.First().Chromosome) != PloidyModel.DiploidByAdaptiveGT)
            {
                return GetTypeOfUpdate((AdaptiveGtOptions)options, incomingAlleles, TypeOfUpdateNeeded.NoChangeNeeded,
                    out outGoingAlleles);
            }

            var orderedAlleles = GetTopTwoAlleles(incomingAlleles);

            if (orderedAlleles.Count == 1)
            {
                var alleles = ProcessSingleVariantLocus(incomingAlleles[0], results);
                return GetTypeOfUpdate((AdaptiveGtOptions)options, alleles, TypeOfUpdateNeeded.Modify, out outGoingAlleles);
            }
            else
            {
                var alleles = ProcessMultiAllelicLocus(orderedAlleles, results);
                return GetTypeOfUpdate((AdaptiveGtOptions)options, alleles, TypeOfUpdateNeeded.Modify, out outGoingAlleles);
            }
        }

        /// <summary>
        /// This method determines what to actually write out.
        /// </summary>
        public static TypeOfUpdateNeeded GetTypeOfUpdate(AdaptiveGtOptions options, List<CalledAllele> alleles,
            TypeOfUpdateNeeded defaultUpdate, out List<CalledAllele> outGoingAlleles)
        {
            if (!options.VcfWritingParams.OutputGvcfFile &&
                alleles[0].Genotype == Genotype.HomozygousRef ||
                alleles[0].Genotype == Genotype.RefLikeNoCall)
            {
                outGoingAlleles = new List<CalledAllele>(); // Empty list because we don't want to output anything
                return TypeOfUpdateNeeded.DeleteCompletely;
            }
            else
            {
                outGoingAlleles = alleles;
                return defaultUpdate;
            }
        }

        private static List<CalledAllele> ProcessSingleVariantLocus(CalledAllele allele,
            RecalibrationResults results)
        {
            RecalibrationResult resultForVariant = GetModelFromType(allele, results);
            return new List<CalledAllele> { UpdateVariant(allele, resultForVariant) };
        }

        private static List<CalledAllele> ProcessMultiAllelicLocus(List<CalledAllele> orderedVariants,
            RecalibrationResults results)
        {
            RecalibrationResult resultForVariant = GetModelFromType(orderedVariants[0], results);
            MixtureModelResult mixtureModelResult = GetModelResult(orderedVariants[0], resultForVariant);

            switch (mixtureModelResult.GenotypeCategory)
            {
                case SimplifiedDiploidGenotype.HomozygousRef:
                case SimplifiedDiploidGenotype.HomozygousAlt:
                    var allele = UpdateGenotypeAndQScore(orderedVariants[0], mixtureModelResult);
                    return new List<CalledAllele> { allele };

                case SimplifiedDiploidGenotype.HeterozygousAltRef:
                    orderedVariants[0].Genotype = Genotype.HeterozygousAlt1Alt2;
                    orderedVariants[1].Genotype = Genotype.HeterozygousAlt1Alt2;
                    orderedVariants = UpdateMultiAllelicQScores(orderedVariants, results);
                    return orderedVariants;

                default:
                    throw new ArgumentException("Invalid model results");
            }
        }

        private static CalledAllele UpdateVariant(CalledAllele allele, RecalibrationResult result)
        {
            // Get model results
            string key = allele.Chromosome + ":" + allele.ReferencePosition.ToString();
            MixtureModelResult mixtureModelResult;
            if (result.Variants.ContainsKey(key))
                mixtureModelResult = result.Variants[key].MixtureModelResult;
            else
                mixtureModelResult = GetModelResult(allele, result);

            return UpdateGenotypeAndQScore(allele, mixtureModelResult);
        }

        private static CalledAllele UpdateGenotypeAndQScore(CalledAllele allele, MixtureModelResult mixtureModelResult)
        {
            allele = UpdateGenotype(allele, mixtureModelResult.GenotypeCategory);
            allele.GenotypePosteriors = mixtureModelResult.GenotypePosteriors;
            allele.GenotypeQscore = mixtureModelResult.QScore;
            return allele;
        }

        private static MixtureModelResult GetModelResult(CalledAllele allele, RecalibrationResult result)
        {
            var recal = new RecalibratedVariantsCollection();
            recal.AddLocus(allele);
            return MixtureModel.UsePrefitModel(recal.Ad, recal.Dp, result.Means, result.Priors).PrimaryResult;
        }

        private static CalledAllele UpdateGenotype(CalledAllele allele, SimplifiedDiploidGenotype category)
        {
            if (category == SimplifiedDiploidGenotype.HomozygousRef)
                allele.Genotype = Genotype.HomozygousRef;
            else if (category == SimplifiedDiploidGenotype.HeterozygousAltRef)
            {
                if (allele.IsRefType)
                    allele.Genotype = Genotype.HomozygousRef;
                else if (VariantReader.GetVariantType(allele) == VariantType.Snv)
                    allele.Genotype = Genotype.HeterozygousAltRef;
                else if (VariantReader.GetVariantType(allele) == VariantType.Indel)
                    allele.Genotype = Genotype.HeterozygousAltRef;
            }
            else if (category == SimplifiedDiploidGenotype.HomozygousAlt)
            {
                if (allele.IsRefType)
                    allele.Genotype = Genotype.HomozygousRef;
                else if (VariantReader.GetVariantType(allele) == VariantType.Snv)
                    allele.Genotype = Genotype.HomozygousAlt;
                else if (VariantReader.GetVariantType(allele) == VariantType.Indel)
                    allele.Genotype = Genotype.HomozygousAlt;
            }
            return allele;
        }

        /// <summary>
        /// This method will return a list with only one CalledAllele if the top two are ref and 1 alt,
        /// but will return two if the top two are both alt alleles.
        /// </summary>
        private static List<CalledAllele> GetTopTwoAlleles(List<CalledAllele> calledAlleles)
        {
            if (calledAlleles.Count == 1)
            {
                return calledAlleles;
            }
            else
            {
                var sortedAlleles = calledAlleles.OrderByDescending(allele => allele, new AlleleFrequencySorter()).ToList();
                double refVf = 1;
                foreach (var allele in calledAlleles)
                    refVf = refVf - allele.Frequency;

                if (refVf > sortedAlleles[1].Frequency)
                    return new List<CalledAllele> { sortedAlleles[0] };
                else
                    return new List<CalledAllele> { sortedAlleles[0], sortedAlleles[1] };
            }
        }

        private static List<CalledAllele> UpdateMultiAllelicQScores(List<CalledAllele> variantList, RecalibrationResults results)
        {
            // Determine model types
            RecalibrationResult model1 = GetModelFromType(variantList[0], results);
            RecalibrationResult model2 = GetModelFromType(variantList[1], results);

            // Calculate posteriors based on multinomial distribution
            int dp = variantList[0].TotalCoverage;
            int[] ad = new int[3];
            ad[2] = variantList[0].AlleleSupport; //temp[1];
            ad[1] = variantList[1].AlleleSupport; //temp[0];
            ad[0] = dp - ad[1] - ad[2];
            if (ad[0] < 0)
            {
                ad[0] = 0;
                dp = ad[1] + ad[2];
            }

            var mixtureModelResult = MixtureModel.GetMultinomialQScores(ad, dp,
                new List<double[]> { model1.Means, model2.Means });

            // Update variant genotypes fields
            variantList[0].GenotypePosteriors = mixtureModelResult.GenotypePosteriors;
            variantList[1].GenotypePosteriors = mixtureModelResult.GenotypePosteriors;

            variantList[0].GenotypeQscore = mixtureModelResult.QScore;
            variantList[1].GenotypeQscore = mixtureModelResult.QScore;

            return variantList;
        }

        private static RecalibrationResult GetModelFromType(CalledAllele allele, RecalibrationResults results)
        {
            var variantType = VariantReader.GetVariantType(allele);

            RecalibrationResult resultForVariant;
            if (variantType == VariantType.NoVariant || variantType == VariantType.Snv)
                resultForVariant = results.SnvResults;
            else if (variantType == VariantType.Indel)
                resultForVariant = results.IndelResults;
            else
                throw new ArgumentException("Variant type unrecognized.");

            return resultForVariant;
        }

        class AlleleFrequencySorter : IComparer<CalledAllele>
        {
            public int Compare(CalledAllele allele1, CalledAllele allele2)
            {
                return ((CalledAllele)allele1).Frequency.CompareTo(((CalledAllele)allele2).Frequency);
            }
        }
    }
}
