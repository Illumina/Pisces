using System.Collections.Generic;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;

namespace Pisces.IO
{

    public enum TypeOfUpdateNeeded { NoChangeNeeded, DeleteCompletely, Modify };

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T">type of data to decide how to do the operation (like filter thresholds or priors)</typeparam>
    public class VcfUpdater<T>
    {
        public delegate TypeOfUpdateNeeded UpdateCoLocatedAllelesMethod(VcfConsumerAppOptions options, T data, List<CalledAllele> incomingAlleles, out List<CalledAllele> outGoingAlleles);
        public delegate TypeOfUpdateNeeded UpdateSingleAlleleMethod(VcfConsumerAppOptions options, T data, CalledAllele incomingAllele, out List<CalledAllele> outGoingAlleles);
        public delegate TypeOfUpdateNeeded CanSkipVcfLinesMethod(List<string> vcfLine);
        public delegate VcfFileWriter GetVcfFileWriter(VcfConsumerAppOptions options, string outputFilePath);

        public static void UpdateVcfAlleleByAllele(string vcfOut, VcfConsumerAppOptions options, bool shouldTrimComplexAlleles, T recalibrationData,
          UpdateSingleAlleleMethod whatToDoWithAllele, CanSkipVcfLinesMethod canSkipLineWithoutProcessing, GetVcfFileWriter getVcfFileWriter)
        {
            UpdateVcf(vcfOut, options, shouldTrimComplexAlleles, recalibrationData,
                whatToDoWithAllele, NeverUpdateByLoci, canSkipLineWithoutProcessing,
                getVcfFileWriter);

        }

        public static void UpdateVcfLociByLoci(string vcfOut, VcfConsumerAppOptions options, bool shouldTrimComplexAlleles, 
            T recalibrationData, UpdateCoLocatedAllelesMethod whatToDoWithCoLocatedAlleles, CanSkipVcfLinesMethod canSkipLineWithoutProcessing, 
            GetVcfFileWriter getVcfFileWriter)
        {
            UpdateVcf(vcfOut, options, shouldTrimComplexAlleles, recalibrationData,
                NeverUpdateByAlleleOnly, whatToDoWithCoLocatedAlleles, canSkipLineWithoutProcessing,
                getVcfFileWriter);

        }

