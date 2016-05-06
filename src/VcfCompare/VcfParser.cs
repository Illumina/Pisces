using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SequencingFiles;

namespace VcfCompare
{
    class VcfParser
    {
        private static bool PassedChromosome(string currentChromosome, string chromosomeOfInterest)
        {
            if (chromosomeOfInterest == "chrM" && currentChromosome != "chrM") return true;
            if (chromosomeOfInterest == "chrY") return false;
            if (chromosomeOfInterest == "chrX" && currentChromosome == "chrY") return true;

            int chromosomeOfInterestNumber;
            int currentChromNumber;
            var chromOfInterestIsNumeric = int.TryParse(chromosomeOfInterest.Replace("chr", ""), out chromosomeOfInterestNumber);
            var currentChromIsNumeric = int.TryParse(currentChromosome.Replace("chr", ""), out currentChromNumber);

            if (currentChromNumber > chromosomeOfInterestNumber) return true;
            return false;
        }

        public static List<VcfVariant> GetAlleles(string path, VcfRegion region, Logger logger, out bool hitChromEnd)
        {
            var alleles = new List<VcfVariant>();
            hitChromEnd = false;

            using (var reader = new VcfReader(path))
            {
                var stopwatch = Stopwatch.StartNew();
                logger.Log("Reading "+path);
                if (region.Chromosome != null)
                {
                    var variantsRead = 0;
                    logger.Log("Looking for chromosome "+region.Chromosome + " "+region.Start + " - " + region.End);
                    while (true)
                    {
                        VcfVariant variant = new VcfVariant();
                        bool result = reader.GetNextVariant(variant);
                        variantsRead++;

                        if (variantsRead%50000 == 0)
                        {
                            logger.Log(string.Format("At chromosome '{0}' position {1} in {2}: {3} variants gathered",variant.ReferenceName, variant.ReferencePosition, stopwatch.Elapsed, alleles.Count));
                        }

                        if (!result)
                        {
                            hitChromEnd = true;
                            logger.Log("Reached end of VCF");
                            break;
                        }

                        if (PassedChromosome(variant.ReferenceName, region.Chromosome))
                        {
                            hitChromEnd = true;
                            logger.Log("Passed chromosome "+region.Chromosome);
                            break;
                        }

                        if (variant.ReferenceName != region.Chromosome)
                        {
                            continue;
                        }
                        if (variant.ReferencePosition < region.Start) continue;
                        if (region.End > 0 && variant.ReferencePosition > region.End)
                        {
                            logger.Log("Passed region end");
                            break;
                        }

                        alleles.Add(variant);
                    }
                    logger.Log(string.Format("Finished processing VCF {0}",path));
                }
                else
                {
                    alleles = reader.GetVariants().ToList();
                }
            }

            return alleles;
        }
    }
}