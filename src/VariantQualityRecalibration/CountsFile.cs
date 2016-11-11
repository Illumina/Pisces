using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Pisces.IO.Sequencing;
using Pisces.Processing.Utility;

namespace VariantQualityRecalibration
{
    public class Counts
    {
        private Dictionary<MutationCategory, double> _countsByCategory = new Dictionary<MutationCategory, double>();
        private double _numPossibleVariants = 0;

        public Dictionary<MutationCategory, double> CountsByCategory
        {
            get { return _countsByCategory; }
        }

        public double NumPossibleVariants
        {
            get { return _numPossibleVariants; }
            set { _numPossibleVariants = value; }
        }

        public Counts()
        {
            var categories = MutationCounter.GetAllMutationCategories();

            foreach (var nucleotideTransitionCategory in categories)
            {
                _countsByCategory.Add(nucleotideTransitionCategory, 0);
            }

        }


        public void LoadCountsFile(string file)
        {
            bool inRateSection = false;
            using (StreamReader sr = new StreamReader(file))
            {
                string line;

                while (true)
                {
                    line = sr.ReadLine();

                    if (line == "")
                        continue;

                    if (line == null)
                        break;

                    if (inRateSection)
                    {
                        string[] Splat = line.Split();

                        if (Splat.Length < 2)
                            continue;


                        double result = -1;
                        if (!(double.TryParse(Splat[1], out result)))
                        {
                            throw new ApplicationException("Unable to parse counts from noise file " + file);
                        }

                        switch (Splat[0])
                        {
                            case "AllPossibleVariants":
                                NumPossibleVariants += result;
                                break;

                            case "FalsePosVariantsFound":
                            case "ErrorRate(%)":
                            case "VariantsCountedTowardEstimate":
                            case "ErrorRateEstimate(%)":
                            case "MismatchEstimate(%)":
                                continue;

                            default:
                                MutationCategory category = MutationCounter.GetMutationCategory(Splat[0]);
                                CountsByCategory[category] += result;
                                break;
                        }

                    }
                    if (line.Contains("CountsByCategory"))
                        inRateSection = true;

                }
            }
        }

        public static string WriteCountsFile(string vcfIn, string outDir)
        {
            
            var variant = new VcfVariant();
            var countsPath = Path.Combine(outDir, Path.GetFileName(vcfIn).Replace(".vcf", ".counts"));
            var counter = new MutationCounter();

            using (VcfReader readerA = new VcfReader(vcfIn))
            {
                counter.StartWriter(countsPath);

                while (readerA.GetNextVariant(variant))
                {
                    try
                    {
                        counter.Add(variant);
                    }

                    catch (Exception ex)
                    {
                        Logger.WriteToLog(string.Format("Fatal error processing vcf; Check {0}, position {1}.  Exception: {2}",
                            variant.ReferenceName, variant.ReferencePosition, ex));
                        throw;
                    }
                }

                counter.CloseFalseCallsWriter();
            }

            return countsPath;
        }

    }
}
