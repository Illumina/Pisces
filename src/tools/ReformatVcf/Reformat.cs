using System;
using System.Collections.Generic;
using System.IO;
using Pisces.IO;
using Pisces.Domain.Options;
using Pisces.Domain.Models.Alleles;

namespace ReformatVcf
{
    public class Reformat
    {
        private static TypeOfUpdateNeeded UpdateAllele(VcfConsumerAppOptions appOptions, bool recalibrationData, CalledAllele inAllele, out List<CalledAllele> outAlleles)
        {
            outAlleles = new List<CalledAllele> { inAllele };
            return TypeOfUpdateNeeded.Modify;
        }

        public static TypeOfUpdateNeeded CanSkipNeverVcfLine(List<string> originalVarString)
        {
            return TypeOfUpdateNeeded.Modify;
        }

        public static VcfFileWriter GetVcfFileWriter(VcfConsumerAppOptions options, string outputFilePath)
        {
            var vcp = options.VariantCallingParams;
            var vwp = options.VcfWritingParams;
            var bfp = options.BamFilterParams;
            var vcfConfig = new VcfWriterConfig(vcp, vwp, bfp, null, false, false);

            return (new VcfFileWriter(outputFilePath, vcfConfig, new VcfWriterInputContext()));
        }

        public static void DoReformating(ReformatOptions options)
        {
            var inputFile = options.VcfPath;
            var outputFile = inputFile.Replace(".vcf", ".uncrushed.vcf");
            var crush = false;


            if (options.VcfWritingParams.ForceCrush.HasValue)
            {
                crush = (bool)options.VcfWritingParams.ForceCrush;
                options.VcfWritingParams.AllowMultipleVcfLinesPerLoci = !crush;
            }

            if (crush)
            {
                Console.WriteLine("crushing " + inputFile + "...");
                outputFile = inputFile.Replace(".vcf", ".crushed.vcf");
            }
            else
                Console.WriteLine("uncrushing " + inputFile + "...");

            if (File.Exists(outputFile))
                File.Delete(outputFile);

            //Update Vcf, variant by variant, based on the table data.
            VcfUpdater<bool>.UpdateVcfAlleleByAllele(outputFile, options, false, true, UpdateAllele, CanSkipNeverVcfLine,
                GetVcfFileWriter);
        }
    }
}
