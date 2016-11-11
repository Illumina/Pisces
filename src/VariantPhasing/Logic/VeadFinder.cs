using System;
using System.Collections.Generic;
using System.Text;
using Pisces.IO.Sequencing;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using VariantPhasing.Models;
using VariantPhasing.Types;

namespace VariantPhasing.Logic
{
    public class VeadFinder
    {
        private readonly BamFilterParameters _bamFilterParams;

        public VeadFinder(BamFilterParameters bamFilterParams)
        {
            _bamFilterParams = bamFilterParams;
        }

        public VariantSite[]
FindVariantResults(List<VariantSite> variantsFromVcf, BamAlignment read)
        {
            int lastPosInAlignment;
            var firstPosInAlignment = read.Position + 1;
            var dict = SetCandidateVariantsFoundInRead(_bamFilterParams.MinimumBaseCallQuality, read, out lastPosInAlignment);

            return MatchReadVariantsWithVcfVariants(variantsFromVcf, dict, firstPosInAlignment, lastPosInAlignment);
        }

        public VariantSite[]
            FindVariantResults(List<VariantSite> variantsFromVcf, Read read)
        {
            return FindVariantResults(variantsFromVcf, read.BamAlignment);

            //var firstPosInAlignment = read.Position;
            //var finder = new VariantSiteFinder(_bamFilterParams.MinimumBaseCallQuality);
            //var lastPos = (int) (firstPosInAlignment + read.CigarData.GetReferenceSpan());
            //var sites = finder.FindVariantSites(read, read.Chromosome);
            //return MatchReadVariantsWithVcfVariants(variantsFromVcf, sites, firstPosInAlignment, lastPos);
        }

        public VariantSite[] MatchReadVariantsWithVcfVariants(List<VariantSite> variantsFromVcf, Dictionary<SomaticVariantType, 
            List<VariantSite>> variantsInFoundAlignment, int firstPosInAlignment, decimal lastPosInAlignment)
        {
            var interrogationResults = new VariantSite[variantsFromVcf.Count];
            var numSitesFoundInRead = 0;

            for (var i = 0; i < variantsFromVcf.Count; i++)
            {
                var variantToLookFor = variantsFromVcf[i];
                var type = variantToLookFor.GetVariantType();


                //if the variant is out of our range
                if ((variantToLookFor.TrueFirstBaseOfDiff < firstPosInAlignment)
                    || (variantToLookFor.TrueFirstBaseOfDiff > lastPosInAlignment))
                {
                    interrogationResults[i] = SetEmptyMatch(variantToLookFor);
                    continue;
                }

                numSitesFoundInRead++;

                //if we did not find this type of variant at all
                if (variantsInFoundAlignment[type].Count == 0)
                {
                    interrogationResults[i] = SetReferenceMatch(variantToLookFor);
                    continue;
                }
                var foundThisVariant = false;
                var foundVariantThatFailedFilters = false;

                foreach (var variantFound in variantsInFoundAlignment[type])
                {
                    //if (variantFound.VcfReferencePosition <= 55589767 && (variantFound.VcfReferencePosition + (variantFound.VcfAlternateAllele.Length) >= 5558967))
                    //    Console.WriteLine("Matching candidate {0} {1}>{2}", variantFound.VcfReferencePosition, variantFound.VcfReferenceAllele, variantFound.VcfAlternateAllele);

                    if (foundThisVariant)
                        break;

                    //check we have not already gone past where this variant should be found
                    if (variantToLookFor.VcfReferencePosition < variantFound.VcfReferencePosition)
                    {
                        interrogationResults[i] = SetReferenceMatch(variantToLookFor);
                        break;
                    }

                    switch (type)
                    {
                            //if its an insertion, check the inserted bases match.
                        case SomaticVariantType.Insertion:
                        {
                            if (variantFound.VcfReferencePosition != variantToLookFor.VcfReferencePosition)
                                break;

                            var insertionSection = variantToLookFor.VcfAlternateAllele.Substring(1,
                                variantToLookFor.VcfAlternateAllele.Length - 1);

                            if (variantFound.HasNoData)
                                foundVariantThatFailedFilters = true;
                            else if (variantFound.VcfAlternateAllele == insertionSection)
                                foundThisVariant = true;

                            break;
                        }
                            //if its a deletion, check the deletion length matches
                        case SomaticVariantType.Deletion:
                        {
                            if (variantFound.VcfReferencePosition != variantToLookFor.VcfReferencePosition)
                                break;

                            var numDeletedBasesToLookFor =
                                variantToLookFor.VcfReferenceAllele.Length - variantToLookFor.VcfAlternateAllele.Length;

                            var numDeletedBasesFound = variantFound.VcfReferenceAllele.Length;

                            if (variantFound.HasNoData)
                                foundVariantThatFailedFilters = true;
                            else if (numDeletedBasesToLookFor == numDeletedBasesFound)
                                foundThisVariant = true;

                            break;
                        }

                            //if its a phased (or regular) snp, check the bases match.
                        case SomaticVariantType.SNP:
                        case SomaticVariantType.PhasedSNP:
                        {
                            var indexIntoFoundVariant = variantToLookFor.VcfReferencePosition -
                                                        variantFound.VcfReferencePosition;

                            //variant runs off read.
                            if (
                                indexIntoFoundVariant + variantToLookFor.VcfAlternateAllele.Length >
                                variantFound.VcfAlternateAllele.Length)
                                break;

                            var sectionFoundInRead =
                                variantFound.VcfAlternateAllele.Substring(indexIntoFoundVariant,
                                    variantToLookFor.VcfAlternateAllele.Length);

                            if (sectionFoundInRead == variantToLookFor.VcfAlternateAllele)
                                foundThisVariant = true;
                            else if (sectionFoundInRead == "N")
                                foundVariantThatFailedFilters = true;

                            break;
                        }
                    }
                }

                if (foundVariantThatFailedFilters)
                {
                    interrogationResults[i] = SetEmptyMatch(variantToLookFor);
                }
                else if (foundThisVariant)
                {
                    interrogationResults[i] = SetVariantMatch(variantToLookFor);
                }
                else
                {
                    interrogationResults[i] = SetReferenceMatch(variantToLookFor);
                }

            }


            return numSitesFoundInRead >= _bamFilterParams.MinNumberVariantsInRead ? interrogationResults : null;
        }