        /// <summary>
        /// Take in a vcf, do stuff to it, write out a vcf. Streamed line by line, loci by loci, so as not to blow up your computer. 
        /// </summary>
        /// <param name="vcfOut"> the output file name</param>
        /// <param name="options"> all the parameters associated with writing out a vcf</param>
        /// <param name="recalibrationData">the data you need for doing your "stuff" </param>
        /// <param name="whatToDoWithSingleAllele">how you want to change each allele</param>
        /// <param name="whatToDoWithCoLocatedAlleles">how you want to change each set of alleles, by loci</param>
        /// <param name="canSkipLinesWithoutProcessing">when you can skip lines (saves CPU time)</param>
        /// <param name="getVcfFileWriter">what your special vcf writer should be, includes special header lines, etc</param>
        /// <param name="shouldTrimComplexAlleles">if ACGT-> ACCT is ok, or if you want it trimmed to G -> C. this might affect position and ordering. Generally turn if OFF for processing vcfs, post scylla. </param>
        private static void UpdateVcf(string vcfOut, VcfConsumerAppOptions options, bool shouldTrimComplexAlleles, T recalibrationData,
            UpdateSingleAlleleMethod whatToDoWithSingleAllele, UpdateCoLocatedAllelesMethod whatToDoWithCoLocatedAlleles,
            CanSkipVcfLinesMethod canSkipLinesWithoutProcessing, GetVcfFileWriter getVcfFileWriter)
        {
            using (AlleleReader reader = new AlleleReader(options.VcfPath, shouldTrimComplexAlleles))
            {

                using (VcfFileWriter writer = getVcfFileWriter(options, vcfOut))
                {

                    writer.WriteHeader();
                    writer.FlushBuffer();

                    var variantListFromFile = new List<CalledAllele>() { };

                    string incomingHangingLine = null;
                    string outgoingHangingLine = null;

                    while (true)
                    {
                        //get the next group to process
                        incomingHangingLine = outgoingHangingLine;
                        var coLocatedVcfLinesToProcess = reader.CloseColocatedLines(incomingHangingLine,
                            out outgoingHangingLine);

                        //how we know we are done
                        if (coLocatedVcfLinesToProcess.Count == 0)
                            break;

                        bool updateNeededForLocus = false;
                        TypeOfUpdateNeeded updatedNeededForLine = canSkipLinesWithoutProcessing(coLocatedVcfLinesToProcess);

                        switch (updatedNeededForLine)
                        {
                            case TypeOfUpdateNeeded.NoChangeNeeded:
                                writer.Write(coLocatedVcfLinesToProcess);
                                break;

                            case TypeOfUpdateNeeded.Modify:
                                //then we need to change them into alleles and do stuff to them
                                variantListFromFile = AlleleReader.VcfLinesToAlleles(coLocatedVcfLinesToProcess);
                                List<CalledAllele> modifiedVariantListToWrite = WhatToDoToAlleles(options, recalibrationData,
                                    whatToDoWithSingleAllele, whatToDoWithCoLocatedAlleles, variantListFromFile, ref updateNeededForLocus);

                                if (updateNeededForLocus)
                                    writer.Write(modifiedVariantListToWrite);
                                else
                                    writer.Write(coLocatedVcfLinesToProcess);
                                break;

                            case TypeOfUpdateNeeded.DeleteCompletely:
                            default:
                                break;
                        }

                    }


                }
            }
        }

        private static List<CalledAllele> WhatToDoToAlleles(VcfConsumerAppOptions options, T recalibrationData, UpdateSingleAlleleMethod whatToDoWithAllele,
           UpdateCoLocatedAllelesMethod whatToDoWithCoLocatedAlleles, List<CalledAllele> variantListFromFile, ref bool updateNeeded)
        {
            //do any loci-wide actions
            var modifiedAsAGroup = new List<CalledAllele>() { };
            TypeOfUpdateNeeded updateneededForLoci = whatToDoWithCoLocatedAlleles(options, recalibrationData, variantListFromFile, out modifiedAsAGroup);
            updateNeeded = (updateneededForLoci != TypeOfUpdateNeeded.NoChangeNeeded);

            //do any per-individual allele actions on the new list
            var modifiedAsIndividualAlleles = new List<CalledAllele>() { };
            foreach (var calledAllele in modifiedAsAGroup)
            {
                var convertedVariants = new List<CalledAllele>() { };
                TypeOfUpdateNeeded updateneededForAllele = whatToDoWithAllele(options, recalibrationData, calledAllele, out convertedVariants);
                updateNeeded = updateNeeded || (updateneededForAllele != TypeOfUpdateNeeded.NoChangeNeeded);

                if (updateneededForAllele != TypeOfUpdateNeeded.DeleteCompletely)
                    modifiedAsIndividualAlleles.AddRange(convertedVariants);
            }

            return modifiedAsIndividualAlleles;
        }


        public static TypeOfUpdateNeeded NeverUpdateByAlleleOnly(VcfConsumerAppOptions appOptions, T newData, CalledAllele inAllele, out List<CalledAllele> outAlleles)
        {
            outAlleles = new List<CalledAllele>() { inAllele };
            return TypeOfUpdateNeeded.NoChangeNeeded;
        }

        public static TypeOfUpdateNeeded NeverUpdateByLoci(VcfConsumerAppOptions appOptions, T newData, List<CalledAllele> inAlleles, out List<CalledAllele> outAlleles)
        {
            outAlleles = inAlleles;
            return TypeOfUpdateNeeded.NoChangeNeeded;
        }

    }
}
