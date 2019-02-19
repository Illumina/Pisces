using System.IO;

namespace VariantQualityRecalibration
{
    class CountsFileWriter
    {

        public static void WriteCountsFile(string outFile, CountData counts)
        {
            
            using (var writer = new StreamWriter(new FileStream(outFile, FileMode.Create)))
            {
                writer.WriteLine();
                writer.WriteLine("CountsByCategory");
                foreach (MutationCategory mutation in counts.CountsByCategory.Keys)
                {
                    writer.WriteLine(mutation + "\t" + counts.CountsByCategory[mutation]);
                }

                writer.WriteLine();
                writer.WriteLine("AllPossibleVariants\t" + counts.NumPossibleVariants);
                writer.WriteLine("VariantsCountedTowardEstimate\t" + counts.TotalMutations);
                writer.WriteLine("MismatchEstimate(%)\t{0:N4}", (counts.ObservedMutationRate * 100));
            }
        }
    }
}
