using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;
using Common.IO.Utility;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using CommandLine.Options;

namespace AdaptiveGenotyper
{
    public static class VariantReader
    {
        private const double MultiAllelicThreshold = 0.8;
        private const double HetThreshold = 0.2;
        private const double HomAltThreshold = 0.7;

        public static bool GetNextUncrushedAllele(AlleleReader reader, out CalledAllele variant)
        {
            var nextVariants = new List<CalledAllele>();
            bool worked = reader.GetNextVariants(out nextVariants);
            variant = null;

            if (worked)
            {
                if (nextVariants.Count > 1)
                    throw new ArgumentException("Input file should not have crushed variants. There should only be one variant per line");

                variant = nextVariants[0];
            }

            return worked;
        }

        public static List<RecalibratedVariantsCollection> GetVariantFrequencies(string vcfIn)
        {
            CalledAllele variant = new CalledAllele();
            CalledAllele lastVar = new CalledAllele();

            var snvLoci = new RecalibratedVariantsCollection();
            var indelLoci = new RecalibratedVariantsCollection();

            var alleleReader = new AlleleReader(vcfIn);

            // Check headers
            CheckHeader(alleleReader);

            using (alleleReader)
            {

                while (GetNextUncrushedAllele(alleleReader, out variant))
                {
                    try
                    {
                        // Check if multiallelic
                        if (variant.IsCoLocatedAllele(lastVar))
                            variant = ProcessMultiAllelicVariant(lastVar, variant, alleleReader, snvLoci, indelLoci);

                        // Check if within deletion
                        if (lastVar.ReferenceAllele != null && variant != null &&
                                lastVar.ReferenceAllele.Length > 1 &&
                                lastVar.Genotype != Genotype.HomozygousRef &&
                                variant.ReferencePosition == lastVar.ReferencePosition + 1)
                            variant = ProcessDeletion(lastVar, variant, alleleReader, snvLoci, indelLoci);


                        // this happens if last variants in file are multi-allelic or a deletion
                        if (variant == null)
                            break;

                        if (ShouldSkipVariant(variant) || !variant.Chromosome.Any(char.IsDigit))
                            continue;

                        var variantType = GetVariantType(variant);
                        if (variantType == VariantType.NoVariant)
                        {
                            snvLoci.AddLocus(variant);
                            indelLoci.AddLocus(variant);
                        }
                        else if (variantType == VariantType.Snv)
                            snvLoci.AddLocus(variant);
                        else if (variantType == VariantType.Indel)
                            indelLoci.AddLocus(variant);

                        lastVar = variant;
                        variant = new CalledAllele();
                    }

                    catch (Exception ex)
                    {
                        Logger.WriteToLog(string.Format("Fatal error processing vcf; Check {0}, position {1}.  Exception: {2}",
                            variant.Chromosome, variant.ReferencePosition, ex));
                        throw;
                    }
                }
            }
            return new List<RecalibratedVariantsCollection> { snvLoci, indelLoci };
        }

        private static void CheckHeader(AlleleReader reader)
        {
            string piscesCmd = reader.HeaderLines.FirstOrDefault(str => str.Contains("##Pisces_cmdline")).Split("\"\"")[1];
            var appOptionParser = new PiscesOptionsParser();
            appOptionParser.ParseArgs(piscesCmd.Split(null));

            // Check if VCF is diploid
            if (appOptionParser.PiscesOptions.VariantCallingParameters.PloidyModel == PloidyModel.DiploidByAdaptiveGT ||
                appOptionParser.PiscesOptions.VariantCallingParameters.PloidyModel == PloidyModel.DiploidByThresholding)
                throw new VariantReaderException("Adaptive Genotyper should be used with VCFs that are called as somatic " +
                    "VCFs by Pisces.  Please check the input VCF file.");

            // Check if VCF is crushed
            else if (appOptionParser.PiscesOptions.VcfWritingParameters.ForceCrush == true)
                throw new VariantReaderException("Adaptive Genotyper should be used with uncrushed VCFs.  Please check the input VCF file.");

            // Check if GVCF or --minvq 0
            else if (!appOptionParser.PiscesOptions.VcfWritingParameters.OutputGvcfFile &&
                    (appOptionParser.PiscesOptions.VariantCallingParameters.MinimumVariantQScore > 0 ||
                    appOptionParser.PiscesOptions.VariantCallingParameters.MinimumFrequency > 0.02))
                throw new VariantReaderException("Adaptive Genotyper should be used with GVCFs or with option -minvq 0.  Please" +
                    " check in the input VCF file.");
        }

        private static CalledAllele ProcessDeletion(CalledAllele deletionVar, CalledAllele variant, AlleleReader reader,
            RecalibratedVariantsCollection snvLoci, RecalibratedVariantsCollection indelLoci)
        {
            CalledAllele lastVar;
            for (int i = 1; i < deletionVar.ReferenceAllele.Length; i++)
            {
                if (ShouldSkipVariant(variant))
                    continue;

                if (variant.HasAnAltAllele)
                {
                    if (GetVariantType(variant) == VariantType.Snv)
                        snvLoci.AddLocus(variant);
                    else
                        indelLoci.AddLocus(variant);
                }

                lastVar = variant;
                variant = new CalledAllele();

                GetNextUncrushedAllele(reader, out variant);

                // If there is multiallelic variant inside deletion, ignore locus
                if (variant.IsCoLocatedAllele(lastVar) && lastVar.HasAnAltAllele)
                {
                    if (GetVariantType(lastVar) == VariantType.Snv)
                        snvLoci.RemoveLastEntry();
                    else
                        indelLoci.RemoveLastEntry();
                }
                while (variant.ReferencePosition == lastVar.ReferencePosition &&
                    variant.Chromosome == lastVar.Chromosome)
                {
                    lastVar = variant;
                    variant = new CalledAllele();
                    GetNextUncrushedAllele(reader, out variant);
                }

                if (variant.ReferencePosition > deletionVar.ReferencePosition + deletionVar.ReferenceAllele.Length - 1 &&
                    variant.Chromosome == deletionVar.Chromosome)
                    break;
            }

            return variant;
        }

