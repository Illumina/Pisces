using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.IO;
using VariantPhasing.Interfaces;

namespace VariantPhasing.Logic
{
    public class VcfMerger
    {
        private readonly IAlleleSource _variantSource;
      
        public VcfMerger(IAlleleSource originalVariantSource)
        {
            _variantSource = originalVariantSource;
        }

        private List<Tuple<CalledAllele, string>> GetNextBlockOfOriginalAlleleTuplesFromVcfVar()
        {
            var allelesUnpackedFromVcfVariant = new List<CalledAllele>();
            var originalVCFLine = "";
            var alleleTuplesFromVcfVariantList = new List<Tuple<CalledAllele, string>>();

            bool worked = _variantSource.GetNextVariants(out allelesUnpackedFromVcfVariant, out originalVCFLine);
            
            if (!worked)
                return new List<Tuple<CalledAllele, string>>();

            //if the input ploidy is diploid, and the output ploidy is somatic
            //we will have a 1 -> 2 mapping issue... in that case, we have to forbidUsingoriginalVCFLine
            bool forbidUsingOriginalVCFLine = false;
            foreach (var allele in allelesUnpackedFromVcfVariant)
            {
                if (allele.Genotype == Pisces.Domain.Types.Genotype.HeterozygousAlt1Alt2)
                    forbidUsingOriginalVCFLine = true;

                if (forbidUsingOriginalVCFLine)
                    alleleTuplesFromVcfVariantList.Add(new Tuple<CalledAllele, string>(allele, ""));
                else
                {
                    alleleTuplesFromVcfVariantList.Add(new Tuple<CalledAllele, string>(allele,  originalVCFLine));
                }
            }

            return alleleTuplesFromVcfVariantList;
        }

        public void WriteRemainingVariants(IVcfFileWriter<CalledAllele> writer, List<Tuple<CalledAllele, string>> alleleTuplesPastNbhd)
        {
            WriteDistinctVcfLines(writer, alleleTuplesPastNbhd);

            while (true)
            {
                var alleleTuplesOriginalFromVcfVarList = GetNextBlockOfOriginalAlleleTuplesFromVcfVar();

                if (!alleleTuplesOriginalFromVcfVarList.Any())
                    return;
                
                WriteDistinctVcfLines(writer, alleleTuplesOriginalFromVcfVarList);
            }
        }

        public List<Tuple<CalledAllele, string>> WriteVariantsUptoChr(
            IVcfFileWriter<CalledAllele> writer, List<Tuple<CalledAllele, string>> alleleTuplesLeftoverList, string stopChr)
        {
            WriteDistinctVcfLines(writer, alleleTuplesLeftoverList);

            while (true)
            {
                var alleleTuplesInProcessList = GetNextBlockOfOriginalAlleleTuplesFromVcfVar();

                if (alleleTuplesInProcessList.Count == 0)
                    return new List<Tuple<CalledAllele, string>>();

                if (alleleTuplesInProcessList[0].Item1.Chromosome != stopChr)
                {
                    //still not in the right nbhd
                    WriteDistinctVcfLines(writer, alleleTuplesInProcessList);
                }
                else
                {
                    return alleleTuplesInProcessList;
                }
            }
        }
        
