using System;
using System.IO;
using System.Collections.Generic;
using Common.IO.Utility;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;

namespace VariantQualityRecalibration
{
    public class SignatureSorterResultFiles
    {
        public string BasicCountsFilePath;
        public string AmpliconEdgeCountsFilePath;
        public string AmpliconEdgeSuspectListFilePath;

        public SignatureSorterResultFiles(string allLociCountsFile, string edgeCountsFile, string suspectListFile)
        {
            BasicCountsFilePath = allLociCountsFile;
            AmpliconEdgeCountsFilePath = edgeCountsFile;
            AmpliconEdgeSuspectListFilePath = suspectListFile;
        }
    }
    public class SignatureSorter
    {
        // idea is to keep track of the disparity between two pools as a measure of FFPE degradation,
        // or overall oxidation affecting tissue sample.


        //possible SNP changes:
        //
        //
        // *    A   C   G   T
        //  A   *   1   2   3
        //  C   4   *   5   6
        //  G   7   8   *   9
        //  T   10  11  12  *
        //      

        public static SignatureSorterResultFiles StrainVcf(VQROptions options)
        {

            var variantList = new List<CalledAllele>() { };
            var basicCountsData = new CountData();
            var edgeVariantsCountData = new EdgeIssueCountData(options.ExtentofEdgeRegion);

            string basicCountsPath = CleanUpOldFiles(options.VcfPath, options.OutputDirectory, ".counts");
            string edgeCountsPath = CleanUpOldFiles(options.VcfPath, options.OutputDirectory, ".edgecounts");
            string edgeVariantsPath = CleanUpOldFiles(options.VcfPath, options.OutputDirectory, ".edgevariants");

            using (AlleleReader readerA = new AlleleReader(options.VcfPath))
            {
                while (readerA.GetNextVariants(out variantList))
                {
                    foreach (var variant in variantList)
                    {
                        try
                        {

                            basicCountsData.Add(variant);
                            edgeVariantsCountData.Add(variant, edgeVariantsPath);
                        }


                        catch (Exception ex)
                        {
                            Logger.WriteToLog(string.Format("Fatal error processing vcf; Check {0}, position {1}.  Exception: {2}",
                                variant.Chromosome, variant.ReferencePosition, ex));
                            throw;
                        }
                    }
                }

                //The edge issue filter trails N variants behind.
                //The following code cleans out the buffer, processing anything left behind in the buffer.
                for (int i = 0; i < options.ExtentofEdgeRegion; i++)
                    edgeVariantsCountData.Add(null, edgeVariantsPath);
                
                if (options.LociCount > 0)
                {
                    basicCountsData.ForceTotalPossibleMutations(options.LociCount);
                    edgeVariantsCountData.ForceTotalPossibleMutations(options.LociCount);
                }

                if (options.DoBasicChecks) { CountsFileWriter.WriteCountsFile(basicCountsPath, basicCountsData); }

                if (options.DoAmpliconPositionChecks) { CountsFileWriter.WriteCountsFile(edgeCountsPath, edgeVariantsCountData); }
            }

            return new SignatureSorterResultFiles(basicCountsPath, edgeCountsPath, edgeVariantsPath);
        }

        private static string CleanUpOldFiles(string vcfIn, string outDir, string newExtension)
        {
            var countsPath = Path.Combine(outDir, Path.GetFileName(vcfIn).Replace(".vcf", newExtension));
            var countsPathOld = Path.Combine(outDir, Path.GetFileName(vcfIn).Replace(".vcf", newExtension + ".original"));

            if (File.Exists(countsPath))
            {
                if (File.Exists(countsPathOld))
                {
                    File.Delete(countsPathOld);
                }
                File.Copy(countsPath, countsPathOld);
                File.Delete(countsPath);
            }

            return countsPath;
        }
    }
}
