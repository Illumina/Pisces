using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models.Alleles;
using Pisces.IO;
using VariantPhasing.Interfaces;

namespace VariantPhasing.Logic
{
    public class VcfMerger
    {
        private readonly IVcfVariantSource _variantSource;
      
        public VcfMerger(IVcfVariantSource originalVariantSource)
        {
            _variantSource = originalVariantSource;
        }


        public void WriteRemainingVariants(IVcfFileWriter<CalledAllele> writer, List<CalledAllele> allelesPastNbhd)
        {
            writer.Write(allelesPastNbhd);

            while (true)
            {
                var alleles = GetNextBlockOfOriginalAllelesFromVcfVar();

                if (alleles.Count() == 0)
                    return;

                writer.Write(alleles);

            }
        }

        private IEnumerable<CalledAllele> GetNextBlockOfOriginalAllelesFromVcfVar()
        {
            var vcfVar = new VcfVariant();
            bool worked = _variantSource.GetNextVariant(vcfVar);

            if (!worked)
                return new List<CalledAllele>();
          
            return (VcfVariantUtilities.Convert(new List<VcfVariant> { vcfVar }));
        }


        public List<CalledAllele> WriteVariantsUptoIncludingNbhd(
            IVcfNeighborhood nbhdWithMNVs,
            IVcfFileWriter<CalledAllele> writer, List<CalledAllele> leftoverAlleles)
        {
            var calledMNVs = nbhdWithMNVs.CalledVariants;
            var allelesReadyToWrite = new List<CalledAllele>();
            var allelesInCurrentVcfNbhd = new List<CalledAllele>();
            var allelesToGoupWithNextNbhd = new List<CalledAllele>();

            var allelesInProcess = new List<CalledAllele>();

            int orderWithRespectToNbhd = -1;
            bool quittingNbhd = false;
            bool takenCareOfLeftOvers = (leftoverAlleles.Count() == 0);

            while (true)
            {

                if (quittingNbhd)
                    break;

                if (takenCareOfLeftOvers)
                    allelesInProcess = GetNextBlockOfOriginalAllelesFromVcfVar().ToList();
                else
                {
                    allelesInProcess = GetNextSetOfAllelesToProcess(leftoverAlleles, nbhdWithMNVs.ReferenceName, writer);
                    takenCareOfLeftOvers = true;
                }

            
                if (allelesInProcess.Count() == 0)
                    break;

                foreach (var originalAllele in allelesInProcess)
                {

                    if (quittingNbhd)
                    {
                        allelesReadyToWrite.Add(originalAllele);
                        continue;
                    }

                    orderWithRespectToNbhd = OrderWithNeighborhood(nbhdWithMNVs, originalAllele);
                    switch (orderWithRespectToNbhd)
                    {
                        case (-1)://if we are before the nbhd, write straight to vcf
                            allelesReadyToWrite.Add(originalAllele);
                            break;

                        case (0): //in the nbhd
                            allelesInCurrentVcfNbhd.Add(originalAllele);
                            break;

                        default:
                            //if we are either ahead of our nbhd, or gone into the wrong chr)
                            //Close out the nbhd and finish up.                           
                            var mergedVariants = GetMergedListOfVariants(nbhdWithMNVs, allelesInCurrentVcfNbhd);
                            allelesReadyToWrite.AddRange(mergedVariants);
                            allelesToGoupWithNextNbhd.Add(originalAllele);
                            quittingNbhd = true;
                            break;

                    }
                }
            }

            //close out any remaining nbhd
            if (orderWithRespectToNbhd == 0)
            {
                var mergedVariants = GetMergedListOfVariants(nbhdWithMNVs, allelesInCurrentVcfNbhd);
                allelesReadyToWrite.AddRange(mergedVariants);

            }

            var alleleAfterAdjustment = VcfMergerUtils.AdjustForcedAllele(allelesReadyToWrite);
            writer.Write(alleleAfterAdjustment);
            return allelesToGoupWithNextNbhd;
        }