        public List<Tuple<CalledAllele, string>> WriteVariantsUptoIncludingNbhd(
            IVcfFileWriter<CalledAllele> writer, List<Tuple<CalledAllele, string>> alleleTuplesLeftoverList, ICallableNeighborhood nbhdWithMNVs)
        {
            var alleleTuplesInProcessList = new List<Tuple<CalledAllele, string>>();
            var alleleTuplesReadyToWriteList = new List<Tuple<CalledAllele, string>>();
            var alleleTuplesInCurrentVcfNbhdList = new List<Tuple<CalledAllele, string>>();
            var alleleTuplesToGroupWithNextNbhdList = new List<Tuple<CalledAllele, string>>();
            
            int orderWithRespectToNbhd = -1;
            bool quittingNbhd = false;
            bool takenCareOfLeftOvers = !alleleTuplesLeftoverList.Any();

            while (true)
            {
                if (quittingNbhd)
                    break;

                if (takenCareOfLeftOvers)
                    alleleTuplesInProcessList = GetNextBlockOfOriginalAlleleTuplesFromVcfVar();
                else
                {
                    alleleTuplesInProcessList = GetNextSetOfAlleleTuplesToProcess(
                        writer, alleleTuplesLeftoverList, nbhdWithMNVs.ReferenceName);
                    takenCareOfLeftOvers = true;
                }
            
                if (!alleleTuplesInProcessList.Any())
                    break;

                foreach (var originalAlleleTuple in alleleTuplesInProcessList)
                {
                    if (quittingNbhd)
                    {
                        alleleTuplesToGroupWithNextNbhdList.Add(originalAlleleTuple);
                        continue;
                    }

                    orderWithRespectToNbhd = OrderWithNeighborhood(nbhdWithMNVs, originalAlleleTuple.Item1);
                    switch (orderWithRespectToNbhd)
                    {
                        case (-1)://if we are before the nbhd, write straight to vcf
                            alleleTuplesReadyToWriteList.Add(originalAlleleTuple);
                            break;

                        case (0): //in the nbhd
                            alleleTuplesInCurrentVcfNbhdList.Add(originalAlleleTuple);
                            break;

                        default:
                            //if we are either ahead of our nbhd, or gone into the wrong chr)
                            //Close out the nbhd and finish up.
                            var mergedVariants = GetMergedListOfVariants(nbhdWithMNVs, alleleTuplesInCurrentVcfNbhdList);
                            alleleTuplesReadyToWriteList.AddRange(mergedVariants);
                            alleleTuplesToGroupWithNextNbhdList.Add(originalAlleleTuple);
                            quittingNbhd = true;
                            break;
                    }
                }
            }

            //close out any remaining nbhd
            if (orderWithRespectToNbhd == 0)
            {
                var mergedVariants = GetMergedListOfVariants(nbhdWithMNVs, alleleTuplesInCurrentVcfNbhdList);
                alleleTuplesReadyToWriteList.AddRange(mergedVariants);
            }

            var alleleTuplesAfterAdjustmentList = VcfMergerUtils.AdjustForcedAllele(alleleTuplesReadyToWriteList);

            WriteDistinctVcfLines(writer, alleleTuplesAfterAdjustmentList);

            return alleleTuplesToGroupWithNextNbhdList;
        }

        private List<Tuple<CalledAllele, string>> GetNextSetOfAlleleTuplesToProcess(
            IVcfFileWriter<CalledAllele> writer, List<Tuple<CalledAllele, string>> alleleTuplesLeftoverList, string thisChr)
        {
            var alleleTuplesOriginalList = new List<Tuple<CalledAllele, string>>();

            //if alleles are left over from the last nbhd, do them first.
            if (alleleTuplesLeftoverList.Count > 0)
            {
                if (alleleTuplesLeftoverList[0].Item1.Chromosome != thisChr)
                {
                    //we have already gone into the wrong chr. This cant possibly be in our nbhd.
                    //write straight to vcf.
                    WriteDistinctVcfLines(writer, alleleTuplesLeftoverList);

                    return GetNextSetOfAlleleTuplesToProcess(writer, new List<Tuple<CalledAllele, string>>(), thisChr);
                }
                else //they might be in our nbhd, so send them through the process
                {
                    alleleTuplesOriginalList.AddRange(alleleTuplesLeftoverList);
                }
                return alleleTuplesOriginalList;
            }
            else
            {
                alleleTuplesOriginalList = GetNextBlockOfOriginalAlleleTuplesFromVcfVar();
                return alleleTuplesOriginalList;
            }
        }
        
        private static int OrderWithNeighborhood(ICallableNeighborhood nbhdWithMNVs, CalledAllele originalVariant)
        {
            if (originalVariant.Chromosome != nbhdWithMNVs.ReferenceName)
                return 1;

            if (originalVariant.ReferencePosition > nbhdWithMNVs.LastPositionOfInterestInVcf)
                return 1;

            if ((originalVariant.ReferencePosition <= nbhdWithMNVs.LastPositionOfInterestInVcf) && (originalVariant.ReferencePosition >= nbhdWithMNVs.FirstPositionOfInterest))
                return 0;

            return -1;
        }

