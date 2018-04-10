using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Pisces.IO.Sequencing;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;

namespace ReformatVcf
{
    public class Reformat
    {
        public static void DoReformating(string inputFile, bool crush)
        {
            var outputFile = inputFile.Replace(".vcf", ".uncrushed.vcf");

            if (crush)
            {
                Console.WriteLine("crushing " + inputFile + "...");
                outputFile = inputFile.Replace(".vcf", ".crushed.vcf");
            }
            else
                Console.WriteLine("uncrushing " + inputFile + "...");

            if (File.Exists(outputFile))
                File.Delete(outputFile);

            var config = new VcfWriterConfig() { AllowMultipleVcfLinesPerLoci = !crush };
            using (VcfFileWriter writer = new VcfFileWriter(outputFile, config, new VcfWriterInputContext()))
            {
                writer.WriteHeader();

                using (VcfReader reader = new VcfReader(inputFile, false))
                {

                    var currentAllele = new CalledAllele();
                    var backLogVcfVariant = new VcfVariant();

                    var backLogExists = reader.GetNextVariant(backLogVcfVariant);

                    while (backLogExists)
                    {
                        var backLogAlleles = backLogExists ? VcfVariantUtilities.Convert(new List<VcfVariant> { backLogVcfVariant }).ToList() : null;

                        foreach (var allele in backLogAlleles)
                        {
                            try
                            {
                                writer.Write(new List<CalledAllele>() { allele });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Problem writing " + allele.ToString());
                                Console.WriteLine("Exception: " + ex);
                                return;
                            }
                        }


                        backLogExists = reader.GetNextVariant(backLogVcfVariant);

                        if (backLogAlleles[0].Chromosome != backLogVcfVariant.ReferenceName)
                        {
                            //we have switched to the next chr. flush the buffer.
                            writer.FlushBuffer();
                        }
                    }

                    writer.FlushBuffer();

                }

            }
        }
    }
}
