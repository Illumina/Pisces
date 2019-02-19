using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;
using Common.IO.Utility;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using CommandLine.Options;

namespace AdaptiveGenotyper
{
    public class VariantReader
    {
        private const double MultiAllelicThreshold = 0.8;
        private const double HetThreshold = 0.2;
        private const double HomAltThreshold = 0.7;

        #region Fields
        private RecalibratedVariantsCollection SnvLoci = new RecalibratedVariantsCollection();
        private RecalibratedVariantsCollection IndelLoci = new RecalibratedVariantsCollection();
        private VcfReader Reader;
        #endregion

        public List<RecalibratedVariantsCollection> GetVariantFrequencies(string vcfIn)
        {
            VcfVariant variant = new VcfVariant();
            VcfVariant lastVar = new VcfVariant();

            Reader = new VcfReader(vcfIn);

            // Check headers
            CheckHeader();

            using (Reader)
            {

                while (Reader.GetNextVariant(variant))
                {
                    try
                    {
                        // Check if multiallelic
                        if (variant.ReferencePosition == lastVar.ReferencePosition &&
                                variant.ReferenceName == lastVar.ReferenceName)
                            variant = ProcessMultiAllelicVariant(lastVar, variant);

                        // Check if within deletion
                        if (lastVar.ReferenceAllele != null &&
                                lastVar.ReferenceAllele.Length > 1 &&
                                lastVar.Genotypes[0]["GT"] != "0/0" &&
                                variant.ReferencePosition == lastVar.ReferencePosition + 1)
                            variant = ProcessDeletion(lastVar, variant);

                        // this happens if last variants in file are multi-allelic or a deletion
                        if (variant.InfoFields == null)
                            break;

                        if (ShouldSkipVariant(variant) || !variant.ReferenceName.Any(char.IsDigit))
                            continue;

                        var variantType = GetVariantType(variant);
                        if (variantType == VariantType.NoVariant)
                        {
                            SnvLoci.AddLocus(variant);
                            IndelLoci.AddLocus(variant);
                        }
                        else if (variantType == VariantType.Snv)
                            SnvLoci.AddLocus(variant);
                        else if (variantType == VariantType.Indel)
                            IndelLoci.AddLocus(variant);

                        lastVar = variant;
                        variant = new VcfVariant();
                    }

                    catch (Exception ex)
                    {
                        Logger.WriteToLog(string.Format("Fatal error processing vcf; Check {0}, position {1}.  Exception: {2}",
                            variant.ReferenceName, variant.ReferencePosition, ex));
                        throw;
                    }
                }
            }
            return new List<RecalibratedVariantsCollection> { SnvLoci, IndelLoci };
        }

        private void CheckHeader()
        {
            string piscesCmd = Reader.HeaderLines.FirstOrDefault(str => str.Contains("##Pisces_cmdline")).Split("\"\"")[1];
            var piscesOptions = new PiscesApplicationOptions();
            var appOptionParser = new PiscesOptionsParser();
            appOptionParser.ParseArgs(piscesCmd.Split(null));

            // Check if VCF is diploid
            if (appOptionParser.PiscesOptions.VariantCallingParameters.PloidyModel is PloidyModel.DiploidByAdaptiveGT ||
                appOptionParser.PiscesOptions.VariantCallingParameters.PloidyModel is PloidyModel.DiploidByThresholding)
                throw new Exception("Adaptive Genotyper should be used with VCFs that are called as somatic " +
                    "VCFs by Pisces.  Please check the input VCF file.");

            // Check if VCF is crushed
            else if (appOptionParser.PiscesOptions.VcfWritingParameters.ForceCrush == true)
                throw new Exception("Adaptive Genotyper should be used with uncrushed VCFs.  Please check the input VCF file.");

            // Check if GVCF or --minvq 0
            else if (appOptionParser.PiscesOptions.VcfWritingParameters.OutputGvcfFile == false &&
                    (appOptionParser.PiscesOptions.VariantCallingParameters.MinimumVariantQScore > 0 ||
                    appOptionParser.PiscesOptions.VariantCallingParameters.MinimumFrequency > 0.02))
                throw new Exception("Adaptive Genotyper should be used with GVCFs or with option -minvq 0.  Please" +
                    " check in the input VCF file.");
        }

        private VcfVariant ProcessDeletion(VcfVariant deletionVar, VcfVariant variant)
        {
            VcfVariant lastVar;
            for (int i = 1; i < deletionVar.ReferenceAllele.Length; i++)
            {
                if (ShouldSkipVariant(variant))
                    continue;

                if (variant.Genotypes[0]["GT"].Contains("1"))
                {
                    if (GetVariantType(variant) == VariantType.Snv)
                        SnvLoci.AddLocus(variant);
                    else
                        IndelLoci.AddLocus(variant);
                }

                lastVar = variant;
                variant = new VcfVariant();
                Reader.GetNextVariant(variant);

                // If there is multiallelic variant inside deletion, ignore locus
                if (variant.ReferencePosition == lastVar.ReferencePosition && variant.ReferenceName == lastVar.ReferenceName &&
                    lastVar.Genotypes[0]["GT"].Contains("1"))
                {
                    if (GetVariantType(lastVar) == VariantType.Snv)
                        SnvLoci.RemoveLastEntry();
                    else
                        IndelLoci.RemoveLastEntry();
                }
                while (variant.ReferencePosition == lastVar.ReferencePosition && 
                    variant.ReferenceName == lastVar.ReferenceName)
                {
                    lastVar = variant;
                    variant = new VcfVariant();
                    Reader.GetNextVariant(variant);
                }

                if (variant.ReferencePosition > deletionVar.ReferencePosition + deletionVar.ReferenceAllele.Length - 1 &&
                    variant.ReferenceName == deletionVar.ReferenceName)
                    break;
            }

            return variant;
        }

