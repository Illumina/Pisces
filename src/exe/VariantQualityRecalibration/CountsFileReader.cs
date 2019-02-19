using System.IO;
using Common.IO.Utility;

namespace VariantQualityRecalibration
{
    public class CountsFileReader
    {
        /// <summary>
        /// This is a pretty naive reader. The mutation categories must be listed first, and the rates must be listed last, or the parsing fails.
        /// There are some hard coded strings. Yes, maybe it should be in json.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static CountData ReadCountsFile(string file)
        {
            CountData variantCounts = new CountData();
            bool inRateSection = false;

            using (StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open)))
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
                        string[] splat = line.Split();

                        if (splat.Length < 2)
                            continue;


                        double result = -1;
                        if (!(double.TryParse(splat[1], out result)))
                        {
                            throw new IOException("Unable to parse counts from noise file " + file);
                        }

                        string firstWord = splat[0];
                        switch (firstWord)
                        {
                            case "AllPossibleVariants":
                                variantCounts.NumPossibleVariants += result;
                                break;

                            case "FalsePosVariantsFound":
                            case "ErrorRate(%)":
                            case "VariantsCountedTowardEstimate":
                            case "ErrorRateEstimate(%)":
                            case "MismatchEstimate(%)":
                                continue;

                            default:

                                //if its a mutation category - do something. Else do nothing
                                if (MutationCategoryUtil.IsValidCategory(firstWord))
                                {
                                    MutationCategory category = MutationCategoryUtil.GetMutationCategory(firstWord);

                                    //this category should always exist. this is just defensive
                                    if (!variantCounts.CountsByCategory.ContainsKey(category))
                                    {
                                        variantCounts.CountsByCategory.Add(category, 0);
                                        Logger.WriteWarningToLog("This counts file found a mutation category listed that this version of VQR is not aware of, and cannot process. Please check " + firstWord);
                                    }

                                    variantCounts.CountsByCategory[category] += result;
                                }
                                break;
                        }

                    }
                    if (line.Contains("CountsByCategory"))
                        inRateSection = true;

                }
            }

            return variantCounts;
        }

    }
}