        private static VariantSite SetEmptyMatch(VariantSite vcfVariant)
        {
            var variantMatch = vcfVariant.DeepCopy();
            variantMatch.VcfReferenceAllele = "N";
            variantMatch.VcfAlternateAllele = "N";
            return variantMatch;
        }

        private static VariantSite SetReferenceMatch(VariantSite vcfVariant)
        {
            var variantMatch = vcfVariant.DeepCopy();
            variantMatch.VcfReferenceAllele = vcfVariant.VcfReferenceAllele.Substring(0, 1);
            variantMatch.VcfAlternateAllele = vcfVariant.VcfReferenceAllele.Substring(0, 1);
            return variantMatch;
        }

        private static VariantSite SetVariantMatch(VariantSite vcfVariant)
        {
            var variantMatch = vcfVariant.DeepCopy();
            variantMatch.VcfReferenceAllele = vcfVariant.VcfReferenceAllele;
            variantMatch.VcfAlternateAllele = vcfVariant.VcfAlternateAllele;
            return variantMatch;
        }

        private static string GetReferenceSection(int opLength)
        {
            var sb = new StringBuilder();

            for (var i = 0; i < opLength; i++)
            {
                sb.Append("R");
            }

            return sb.ToString();
        }

        public Dictionary<SomaticVariantType, List<VariantSite>> SetCandidateVariantsFoundInRead(int minBaseCallQScore, 
            BamAlignment alignment, out int lastPosInAlignment)
        {

            //var cycleMap = new int[alignment.Bases.Length];
            var overallCycleIndex = 0;
            var referencePosition = alignment.Position;//first unclipped base.
            var variantsInFoundAlignment  = new Dictionary<SomaticVariantType, List<VariantSite>>
            {
                {SomaticVariantType.Deletion, new List<VariantSite>()},
                {SomaticVariantType.SNP, new List<VariantSite>()},
                {SomaticVariantType.Insertion, new List<VariantSite>()}
            };


            for (var cigarOpIndex = 0; cigarOpIndex < alignment.CigarData.Count; cigarOpIndex++)
            {
                var vs = new VariantSite(referencePosition + 1); //shift to vcf coordinates
                var operation = alignment.CigarData[cigarOpIndex];

                switch (operation.Type)
                {
                    case 'S': // soft-clip
                        for (var cycleIndex = 0; cycleIndex < operation.Length; cycleIndex++)
                        {
                            overallCycleIndex++;
                        }
                        break;

                    case 'M': // match or mismatch
                        var operationLength = (int)operation.Length;
                        var rawAllele = alignment.Bases.Substring(overallCycleIndex, operationLength).ToCharArray();
                        var rawQ = new byte[operationLength];
                        Array.Copy(alignment.Qualities, overallCycleIndex, rawQ, 0, operationLength);

                        var sb = new StringBuilder();
                        for (int i = 0; i < operationLength; i++)
                        {
                            if (rawQ[i] < minBaseCallQScore)
                                rawAllele[i] = 'N';

                            sb.Append(rawAllele[i]);
                            vs.VcfReferenceAllele += "R";
                        }

                        vs.VcfAlternateAllele = sb.ToString();
                        variantsInFoundAlignment[SomaticVariantType.SNP].Add(vs);
                        for (var cycleIndex = 0; cycleIndex < operation.Length; cycleIndex++)
                        {
                            overallCycleIndex++;
                            referencePosition++;
                        }

                        break;

                    case 'I': // insertion
                        var insertionQualOk = alignment.Qualities[overallCycleIndex] >= minBaseCallQScore;
                        vs.VcfReferenceAllele = "";
                        vs.VcfAlternateAllele = alignment.Bases.Substring(overallCycleIndex, (int)operation.Length);

                        if (!insertionQualOk)
                        {
                            vs.VcfReferenceAllele = "N";
                            vs.VcfAlternateAllele = "N";
                        }

                        vs.VcfReferencePosition = vs.VcfReferencePosition - 1; //b/c indels are listed in vcf on preceeding base
                        variantsInFoundAlignment[SomaticVariantType.Insertion].Add(vs);

                        for (var cycleIndex = 0; cycleIndex < operation.Length; cycleIndex++)
                        {
                            overallCycleIndex++;
                        }

                        break;

                    case 'D': // deletion
                        var deletionQualOk = false;
                        var qualityOfBaseAfterDel = overallCycleIndex < alignment.Qualities.Length ? alignment.Qualities[overallCycleIndex] : alignment.Qualities[overallCycleIndex - 1];
                        var qualityOfBaseBeforeDel = qualityOfBaseAfterDel;
                        if (overallCycleIndex > 0) //if we do have data before the indel, grab it.
                            qualityOfBaseBeforeDel = alignment.Qualities[overallCycleIndex - 1];

                        if ((qualityOfBaseBeforeDel >= minBaseCallQScore) &&
                            (qualityOfBaseAfterDel >= minBaseCallQScore))
                        {
                            deletionQualOk = true;
                        }

                        vs.VcfAlternateAllele = "";
                        vs.VcfReferenceAllele = GetReferenceSection((int)operation.Length); //we dont know the reference, but we can pad it tothe right length for now.

                        if (!deletionQualOk)
                        {
                            vs.VcfReferenceAllele = "N";
                            vs.VcfAlternateAllele = "N";
                        }
                          
                        vs.VcfReferencePosition = vs.VcfReferencePosition - 1; //b/c indels are listed in vcf on preceeding base                          
                        variantsInFoundAlignment[SomaticVariantType.Deletion].Add(vs);

                        referencePosition += (int)operation.Length;
                        break;

                }
            }

            lastPosInAlignment = referencePosition+1;
            return variantsInFoundAlignment;
        }


    }
}