        public static List<Tuple<CalledAllele, string>> GetMergedListOfVariants(
            ICallableNeighborhood completedNbhd, List<Tuple<CalledAllele, string>> originalVariantsInsideRange)
        {
            var mergedVariantList = new List<Tuple<CalledAllele, string>>();
            Dictionary<int, List<CalledAllele>> foundMNVS = completedNbhd.CalledVariants;

            //track which variants got used for MNV phasing.
            var indexesOfVariantsRecalledByMnvCaller = completedNbhd.GetOriginalVcfVariants();

            //decide which of the original variants we keep, and which get replaced.
            for (int i = 0; i < originalVariantsInsideRange.Count; i++)
            {
                var originalCall = originalVariantsInsideRange[i];
                var currentPosition = originalCall.Item1.ReferencePosition;
                bool variantWasAlreadyUsed = CheckIfUsed(indexesOfVariantsRecalledByMnvCaller, originalCall.Item1);
                
                if (foundMNVS.ContainsKey(currentPosition))
                {
                    //add all the MNVs
                    foreach (var mnv in foundMNVS[currentPosition])
                    {
                        if (mnv.IsSameAllele(originalCall.Item1) && 
                            mnv.AlleleSupport == originalCall.Item1.AlleleSupport && 
                            mnv.TotalCoverage == originalCall.Item1.TotalCoverage &&
                            mnv.ReferenceSupport == originalCall.Item1.ReferenceSupport)
                        {
                            mergedVariantList.Add(originalCall);
                        }
                        else
                        {
                            mergedVariantList.Add(new Tuple<CalledAllele, string>(mnv, ""));
                        }
                    }
                    foundMNVS[currentPosition] = new List<CalledAllele>();//empty out the list, but leave the fact that there were MNVs here.

                    //add back any original variants, 
                    //so long as they were not used by the caller, and not reference
                    if (!(variantWasAlreadyUsed) &&
                        (originalCall.Item1.Type != Pisces.Domain.Types.AlleleCategory.Reference))
                         mergedVariantList.Add(originalCall);

                    continue;
                }

                //Else, we did not find any MNVs here.
                //Then this position should be either the original call from the vcf,
                //or a new reference call converted from a variant that got used by the MNV caller.                
                if (variantWasAlreadyUsed)
                {
                    var newRef = completedNbhd.CalledRefs[originalCall.Item1.ReferencePosition];

                    //sometimes several variants were used, all at the same position. we dont need to add new references for all of them.
                    if ((mergedVariantList.Count == 0) ||
                        (mergedVariantList.Last().Item1.ReferencePosition != currentPosition))
                    {
                        mergedVariantList.Add(new Tuple<CalledAllele, string>(newRef, ""));
                    }
                }
                else //wasnt used for MNV calling
                {
                    mergedVariantList.Add(originalCall);
                }
            }

            //in case we called any MNVs past the edge of the input VCF.
            foreach (var mnvsLeft in foundMNVS)
            {
                foreach (var mnv in mnvsLeft.Value)
                {
                    mergedVariantList.Add(new Tuple<CalledAllele, string>(mnv, ""));
                }
            }

            mergedVariantList.Sort(new AlleleTupleCompareByLociAndAllele());

            return mergedVariantList;

        }
        
        private static bool CheckIfUsed(List<CalledAllele> usedAlleles, CalledAllele originalCall)
        {
            foreach (var allele in usedAlleles)
            {
                if (originalCall.IsSameAllele(allele))
                    return true;
            }

            return false;
        }

        private void WriteDistinctVcfLines(IVcfFileWriter<CalledAllele> writer, List<Tuple<CalledAllele, string>> alleleTuples)
        {
            var distinctVcfLines = new HashSet<string>();

            foreach (var alleleTuple in alleleTuples)
            {
                if (alleleTuple.Item2 == "")
                {
                    writer.Write(new List<CalledAllele>{alleleTuple.Item1});
                }
                else
                {
                    if (!distinctVcfLines.Contains(alleleTuple.Item2))
                    {
                        distinctVcfLines.Add(alleleTuple.Item2);
                        writer.Write(alleleTuple.Item2);
                    }
                }
            }

        }

    }

}
