using System;
using System.Collections.Generic;
using System.Text;
using Pisces.Domain.Options;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using VariantPhasing.Models;
using VariantPhasing.Types;

namespace VariantPhasing.Logic
{
    public class VeadFinder
    {

        //For each potential phasing candiate (vs) in a Neighborhood, the read either has that variant, or has something else.
        public enum StateOfPhasingSiteInRead { FoundThisVariant, HaveInsufficientData, FoundDifferentVariant, FoundReferenceVariant, IDontKnow };

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
        }

        public bool HaveWeSeenEvidenceForAReferenceCall(VariantSite variantWeCantFind,
            Dictionary<SubsequenceType,
            List<VariantSite>> variantsInFoundAlignment, int firstPosInAlignment, decimal lastPosInAlignment)
        {
            int variantSitePosition = variantWeCantFind.VcfReferencePosition;
            string refBase = variantWeCantFind.VcfReferenceAllele.Substring(0, 1);

            VariantSite testReferenceCall = new VariantSite(variantSitePosition) { VcfReferenceAllele = refBase, VcfAlternateAllele = refBase };

            foreach (var variantSite in variantsInFoundAlignment[SubsequenceType.MatchOrMismatchSequence])
            {
                var result = CheckVariantSequenceForMatchInVariantSiteFromRead(testReferenceCall, variantSite);

                //GB: Confused by the name of this if it returns true for "FoundThisVariant" and "FoundReferenceVariant"?
                //TJD: I'm recycling the "CheckSnpForMatch" method.  We always want to use the same snp-huning method. 
                //In this case particular case, I am FEEDING it a reference variant to go hunting for
                //as the variant to match in the read. SO if it finds a reference OR the variant its looking for, we have proven support for a reference exists. 
                //You might rightfully wonder how we can end up in this state.
                //There exists the rare case where we called an indel that failed basecall filters BUT the read
                //still has good evidence for a reference call at that base, and we can use that. 
                //(and if we dont use it, it looks like a weird coverage drop-out that was not in the original vcf)
                //This comes up in the cases of certain error prone situations where the base call quality
                //jumps all over the place and an insertion might fail from lack of coverage
                //but a right-next-to-it reference might be a high pass.
                

                if (result==StateOfPhasingSiteInRead.FoundThisVariant || result == StateOfPhasingSiteInRead.FoundReferenceVariant)
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// Here we take a list of variants we found in the the input vcf, that we are trying to phase, and query a given alignment,
        /// to see if we find support for any of those varaints. 
        /// By this point, We have already pre-processed the the read into a dictionary of 3 types (insertion, deletion, and MNVorSNP),
        /// so its easy to for each varaint-to-look-for, to pull up the correct list of insertions, deletions, or MNVorSNP,
        /// and then just process that list, looking for matches.
        /// </summary>
        /// <param name="variantsFromVcf">The variants we found in the the input vcf, that we are trying to phase</param>
        /// <param name="allelesInFoundAlignment">The alleles found in a given alignment. This is a mix of indels, snps and mnvs, and we only care about what we find at the "variantsfromvcf" positions</param>
        /// <param name="firstPosInAlignment"></param>
        /// <param name="lastPosInAlignment"></param>
        /// <returns></returns>
        public VariantSite[] MatchReadVariantsWithVcfVariants(List<VariantSite> variantsFromVcf, Dictionary<SubsequenceType, 
            List<VariantSite>> allelesInFoundAlignment, int firstPosInAlignment, decimal lastPosInAlignment)
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
                if (allelesInFoundAlignment[type].Count == 0)
                {
                    if (HaveWeSeenEvidenceForAReferenceCall(variantToLookFor,
                                                allelesInFoundAlignment,  firstPosInAlignment, lastPosInAlignment))
                    {
                        interrogationResults[i] = SetReferenceMatch(variantToLookFor);
                    }
                    else
                        interrogationResults[i] = SetEmptyMatch(variantToLookFor);

                    continue;
                }

                StateOfPhasingSiteInRead result = StateOfPhasingSiteInRead.IDontKnow;

                foreach (var variantFound in allelesInFoundAlignment[type])
                {
                    
                    if (result==StateOfPhasingSiteInRead.FoundThisVariant)
                        break;

                 

                    //check we have not already gone past where this variant should be found
                    if (variantToLookFor.VcfReferencePosition < variantFound.VcfReferencePosition)
                    {

                        if (HaveWeSeenEvidenceForAReferenceCall(variantToLookFor, allelesInFoundAlignment, firstPosInAlignment, lastPosInAlignment))
                        {
                            interrogationResults[i] = SetReferenceMatch(variantToLookFor);
                        }
                        else
                            interrogationResults[i] = SetEmptyMatch(variantToLookFor);

                        break;
                    }

                    switch (type)
                    {
                        //if its an insertion, check the inserted bases match.
                        case SubsequenceType.InsertionSquence:
                            {
                                if (variantFound.VcfReferencePosition != variantToLookFor.VcfReferencePosition)
                                    break;

                                var insertionSection = variantToLookFor.VcfAlternateAllele.Substring(1,
                                    variantToLookFor.VcfAlternateAllele.Length - 1);

                                if (variantFound.HasNoData)
                                    result = StateOfPhasingSiteInRead.HaveInsufficientData;
                                else if (variantFound.VcfAlternateAllele == insertionSection)
                                    result = StateOfPhasingSiteInRead.FoundThisVariant;
                                else
                                    result = StateOfPhasingSiteInRead.FoundDifferentVariant;
     
                                break;
                            }

                        //if its a deletion, check the deletion length matches
                        case SubsequenceType.DeletionSequence:
                            {
                                if (variantFound.VcfReferencePosition != variantToLookFor.VcfReferencePosition)
                                    break;

                                var numDeletedBasesToLookFor =
                                    variantToLookFor.VcfReferenceAllele.Length - variantToLookFor.VcfAlternateAllele.Length;

                                var numDeletedBasesFound = variantFound.VcfReferenceAllele.Length;

                                if (variantFound.HasNoData)
                                    result = StateOfPhasingSiteInRead.HaveInsufficientData;
                                else if (numDeletedBasesToLookFor == numDeletedBasesFound)
                                    result = StateOfPhasingSiteInRead.FoundThisVariant;
                                else
                                {
                                    result = StateOfPhasingSiteInRead.FoundDifferentVariant;
                                }
                                break;
                            }

                        //if its a phased (or regular) snp, check the bases match.
                        case SubsequenceType.MatchOrMismatchSequence:
                        //case SomaticVariantType.PhasedSNP:
                            {
                                result = CheckVariantSequenceForMatchInVariantSiteFromRead(variantToLookFor, variantFound);
                                break;
                            }
                    }
                }

                //we get here if the vcf variant we were looking for is an indel and none of the indels found matched (maybe at dif positions)
                //Now we need to find out if what _is_ there at the indel site, if it is ref or not for this read.
                // NEW CODE HERE

                if (result == StateOfPhasingSiteInRead.IDontKnow)
                {
                    if (HaveWeSeenEvidenceForAReferenceCall(variantToLookFor,
                                            allelesInFoundAlignment, firstPosInAlignment, lastPosInAlignment))
                    {
                        result = StateOfPhasingSiteInRead.FoundReferenceVariant;
                    }
                }
                  
                switch (result)
                {
                    case StateOfPhasingSiteInRead.IDontKnow:
                    case StateOfPhasingSiteInRead.HaveInsufficientData:
                        {
                            interrogationResults[i] = SetEmptyMatch(variantToLookFor);
                            break;
                        }

                    case StateOfPhasingSiteInRead.FoundThisVariant:
                        {
                            interrogationResults[i] = SetVariantMatch(variantToLookFor);
                            break;
                        }

                    case StateOfPhasingSiteInRead.FoundDifferentVariant:
                        {
                            interrogationResults[i] = SetDifferenceMatch(variantToLookFor);
                            break;
                        }

                    case StateOfPhasingSiteInRead.FoundReferenceVariant:
                        {
                            interrogationResults[i] = SetReferenceMatch(variantToLookFor); 
                            break;
                        }
                    default: 
                        { //defensive against complexity, currently impossible to reach.
                            throw new ArgumentException("A phasing state exists which has not been properly handled in the code.");
                        }

                }
         
            }


            return numSitesFoundInRead >= _bamFilterParams.MinNumberVariantsInRead ? interrogationResults : null;
        }