        private List<CalledAllele> GetNextSetOfAllelesToProcess( List<CalledAllele> leftoverAlleles,
            string thisChr, IVcfFileWriter<CalledAllele> writer)
        {
            var originalAlleles = new List<CalledAllele>();

            //if alleles are left over from the last nbhd, do them first.
            if (leftoverAlleles.Count > 0)
            {

                if (leftoverAlleles[0].Chromosome != thisChr)
                {
                    //we have already gone into the wrong chr. This cant possibly be in our nbhd.
                    //write straight to vcf.       
                    writer.Write(leftoverAlleles);

                    return GetNextSetOfAllelesToProcess(new List<CalledAllele>(), thisChr, writer);
                }
                else //they might be in our nbhd, so send them through the process
                {
                    originalAlleles.AddRange(leftoverAlleles);
                }
                leftoverAlleles = new List<CalledAllele>();
                return originalAlleles;
            }
            else
            {
                originalAlleles = GetNextBlockOfOriginalAllelesFromVcfVar().ToList();
                return originalAlleles;
            }
        }
        

        private static int OrderWithNeighborhood(IVcfNeighborhood nbhdWithMNVs, CalledAllele originalVariant)
        {
            if (originalVariant.Chromosome != nbhdWithMNVs.ReferenceName)
                return 1;

            if (originalVariant.ReferencePosition > nbhdWithMNVs.LastPositionOfInterestInVcf)
                return 1;

            if ((originalVariant.ReferencePosition <= nbhdWithMNVs.LastPositionOfInterestInVcf) && (originalVariant.ReferencePosition >= nbhdWithMNVs.FirstPositionOfInterest))
                return 0;

            return -1;
        }

        public static List<CalledAllele> GetMergedListOfVariants(IVcfNeighborhood completedNbhd, List<CalledAllele> originalVariantsInsideRange)
        {
            var mergedVariantList = new List<CalledAllele>();
            Dictionary<int, List<CalledAllele>> foundMNVS = completedNbhd.CalledVariants;

            //track which variants got used for MNV phasing.
            var indexesOfVariantsRecalledByMnvCaller = completedNbhd.GetOriginalVcfVariants();

            //decide which of the original variants we keep, and which get replaced.
            for (int i = 0; i < originalVariantsInsideRange.Count; i++)
            {
                var originalCall = originalVariantsInsideRange[i];
                var currentPosition = originalCall.ReferencePosition;
                bool variantWasAlreadyUsed = CheckIfUsed(indexesOfVariantsRecalledByMnvCaller, originalCall);


                if (foundMNVS.ContainsKey(currentPosition))
                {
                    //add all the MNVs
                    mergedVariantList.AddRange(foundMNVS[currentPosition]);
                    foundMNVS[currentPosition] = new List<CalledAllele>();//empty out the list, but leave the fact that there were MNVs here.

                    //add back any original variants, 
                    //so long as they were not used by the caller, and not reference
                    if (!(variantWasAlreadyUsed) &&
                        (originalCall.Type != Pisces.Domain.Types.AlleleCategory.Reference))
                        mergedVariantList.Add(originalCall);

                    continue;
                }

                //Else, we did not find any MNVs here.
                //Then this position should be either the original call from the vcf,
                //or a new reference call converted from a variant that got used by the MNV caller.                
                if (variantWasAlreadyUsed)
                {
                    var newRef = completedNbhd.CalledRefs[originalCall.ReferencePosition];

                    //sometimes several variants were used, all at the same position. we dont need to add new references for all of them.
                    if ((mergedVariantList.Count == 0) || (mergedVariantList.Last().ReferencePosition != currentPosition))
                        mergedVariantList.Add(newRef);

                }
                else //wasnt used for MNV calling
                {
                    mergedVariantList.Add(originalVariantsInsideRange[i]);
                }
            }

            //incase we called any MNVs past the edge of the input VCF.
            foreach (var mnvsLeft in foundMNVS)
                mergedVariantList.AddRange(mnvsLeft.Value);

            var comparer = new AlleleCompareByLoci();
            mergedVariantList.Sort(comparer);


            return mergedVariantList;

        }

        public List<CalledAllele> WriteVariantsUptoChr(IVcfFileWriter<CalledAllele> writer, List<CalledAllele> leftoverAlleles, string stopChr)
        {
            writer.Write(leftoverAlleles);

            while (true)
            {

                var allelesInProcess = GetNextBlockOfOriginalAllelesFromVcfVar().ToList();

                if (allelesInProcess == null || allelesInProcess.Count == 0)
                    return new List<CalledAllele>();

                if (allelesInProcess[0].Chromosome != stopChr)
                {
                    //still not in the right nbhd
                    writer.Write(allelesInProcess);
                }
                else return allelesInProcess;
            }
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
    }

}