        private VcfVariant ProcessMultiAllelicVariant(VcfVariant lastVar, VcfVariant variant)
        {
            // SNPs and insertions are processed the same way--check and see if the two major variants have total VF > 0.8
            // 1/2 variants should not be used in the modeling because this model is for alignment bias of the reference

            List<VcfVariant> variants = new List<VcfVariant>() { lastVar, variant };

            // Use VF to find the two major variants
            List<double> vf = new List<double>() { double.Parse(lastVar.Genotypes[0]["VF"]),
                double.Parse(variant.Genotypes[0]["VF"])};
            int[] topIndices = new int[] { 0, 1 };
            Array.Sort(vf.ToArray(), topIndices);
            Array.Reverse(topIndices);

            // Keep track of ref vf
            // NB: refVf is only approximate and could be negative if ref alleles are different lengths
            double refVf = 1 - vf[0] - vf[1];

            int currIndex = 2;
            while (true)
            {
                variant = new VcfVariant();
                Reader.GetNextVariant(variant);
                if (variant.ReferencePosition != lastVar.ReferencePosition || variant.ReferenceName != lastVar.ReferenceName)
                    break;

                // Handle variant and update top 2 in VF
                variants.Add(variant);
                double newVf = double.Parse(variant.Genotypes[0]["VF"]);
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
            }

            // Remove the last entry
            if (GetVariantType(variants[0]) == VariantType.Snv)
                SnvLoci.RemoveLastEntry();
            else if (GetVariantType(variants[0]) == VariantType.Indel)
                IndelLoci.RemoveLastEntry();

            // Determine type of entry
            RecalibratedVariantsCollection currLoci;
            if     (GetVariantType(variants[topIndices[0]]) == VariantType.Snv && 
                    GetVariantType(variants[topIndices[1]]) == VariantType.Snv)
                currLoci = SnvLoci;
            else if (GetVariantType(variants[topIndices[0]]) == VariantType.Indel &&
                     GetVariantType(variants[topIndices[1]]) == VariantType.Indel)
                currLoci = IndelLoci;
            else // mixed type
                return variant;

            if ((currLoci == SnvLoci &&                                                         // multiallelic check for SNVs
                    (double.Parse(variants[topIndices[0]].Genotypes[0]["VF"]) +                  // top 2 VF > 0.8
                    double.Parse(variants[topIndices[1]].Genotypes[0]["VF"]) > MultiAllelicThreshold ||
                    double.Parse(variants[topIndices[0]].Genotypes[0]["VF"]) + refVf > MultiAllelicThreshold) ||

                    currLoci == IndelLoci) &&          

                    !ShouldSkipVariant(variants[topIndices[0]]) &&                                 // should not skip variant

                    !(vf[topIndices[0]] > HetThreshold && vf[topIndices[0]] < HomAltThreshold && 
                        vf[topIndices[1]] > HetThreshold))   // not 1/2

                currLoci.AddLocus(variants[topIndices[0]]);

            return variant;
        }

        #region Static methods
        public static bool ShouldSkipVariant(VcfVariant variant)
        {
            int dp = ParseDepth(variant);
            if (variant.Filters.ToLower().Contains("lowdp") )//|| dp > 1000) // DP > 1000 causes some problems with the binomial probability
                                                                          // because the binomial is not dispersed enough to fully model data
                return true;
            else if (double.Parse(variant.Genotypes[0]["VF"]) < 0.02 && 
                variant.ReferenceAllele.Length == variant.VariantAlleles[0].Length)
                return true;
            else if (variant.ReferenceAllele.Length != variant.VariantAlleles[0].Length &&
                   double.Parse(variant.Genotypes[0]["VF"]) == 1)
                return true;
            else
                return false;
        }

        public static int ParseAlleleSupport(VcfVariant variant)
        {
            if (variant.Genotypes[0].ContainsKey("AD"))
            {
                string[] field = variant.Genotypes[0]["AD"].Split(',');
                if (field.Length == 1)
                    return ParseDepth(variant) - int.Parse(field[0]);
                else
                    return int.Parse(field[1]);
            }
            else
                throw new Exception("VCF did not report allele support");
        }
        public static int ParseDepth(VcfVariant variant)
        {
            int dp;
            if (variant.InfoFields.ContainsKey("DP"))
                dp = int.Parse(variant.InfoFields["DP"]);
            else if (variant.Genotypes[0].ContainsKey("DP"))
                dp = int.Parse(variant.Genotypes[0]["DP"]);
            else
                throw new Exception("VCF did not report total read depth.");

            return dp;
        }

        public static VariantType GetVariantType(VcfVariant variant)
        {
            if (variant.ReferenceAllele == variant.VariantAlleles[0] ||
                    (variant.ReferenceAllele.Length == 1 && variant.VariantAlleles[0] == "."))
                return VariantType.NoVariant;
            else if (variant.ReferenceAllele.Length == 1 && variant.VariantAlleles[0].Length == 1)
                return VariantType.Snv;
            else if (variant.ReferenceAllele.Length > 1 || variant.VariantAlleles[0].Length > 1)
                return VariantType.Indel;
            else
                return VariantType.Error;
        }
        #endregion        
    }

    public enum VariantType
    {
        NoVariant,
        Snv,
        Indel,
        Error
    }
}