        /// <summary>
        /// We are looking for evidence for a variant (SNP or MNV) in the mismatch string we picked up from a read.
        /// Basically, if you say CheckVariantSequenceForMatchInVariantSiteFromRead( (MNV(X->Y) , Z ) it wants to find subsequence Y in Z
        /// //Further notes:
        /// * Mismatches other than the one being queried do not matter (looking for 5:T>C in 4: AT>TC will show as found this variant)
        /// * Ns outside of the query sequence don't matter (looking for 5:T>C in 4:AT>NC will show as found this variant)
        /// * Practically equivalent subsequence doesn't cut it because explicit assumption of the method is that they are equivalently trimmed (i.e. ATA>AGC != TA>GC)
        /// See unit tests for many, many worked examples.
        /// </summary>
        /// <param name="variantSequenceToLookFor"></param> A variant we are looking for. Must be an SNP or MNV. Indels are handled on a sep method.
        /// <param name="readSequenceToQuery"></param> A mismatch string from the read. Its assumed that ref and alt strings will match in length.
        /// <returns></returns>
        public static StateOfPhasingSiteInRead CheckVariantSequenceForMatchInVariantSiteFromRead(VariantSite variantSequenceToLookFor, VariantSite readSequenceToQuery)
        {
            var indexIntoFoundSection = variantSequenceToLookFor.VcfReferencePosition -
                                        readSequenceToQuery.VcfReferencePosition;

           
            //case: variant not in read sequence becasue the positions dont match up
            if ((
                indexIntoFoundSection + variantSequenceToLookFor.VcfAlternateAllele.Length >
                readSequenceToQuery.VcfAlternateAllele.Length) //after read
                ||
                    (indexIntoFoundSection<0)) //before read
            {
                //do nothing..(we could return an N here)
                return StateOfPhasingSiteInRead.HaveInsufficientData; //equivalent to an "N"
            }
            else //case: the positions match up, so lets check if the bases match up
            {

                var subsequenceFoundInRead =
                    readSequenceToQuery.VcfAlternateAllele.Substring(indexIntoFoundSection,
                        variantSequenceToLookFor.VcfAlternateAllele.Length);

                if (subsequenceFoundInRead == variantSequenceToLookFor.VcfAlternateAllele)
                    return StateOfPhasingSiteInRead.FoundThisVariant;

                else if (subsequenceFoundInRead.Contains("N"))
                {
                    return StateOfPhasingSiteInRead.HaveInsufficientData;
                }
                else if (subsequenceFoundInRead == variantSequenceToLookFor.VcfReferenceAllele)
                {
                    return StateOfPhasingSiteInRead.FoundReferenceVariant;
                }
                else //sectionFoundInRead neither reference nor what we are looking for 
                {
                    return StateOfPhasingSiteInRead.FoundDifferentVariant;
                }
            }
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

        private static VariantSite SetDifferenceMatch(VariantSite vcfVariant)
        {
            var variantMatch = vcfVariant.DeepCopy();
            variantMatch.VcfReferenceAllele = "X";
            variantMatch.VcfAlternateAllele = "X";
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

        public static Dictionary<SubsequenceType, List<VariantSite>> SetCandidateVariantsFoundInRead(int minBaseCallQScore, 
            BamAlignment alignment, out int lastPosInAlignment)
        {

            //var cycleMap = new int[alignment.Bases.Length];
            var overallCycleIndex = 0;
            var referencePosition = alignment.Position;//first unclipped base.
            var variantsInFoundAlignment  = new Dictionary<SubsequenceType, List<VariantSite>>
            {
                {SubsequenceType.DeletionSequence, new List<VariantSite>()},
                {SubsequenceType.MatchOrMismatchSequence, new List<VariantSite>()},
                {SubsequenceType.InsertionSquence, new List<VariantSite>()}
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
                        vs.VcfReferenceAllele = "";
                        var sb = new StringBuilder();
                        for (int i = 0; i < operationLength; i++)
                        {
                            if (rawQ[i] < minBaseCallQScore)
                                rawAllele[i] = 'N';

                            sb.Append(rawAllele[i]);
                            vs.VcfReferenceAllele += "R";
                        }

                        vs.VcfAlternateAllele = sb.ToString();
                        variantsInFoundAlignment[SubsequenceType.MatchOrMismatchSequence].Add(vs);
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
                        variantsInFoundAlignment[SubsequenceType.InsertionSquence].Add(vs);

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
                        variantsInFoundAlignment[SubsequenceType.DeletionSequence].Add(vs);

                        referencePosition += (int)operation.Length;
                        break;

                }
            }

            lastPosInAlignment = referencePosition+1;
            return variantsInFoundAlignment;
        }


    }
}