        private static CalledAllele ProcessMultiAllelicVariant(CalledAllele lastVar, CalledAllele variant,
            AlleleReader reader, RecalibratedVariantsCollection snvLoci, RecalibratedVariantsCollection indelLoci)
        {
            // SNPs and insertions are processed the same way--check and see if the two major variants have total VF > 0.8
            // 1/2 variants should not be used in the modeling because this model is for alignment bias of the reference

            List<CalledAllele> variants = new List<CalledAllele>() { lastVar, variant };

            // Use VF to find the two major variants
            List<double> vf = new List<double>()
            {
                GetAlternateAlleleFrequency(lastVar),
                GetAlternateAlleleFrequency(variant)
            };
            int[] topIndices = new int[] { 0, 1 };
            Array.Sort(vf.ToArray(), topIndices);
            Array.Reverse(topIndices);

            // Keep track of ref vf
            // NB: refVf is only approximate and could be negative if ref alleles are different lengths
            double refVf = 1 - vf[0] - vf[1];

            int currIndex = 2;
            while (GetNextUncrushedAllele(reader, out variant))
            {
                if (!variant.IsCoLocatedAllele(lastVar))
                    break;

                // Handle variant and update top 2 in VF
                variants.Add(variant);
                double newVf = GetAlternateAlleleFrequency(variant);
                vf.Add(newVf);
                if (newVf > vf[topIndices[0]])
                {
                    topIndices[1] = topIndices[0];
                    topIndices[0] = currIndex;
                }
                else if (newVf > vf[topIndices[1]])
                    topIndices[1] = currIndex;

                refVf = refVf - vf[currIndex];
                currIndex++;
                lastVar = variant;
                variant = new CalledAllele();
            }

            // Remove the last entry
            if (GetVariantType(variants[0]) == VariantType.Snv)
                snvLoci.RemoveLastEntry();
            else if (GetVariantType(variants[0]) == VariantType.Indel)
                indelLoci.RemoveLastEntry();

            // Determine type of entry
            RecalibratedVariantsCollection currLoci;
            if (GetVariantType(variants[topIndices[0]]) == VariantType.Snv &&
                    GetVariantType(variants[topIndices[1]]) == VariantType.Snv)
                currLoci = snvLoci;
            else if (GetVariantType(variants[topIndices[0]]) == VariantType.Indel &&
                     GetVariantType(variants[topIndices[1]]) == VariantType.Indel)
                currLoci = indelLoci;
            else // mixed type
                return variant;

            if ((currLoci == snvLoci &&                                                                                 // multiallelic check for SNVs
                    (GetAlternateAlleleFrequency(variants[topIndices[0]]) + GetAlternateAlleleFrequency(variants[topIndices[1]]) > MultiAllelicThreshold ||   // top 2 VF > 0.8
                    GetAlternateAlleleFrequency(variants[topIndices[0]]) + refVf > MultiAllelicThreshold) ||

                    currLoci == indelLoci) &&

                    !ShouldSkipVariant(variants[topIndices[0]]) &&                                                      // should not skip variant

                    !(vf[topIndices[0]] > HetThreshold && vf[topIndices[0]] < HomAltThreshold && vf[topIndices[1]] > HetThreshold))  // not 1/2

                currLoci.AddLocus(variants[topIndices[0]]);

            return variant;
        }

        public static bool ShouldSkipVariant(CalledAllele variant)
        {
            if (variant.Filters.Contains(FilterType.LowDepth))
                return true;

            else if (GetAlternateAlleleFrequency(variant) < 0.02 &&
                    variant.ReferenceAllele.Length == variant.AlternateAllele.Length)
                return true;

            else if (variant.ReferenceAllele.Length != variant.AlternateAllele.Length &&
                   (GetAlternateAlleleFrequency(variant) == 1))
                return true;

            else
                return false;
        }

        public static int GetAlternateAlleleSupport(CalledAllele variant)
        {
            if (variant.HasAnAltAllele)
                return variant.AlleleSupport;
            else
                return variant.TotalCoverage - variant.AlleleSupport;
        }

        public static double GetAlternateAlleleFrequency(CalledAllele variant)
        {
            if (variant.HasAnAltAllele)
                return variant.Frequency;
            else
                return (double)GetAlternateAlleleSupport(variant) / variant.TotalCoverage;
        }

        public static VariantType GetVariantType(CalledAllele variant)
        {
            if (variant.IsRefType)
                return VariantType.NoVariant;
            else if (variant.Type == AlleleCategory.Snv)
                return VariantType.Snv;
            else if (variant.ReferenceAllele.Length > 1 || variant.AlternateAllele.Length > 1)
                return VariantType.Indel;
            else
                return VariantType.Error;
        }  
    }

    public class VariantReaderException : Exception
    {
        public VariantReaderException(string msg) : base(msg) { }
    }

    public enum VariantType
    {
        NoVariant,
        Snv,
        Indel,
        Error
    }
}
