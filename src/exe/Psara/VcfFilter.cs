using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Common.IO.Utility;
using Pisces.IO;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;
using CommandLine.Options;

namespace Psara
{
    public class VcfFilter
    {
        public static void DoFiltering(PsaraOptions settings)
        {
            var geometricFilter = new GeometricFilter(settings.GeometricFilterParameters);
            //maybe expand to add other filters..

            var vcfIn = settings.InputVcf;
            var vcfName = Path.GetFileName(vcfIn);

            var outputFile = Path.Combine(settings.OutputDirectory, vcfName.Replace(".vcf", ".filtered.vcf"));
            outputFile = outputFile.Replace(".genome.filtered.vcf", ".filtered.genome.vcf");

            Logger.WriteToLog("filtering " + vcfIn + "...");

            if (File.Exists(outputFile))
                File.Delete(outputFile);

            List<string> header = VcfReader.GetAllHeaderLines(vcfIn);
            string cmdLine = "##Psara_cmdline=" + settings.QuotedCommandLineArgumentsString;
            VcfWriterConfig config = GetWriterConfigToMatchInputVcf(vcfIn);

            using (PsaraVcfWriter writer = new PsaraVcfWriter(outputFile, config, new VcfWriterInputContext(), header, cmdLine))
            {
                writer.WriteHeader();

                using (VcfReader reader = new VcfReader(vcfIn, false))
                {
                    var backLogVcfVariant = new VcfVariant();
                    var coLocatedAlleles = new List<CalledAllele>();
                    var moreVariantsInVcf = reader.GetNextVariant(backLogVcfVariant);
                    var incomingBatch = new List<CalledAllele>();

                   
                    while (moreVariantsInVcf)
                    {
                        if (incomingBatch.Count == 0)
                        {
                            incomingBatch = moreVariantsInVcf ? VcfVariantUtilities.Convert(new List<VcfVariant> { backLogVcfVariant },
                                                                config.ShouldOutputRcCounts,config.ShouldOutputTsCounts, false).ToList() : null;
                            moreVariantsInVcf = reader.GetNextVariant(backLogVcfVariant);
                        }
                        if ((coLocatedAlleles.Count == 0) || AreColocated(coLocatedAlleles, incomingBatch))
                        {
                            coLocatedAlleles.AddRange(incomingBatch);
                            incomingBatch.Clear();

                            //colocated alleles are left behind
                        }
                        else
                        {
                            FilterAndStreamOut(coLocatedAlleles, writer, geometricFilter);
                            coLocatedAlleles.Clear();

                            //incomingBatch alleles are left behind
                        }


                    }

                    //if you get here, there is no more unprocessed vcf variants but there could be
                    //colocated or an incoming batch of alleles left over. We need to write them to file before exiting.

                    FilterAndStreamOut(coLocatedAlleles, writer, geometricFilter);

                    FilterAndStreamOut(incomingBatch, writer, geometricFilter);

                }

            }
        }

        private static bool AreColocated(List<CalledAllele> coLocatedAlleles, List<CalledAllele> incomingBatch)
        {
            return ((coLocatedAlleles[0].ReferencePosition == incomingBatch[0].ReferencePosition) && (coLocatedAlleles[0].Chromosome == incomingBatch[0].Chromosome));
        }

        private static void FilterAndStreamOut(List<CalledAllele> alleles, VcfFileWriter writer, GeometricFilter filter)
        {
            alleles = filter.DoFiltering(alleles);

           
            try
            {
                writer.Write(alleles);
            }
            catch (Exception ex)
            {
                Logger.WriteWarningToLog("Problem writing alleles to vcf.");
                Logger.WriteExceptionToLog(ex);
                return;
            }
            
            writer.FlushBuffer();

        }

        //if I was not in a hurry, we could refactor this into the Config class and let it be re-used by Scylla and VQR...
        private static VcfWriterConfig GetWriterConfigToMatchInputVcf(string vcfIn)
        {
            List<string> vcfHeaderLines;
            using (var reader = new VcfReader(vcfIn))
            {
                vcfHeaderLines = reader.HeaderLines;
            }
         

            PiscesOptionsParser piscesOptionsParser = VcfConsumerAppParsingUtils.GetPiscesOptionsFromVcfHeader(vcfHeaderLines);
            if (piscesOptionsParser.ParsingFailed)
            {
                Logger.WriteToLog("Unable to parse the original Pisces commandline");
                throw new ArgumentException("Unable to parse the input vcf header: " + vcfIn);
            }

            VariantCallingParameters variantCallingParams = piscesOptionsParser.PiscesOptions.VariantCallingParameters;
            VcfWritingParameters vcfWritingParams = piscesOptionsParser.PiscesOptions.VcfWritingParameters;
            BamFilterParameters bamFilterParams = piscesOptionsParser.PiscesOptions.BamFilterParameters;

            var config = new VcfWriterConfig(variantCallingParams, vcfWritingParams, bamFilterParams, null, false, false);
            return config;
        }
    }
}
